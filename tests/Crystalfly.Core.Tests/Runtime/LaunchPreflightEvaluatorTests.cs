using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Tests.Runtime;

public sealed class LaunchPreflightEvaluatorTests
{
    [Theory]
    [InlineData(LoaderState.Conflict)]
    [InlineData(LoaderState.Drifted)]
    public void Conflict_or_drifted_loader_blocks_launch(LoaderState loaderState)
    {
        var result = LaunchPreflightEvaluator.Evaluate(
            isKnownBuild: true,
            executableExists: true,
            loaderState,
            [],
            transactionsHealthy: true,
            localLowReady: true);

        Assert.False(result.LoaderReady);
        Assert.False(result.IsReady);
    }

    [Fact]
    public void Unknown_build_only_allows_vanilla_loader()
    {
        var vanilla = LaunchPreflightEvaluator.Evaluate(false, true, LoaderState.Vanilla, [], true, true);
        var modded = LaunchPreflightEvaluator.Evaluate(false, true, LoaderState.BepInEx, [], true, true);

        Assert.True(vanilla.IsReady);
        Assert.False(modded.LoaderReady);
        Assert.False(modded.IsReady);
    }

    [Fact]
    public void Missing_or_disabled_dependency_blocks_launch()
    {
        var mod = Receipt("feature", enabled: true, dependencies: ["library"]);
        var disabledLibrary = Receipt("library", enabled: false);

        var missing = LaunchPreflightEvaluator.Evaluate(true, true, LoaderState.ModdingApi, [mod], true, true);
        var disabled = LaunchPreflightEvaluator.Evaluate(
            true,
            true,
            LoaderState.ModdingApi,
            [mod, disabledLibrary],
            true,
            true);

        Assert.False(missing.DependenciesReady);
        Assert.False(disabled.DependenciesReady);
    }

    [Fact]
    public void Healthy_instance_passes_all_checks()
    {
        var library = Receipt("library", enabled: true);
        var mod = Receipt("feature", enabled: true, dependencies: ["library"]);

        var result = LaunchPreflightEvaluator.Evaluate(
            true,
            true,
            LoaderState.ModdingApi,
            [mod, library],
            true,
            true);

        Assert.True(result.GameFilesReady);
        Assert.True(result.LoaderReady);
        Assert.True(result.DependenciesReady);
        Assert.True(result.SaveIsolationReady);
        Assert.True(result.IsReady);
    }

    [Fact]
    public void Running_game_process_blocks_preflight()
    {
        var result = LaunchPreflightEvaluator.Evaluate(
            true,
            true,
            LoaderState.Vanilla,
            [],
            true,
            true,
            gameProcessRunning: true);

        Assert.False(result.GameFilesReady);
        Assert.False(result.IsReady);
    }

    [Fact]
    public void Legacy_result_constructor_and_deconstruction_preserve_summary_semantics()
    {
        var result = new LaunchPreflightResult(
            gameFilesReady: false,
            loaderReady: true,
            dependenciesReady: true,
            saveIsolationReady: true);

        (bool gameFilesReady, bool loaderReady, bool dependenciesReady, bool saveIsolationReady) = result;

        Assert.False(gameFilesReady);
        Assert.True(loaderReady);
        Assert.True(dependenciesReady);
        Assert.True(saveIsolationReady);
        Assert.False(result.IsReady);
        Assert.False(result.CanAttemptLaunch);
        Assert.False(result.CanForceLaunch);
    }

    [Fact]
    public void Blocking_conditions_are_structured_and_cannot_be_forced()
    {
        var result = LaunchPreflightEvaluator.Evaluate(
            isKnownBuild: false,
            executableExists: false,
            LoaderState.Conflict,
            [],
            transactionsHealthy: false,
            localLowReady: false,
            gameProcessRunning: true);

        Assert.All(result.Issues, issue => Assert.Equal(LaunchIssueSeverity.Blocking, issue.Severity));
        Assert.Contains(result.Issues, issue => issue.Code == LaunchIssueCode.ExecutableMissing);
        Assert.Contains(result.Issues, issue => issue.Code == LaunchIssueCode.GameAlreadyRunning);
        Assert.Contains(result.Issues, issue => issue.Code == LaunchIssueCode.LoaderConflict);
        Assert.Contains(result.Issues, issue => issue.Code == LaunchIssueCode.UnsupportedBuildLoaderCombination);
        Assert.Contains(result.Issues, issue => issue.Code == LaunchIssueCode.TransactionUnhealthy);
        Assert.Contains(result.Issues, issue => issue.Code == LaunchIssueCode.LocalLowNotReady);
        Assert.False(result.IsClean);
        Assert.False(result.CanAttemptLaunch);
        Assert.False(result.CanLaunchNormally);
        Assert.False(result.CanForceLaunch);
    }

