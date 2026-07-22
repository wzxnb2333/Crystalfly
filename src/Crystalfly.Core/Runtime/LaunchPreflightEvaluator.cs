using Crystalfly.Core.Models;

namespace Crystalfly.Core.Runtime;

public sealed record LaunchPreflightResult(
    bool GameFilesReady,
    bool LoaderReady,
    bool DependenciesReady,
    bool SaveIsolationReady,
    IReadOnlyList<LaunchPreflightIssue> Issues)
{
    public LaunchPreflightResult(
        bool gameFilesReady,
        bool loaderReady,
        bool dependenciesReady,
        bool saveIsolationReady)
        : this(gameFilesReady, loaderReady, dependenciesReady, saveIsolationReady, [])
    {
    }

    public bool IsClean => Issues.Count == 0;

    public bool CanAttemptLaunch => HasStructuredIssues
        ? Issues.All(issue => issue.Severity != LaunchIssueSeverity.Blocking)
        : SummaryReady;

    public bool CanLaunchNormally => SummaryReady
        && Issues.All(issue => issue.Severity switch
        {
            LaunchIssueSeverity.Warning => issue.IsAcknowledged,
            _ => false
        });

    public bool CanForceLaunch => CanAttemptLaunch;

    public bool IsReady => CanLaunchNormally;

    private bool HasStructuredIssues => Issues.Count != 0;

    private bool SummaryReady => GameFilesReady && LoaderReady && DependenciesReady && SaveIsolationReady;

    public void Deconstruct(
        out bool gameFilesReady,
        out bool loaderReady,
        out bool dependenciesReady,
        out bool saveIsolationReady)
    {
        gameFilesReady = GameFilesReady;
        loaderReady = LoaderReady;
        dependenciesReady = DependenciesReady;
        saveIsolationReady = SaveIsolationReady;
    }
}

public static class LaunchPreflightEvaluator
{
    public static LaunchPreflightResult Evaluate(
        bool isKnownBuild,
        bool executableExists,
        LoaderState loaderState,
        IReadOnlyList<InstalledModReceipt> installedMods,
        bool transactionsHealthy,
        bool localLowReady,
        bool gameProcessRunning = false)
        => Evaluate(
            isKnownBuild,
            executableExists,
            loaderState,
            installedMods,
            transactionsHealthy,
            localLowReady,
            instanceId: "legacy",
            modHealthReports: [],
            acknowledgements: [],
            gameProcessRunning);

    public static LaunchPreflightResult Evaluate(
        bool isKnownBuild,
        bool executableExists,
        LoaderState loaderState,
        IReadOnlyList<InstalledModReceipt> installedMods,
        bool transactionsHealthy,
        bool localLowReady,
        string instanceId,
        IReadOnlyList<ModHealthReport> modHealthReports,
        IReadOnlyList<ModHealthAcknowledgement>? acknowledgements = null,
        bool gameProcessRunning = false)
    {
        ArgumentNullException.ThrowIfNull(installedMods);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(modHealthReports);
        acknowledgements ??= [];

        var installedById = installedMods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var issues = new List<LaunchPreflightIssue>();
        AddBaseIssues(
            issues,
            isKnownBuild,
            executableExists,
            loaderState,
            transactionsHealthy,
            localLowReady,
            gameProcessRunning);
        AddDependencyIssues(issues, installedMods, installedById);
        AddModHealthIssues(issues, modHealthReports, installedById);

        var evaluatedIssues = issues.Select(issue => issue.Severity == LaunchIssueSeverity.Warning
            && acknowledgements.Any(acknowledgement => acknowledgement.Matches(instanceId, issue))
                ? issue with { IsAcknowledged = true }
                : issue).ToArray();
        bool dependenciesReady = !issues.Any(issue => issue.Code is
            LaunchIssueCode.MissingDependency or LaunchIssueCode.DisabledDependency);
        bool loaderReady = loaderState is not LoaderState.Conflict and not LoaderState.Drifted
            && (isKnownBuild || loaderState == LoaderState.Vanilla);

        return new LaunchPreflightResult(
            executableExists && !gameProcessRunning,
            loaderReady,
            dependenciesReady,
            transactionsHealthy && localLowReady,
            evaluatedIssues);
    }

    private static void AddBaseIssues(
        List<LaunchPreflightIssue> issues,
        bool isKnownBuild,
        bool executableExists,
        LoaderState loaderState,
        bool transactionsHealthy,
        bool localLowReady,
        bool gameProcessRunning)
    {
        if (!executableExists)
        {
            issues.Add(Blocking(LaunchIssueCode.ExecutableMissing));
        }
        if (gameProcessRunning)
        {
            issues.Add(Blocking(LaunchIssueCode.GameAlreadyRunning));
        }
        if (loaderState == LoaderState.Conflict)
        {
            issues.Add(Blocking(LaunchIssueCode.LoaderConflict));
        }
        if (loaderState == LoaderState.Drifted)
        {
            issues.Add(Blocking(LaunchIssueCode.LoaderDrifted));
        }
        if (!isKnownBuild && loaderState != LoaderState.Vanilla)
        {
            issues.Add(new LaunchPreflightIssue
            {
                Code = LaunchIssueCode.UnsupportedBuildLoaderCombination,
                Severity = LaunchIssueSeverity.Blocking,
                Arguments = [loaderState.ToString()]
            });
        }
        if (!transactionsHealthy)
        {
            issues.Add(Blocking(LaunchIssueCode.TransactionUnhealthy));
        }
        if (!localLowReady)
        {
            issues.Add(Blocking(LaunchIssueCode.LocalLowNotReady));
        }
    }