    [Fact]
    public void Missing_and_disabled_dependencies_are_forceable_structured_issues()
    {
        var missingDependency = Receipt("missing-feature", enabled: true, dependencies: ["missing-library"]);
        var disabledDependency = Receipt("disabled-feature", enabled: true, dependencies: ["disabled-library"]);
        var disabledLibrary = Receipt("disabled-library", enabled: false);

        var result = LaunchPreflightEvaluator.Evaluate(
            true,
            true,
            LoaderState.ModdingApi,
            [missingDependency, disabledDependency, disabledLibrary],
            true,
            true);

        Assert.Collection(
            result.Issues.OrderBy(issue => issue.SubjectModId, StringComparer.Ordinal),
            issue =>
            {
                Assert.Equal(LaunchIssueCode.DisabledDependency, issue.Code);
                Assert.Equal(LaunchIssueSeverity.Forceable, issue.Severity);
                Assert.Equal("disabled-feature", issue.SubjectModId);
                Assert.Equal(["disabled-feature", "disabled-library"], issue.Arguments);
            },
            issue =>
            {
                Assert.Equal(LaunchIssueCode.MissingDependency, issue.Code);
                Assert.Equal(LaunchIssueSeverity.Forceable, issue.Severity);
                Assert.Equal("missing-feature", issue.SubjectModId);
                Assert.Equal(["missing-feature", "missing-library"], issue.Arguments);
            });
        Assert.False(result.DependenciesReady);
        Assert.True(result.CanAttemptLaunch);
        Assert.False(result.CanLaunchNormally);
        Assert.True(result.CanForceLaunch);
    }

    [Fact]
    public void Enabled_mod_missing_files_are_forceable_but_disabled_mod_health_is_ignored()
    {
        var enabled = Receipt("enabled", enabled: true);
        var disabled = Receipt("disabled", enabled: false);
        var result = EvaluateWithHealth(
            [enabled, disabled],
            [
                Health("enabled", ModHealthStatus.CriticalFileMissing, missing: ["Mods/enabled/main.dll"]),
                Health("disabled", ModHealthStatus.CriticalFileMissing, missing: ["Mods/disabled/main.dll"])
            ]);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(LaunchIssueCode.ModCriticalFileMissing, issue.Code);
        Assert.Equal(LaunchIssueSeverity.Forceable, issue.Severity);
        Assert.Equal("enabled", issue.SubjectModId);
        Assert.Equal("Mods/enabled/main.dll", issue.RelativeFilePath);
        Assert.True(result.CanAttemptLaunch);
        Assert.False(result.CanLaunchNormally);
        Assert.True(result.CanForceLaunch);
    }

    [Theory]
    [InlineData(ModHealthStatus.ModifiedFile, LaunchIssueCode.ModModifiedFile)]
    [InlineData(ModHealthStatus.ExtraFile, LaunchIssueCode.ModExtraFile)]
    [InlineData(ModHealthStatus.UnmanagedExternal, LaunchIssueCode.UnmanagedExternalMod)]
    [InlineData(ModHealthStatus.Indeterminate, LaunchIssueCode.ModHealthIndeterminate)]
    public void Mod_health_warnings_require_confirmation(
        ModHealthStatus status,
        LaunchIssueCode expectedCode)
    {
        var receipt = Receipt("health-mod", enabled: true);
        var report = status switch
        {
            ModHealthStatus.ModifiedFile => Health("health-mod", status, modified: ["Mods/health/main.dll"]),
            ModHealthStatus.ExtraFile => Health("health-mod", status, extra: ["Mods/health/extra.dll"]),
            _ => Health("health-mod", status)
        };

        var result = EvaluateWithHealth([receipt], [report]);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(expectedCode, issue.Code);
        Assert.Equal(LaunchIssueSeverity.Warning, issue.Severity);
        Assert.False(issue.IsAcknowledged);
        Assert.False(result.IsClean);
        Assert.True(result.CanAttemptLaunch);
        Assert.False(result.CanLaunchNormally);
        Assert.True(result.CanForceLaunch);
    }

    [Fact]
    public void Exact_warning_acknowledgement_allows_normal_launch_without_removing_issue()
    {
        var receipt = Receipt("health-mod", enabled: true);
        var report = Health(
            "health-mod",
            ModHealthStatus.ModifiedFile,
            modified: ["Mods/health/main.dll"]);
        var first = EvaluateWithHealth([receipt], [report]);
        var acknowledgement = ModHealthAcknowledgement.Create("instance-1", Assert.Single(first.Issues));

        var acknowledged = EvaluateWithHealth([receipt], [report], [acknowledgement]);

        var issue = Assert.Single(acknowledged.Issues);
        Assert.True(issue.IsAcknowledged);
        Assert.False(acknowledged.IsClean);
        Assert.True(acknowledged.CanLaunchNormally);
        Assert.True(acknowledged.IsReady);
    }

    [Fact]
    public void Acknowledgement_fingerprint_changes_with_each_health_identity_component()
    {
        var original = WarningIssue(
            LaunchIssueCode.ModModifiedFile,
            "health-mod",
            "Mods/health/main.dll",
            "AAAAAAAA");
        var originalFingerprint = ModHealthAcknowledgement.Create("instance-1", original).Fingerprint;

        Assert.NotEqual(
            originalFingerprint,
            ModHealthAcknowledgement.Create("instance-2", original).Fingerprint);
        Assert.NotEqual(
            originalFingerprint,
            ModHealthAcknowledgement.Create("instance-1", original with { SubjectModId = "other-mod" }).Fingerprint);
        Assert.NotEqual(
            originalFingerprint,
            ModHealthAcknowledgement.Create(
                "instance-1",
                original with { Code = LaunchIssueCode.ModExtraFile }).Fingerprint);
        Assert.NotEqual(
            originalFingerprint,
            ModHealthAcknowledgement.Create(
                "instance-1",
                original with { RelativeFilePath = "Mods/health/other.dll" }).Fingerprint);
        Assert.NotEqual(
            originalFingerprint,
            ModHealthAcknowledgement.Create(
                "instance-1",
                original with { CurrentFileSha256 = "BBBBBBBB" }).Fingerprint);
    }

    [Fact]
    public void Acknowledgements_never_suppress_forceable_or_blocking_issues()
    {
        var receipt = Receipt("broken", enabled: true, dependencies: ["missing"]);
        var report = Health("broken", ModHealthStatus.CriticalFileMissing, missing: ["Mods/broken/main.dll"]);
        var first = EvaluateWithHealth([receipt], [report]);
        var acknowledgements = first.Issues
            .Select(issue => ModHealthAcknowledgement.Create("instance-1", issue))
            .ToArray();

        var result = EvaluateWithHealth(
            [receipt],
            [report],
            acknowledgements,
            executableExists: false);

        Assert.Equal(3, result.Issues.Count);
        Assert.DoesNotContain(result.Issues, issue => issue.IsAcknowledged);
        Assert.False(result.CanForceLaunch);
    }

    private static LaunchPreflightResult EvaluateWithHealth(
        IReadOnlyList<InstalledModReceipt> receipts,
        IReadOnlyList<ModHealthReport> healthReports,
        IReadOnlyList<ModHealthAcknowledgement>? acknowledgements = null,
        bool executableExists = true) => LaunchPreflightEvaluator.Evaluate(
            isKnownBuild: true,
            executableExists,
            LoaderState.ModdingApi,
            receipts,
            transactionsHealthy: true,
            localLowReady: true,
            instanceId: "instance-1",
            modHealthReports: healthReports,
            acknowledgements: acknowledgements ?? []);

    private static ModHealthReport Health(
        string modId,
        ModHealthStatus status,
        IReadOnlyList<string>? missing = null,
        IReadOnlyList<string>? modified = null,
        IReadOnlyList<string>? extra = null) => new()
        {
            ModId = modId,
            Status = status,
            MissingFiles = missing ?? [],
            ModifiedFiles = modified ?? [],
            ExtraFiles = extra ?? []
        };

    private static LaunchPreflightIssue WarningIssue(
        LaunchIssueCode code,
        string modId,
        string relativePath,
        string currentFileSha256) => new()
        {
            Code = code,
            Severity = LaunchIssueSeverity.Warning,
            SubjectModId = modId,
            RelativeFilePath = relativePath,
            CurrentFileSha256 = currentFileSha256,
            Arguments = [modId, relativePath]
        };

    private static InstalledModReceipt Receipt(
        string id,
        bool enabled,
        IReadOnlyList<string>? dependencies = null) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        LoaderId = "modding-api",
        InstallRoot = $"Mods/{id}",
        Enabled = enabled,
        Dependencies = dependencies ?? []
    };
}