    private static void AddDependencyIssues(
        List<LaunchPreflightIssue> issues,
        IReadOnlyList<InstalledModReceipt> installedMods,
        IReadOnlyDictionary<string, InstalledModReceipt> installedById)
    {
        foreach (InstalledModReceipt mod in installedMods.Where(mod => mod.Enabled))
        {
            foreach (string dependency in mod.Dependencies)
            {
                if (!installedById.TryGetValue(dependency, out InstalledModReceipt? installed))
                {
                    issues.Add(ModIssue(
                        LaunchIssueCode.MissingDependency,
                        LaunchIssueSeverity.Forceable,
                        mod.Id,
                        arguments: [mod.Id, dependency]));
                }
                else if (!installed.Enabled)
                {
                    issues.Add(ModIssue(
                        LaunchIssueCode.DisabledDependency,
                        LaunchIssueSeverity.Forceable,
                        mod.Id,
                        arguments: [mod.Id, dependency]));
                }
            }
        }
    }

    private static void AddModHealthIssues(
        List<LaunchPreflightIssue> issues,
        IReadOnlyList<ModHealthReport> modHealthReports,
        IReadOnlyDictionary<string, InstalledModReceipt> installedById)
    {
        foreach (ModHealthReport report in modHealthReports)
        {
            _ = installedById.TryGetValue(report.ModId, out InstalledModReceipt? receipt);

            switch (report.Status)
            {
                case ModHealthStatus.Healthy:
                    break;
                case ModHealthStatus.CriticalFileMissing when receipt is { Enabled: true }:
                    AddFileIssues(
                        issues,
                        LaunchIssueCode.ModCriticalFileMissing,
                        LaunchIssueSeverity.Forceable,
                        report.ModId,
                        report.MissingFiles,
                        report.CurrentFileSha256ByPath);
                    break;
                case ModHealthStatus.ModifiedFile:
                    AddFileIssues(
                        issues,
                        LaunchIssueCode.ModModifiedFile,
                        LaunchIssueSeverity.Warning,
                        report.ModId,
                        report.ModifiedFiles,
                        report.CurrentFileSha256ByPath);
                    break;
                case ModHealthStatus.ExtraFile:
                    AddFileIssues(
                        issues,
                        LaunchIssueCode.ModExtraFile,
                        LaunchIssueSeverity.Warning,
                        report.ModId,
                        report.ExtraFiles,
                        report.CurrentFileSha256ByPath);
                    break;
                case ModHealthStatus.UnmanagedExternal:
                    issues.Add(ModIssue(
                        LaunchIssueCode.UnmanagedExternalMod,
                        LaunchIssueSeverity.Warning,
                        report.ModId,
                        arguments: [report.ModId]));
                    break;
                case ModHealthStatus.Indeterminate:
                    issues.Add(ModIssue(
                        LaunchIssueCode.ModHealthIndeterminate,
                        LaunchIssueSeverity.Warning,
                        report.ModId,
                        arguments: [report.ModId]));
                    break;
            }
        }
    }

    private static void AddFileIssues(
        List<LaunchPreflightIssue> issues,
        LaunchIssueCode code,
        LaunchIssueSeverity severity,
        string modId,
        IReadOnlyList<string> relativePaths,
        IReadOnlyDictionary<string, string> currentFileSha256ByPath)
    {
        if (relativePaths.Count == 0)
        {
            issues.Add(ModIssue(code, severity, modId, arguments: [modId]));
            return;
        }

        foreach (string relativePath in relativePaths)
        {
            issues.Add(ModIssue(
                code,
                severity,
                modId,
                relativePath,
                [modId, relativePath],
                FindCurrentFileSha256(currentFileSha256ByPath, relativePath)));
        }
    }

    private static string? FindCurrentFileSha256(
        IReadOnlyDictionary<string, string> currentFileSha256ByPath,
        string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/');
        foreach ((string path, string sha256) in currentFileSha256ByPath)
        {
            if (string.Equals(
                path.Replace('\\', '/'),
                normalizedPath,
                StringComparison.OrdinalIgnoreCase))
            {
                return sha256;
            }
        }
        return null;
    }

    private static LaunchPreflightIssue Blocking(LaunchIssueCode code) => new()
    {
        Code = code,
        Severity = LaunchIssueSeverity.Blocking
    };

    private static LaunchPreflightIssue ModIssue(
        LaunchIssueCode code,
        LaunchIssueSeverity severity,
        string modId,
        string? relativeFilePath = null,
        IReadOnlyList<string>? arguments = null,
        string? currentFileSha256 = null) => new()
        {
            Code = code,
            Severity = severity,
            SubjectModId = modId,
            RelativeFilePath = relativeFilePath,
            CurrentFileSha256 = currentFileSha256,
            Arguments = arguments ?? []
        };
}
