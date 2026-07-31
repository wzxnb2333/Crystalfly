using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class SpeedrunEnvironmentVerifierTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-speedrun-{Guid.NewGuid():N}");

    [Fact]
    public async Task Writes_a_v2_report_for_a_clean_runtime_patches_copy()
    {
        VerificationFixture fixture = await CreateFixtureAsync();

        SpeedrunVerificationResult result = await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request);

        Assert.Equal(2, result.Report.SchemaVersion);
        Assert.True(result.Report.IsReadyToLaunch);
        Assert.True(result.Report.IsOfficiallyVerified);
        Assert.Empty(result.Report.Issues);
        Assert.Equal(4, result.Report.Files.Count);
        Assert.Equal(new RuntimePatchesConfiguration(), result.Report.RuntimePatchesConfiguration);
        SpeedrunVerificationReport saved = await AtomicJsonStore.ReadAsync<SpeedrunVerificationReport>(result.ReportPath);
        Assert.Equal(result.Report.Id, saved.Id);
        Assert.Equal(result.Report.ActualBuildFingerprint, saved.ActualBuildFingerprint);
        Assert.Equal(result.Report.RuntimePatchesConfiguration, saved.RuntimePatchesConfiguration);
        Assert.Equal(result.Report.Issues, saved.Issues);
    }

    [Theory]
    [InlineData("texture.png")]
    [InlineData("skins/custom.png")]
    [InlineData("readme.txt")]
    [InlineData("hollow_knight_Data/Managed/harmless-extra.dll")]
    public async Task Ordinary_extra_files_do_not_block_launch(string relativePath)
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        string path = Path.Combine(fixture.InstanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "extra");

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request)).Report;

        Assert.True(report.IsReadyToLaunch);
        Assert.DoesNotContain(report.Issues, issue => issue.Code == SpeedrunIssueCode.UnlistedFile);
    }

    [Theory]
    [InlineData("BepInEx/core/loader.dll")]
    [InlineData("doorstop_config.ini")]
    [InlineData("winhttp.dll")]
    [InlineData("hollow_knight_Data/Managed/Mods/Example.dll")]
    [InlineData("hollow_knight_Data/Managed/Modding.dll")]
    public async Task Loader_and_mod_markers_block_launch(string relativePath)
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        string path = Path.Combine(fixture.InstanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "forbidden");

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request)).Report;

        Assert.False(report.IsReadyToLaunch);
        Assert.Contains(report.Issues, issue =>
            issue.Code == SpeedrunIssueCode.ForbiddenFile
            && issue.Severity == SpeedrunIssueSeverity.EnvironmentError);
    }

    [Fact]
    public async Task Wrong_runtime_patches_dll_and_invalid_configuration_block_launch()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.InstanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll"),
            "tampered");
        await File.WriteAllTextAsync(fixture.Request.RuntimePatchesConfigurationPath, "{not-json");

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request)).Report;

        Assert.False(report.IsReadyToLaunch);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.HashMismatch);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.InvalidRuntimePatchesConfiguration);
    }

    [Fact]
    public async Task Enabled_rule_sensitive_features_warn_but_do_not_block_launch()
    {
        VerificationFixture fixture = await CreateFixtureAsync("1.4.3.2");
        await RuntimePatchesConfiguration.WriteAsync(
            fixture.Request.RuntimePatchesConfigurationPath,
            new RuntimePatchesConfiguration { FasterIntroSkip = true, MiniSaveStates = true });

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request)).Report;

        Assert.True(report.IsReadyToLaunch);
        Assert.True(report.IsOfficiallyVerified);
        Assert.Collection(
            report.Issues.OrderBy(issue => issue.Code),
            issue =>
            {
                Assert.Equal(SpeedrunIssueCode.MiniSaveStatesRuleWarning, issue.Code);
                Assert.Equal(SpeedrunIssueSeverity.RuleWarning, issue.Severity);
            },
            issue =>
            {
                Assert.Equal(SpeedrunIssueCode.FasterIntroSkipRuleWarning, issue.Code);
                Assert.Equal(SpeedrunIssueSeverity.RuleWarning, issue.Severity);
            });
    }

    [Fact]
    public async Task Transaction_and_local_low_failures_block_launch()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        SpeedrunVerificationRequest request = fixture.Request with
        {
            HasTransactionIssue = true,
            HasLocalLowIssue = true
        };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.False(report.IsReadyToLaunch);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.TransactionNeedsAttention);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.LocalLowNeedsAttention);
    }

    [Fact]
    public async Task Legacy_template_is_not_a_verified_runtime_patches_environment()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        SpeedrunVerificationRequest request = fixture.Request with
        {
            Instance = fixture.Request.Instance with { SpeedrunTemplateId = "race-1221" },
            Template = fixture.Request.Template with { Id = "race-1221" }
        };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.False(report.IsReadyToLaunch);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.UnsupportedOfficialTemplate);
    }

    private async Task<VerificationFixture> CreateFixtureAsync(string buildId = "1.2.2.1")
    {
        string instanceRoot = Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(instanceRoot, "hollow_knight_Data", "Managed"));
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), "game");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "UnityPlayer.dll"), "unity");
        await File.WriteAllTextAsync(
            Path.Combine(instanceRoot, "hollow_knight_Data", "globalgamemanagers"),
            "managers");
        await File.WriteAllTextAsync(
            Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll"),
            "runtime-patches");

        string templateId = RuntimePatchesPolicy.GetTemplateId(buildId)!;
        string assetId = RuntimePatchesPolicy.GetAssetId(buildId)!;
        string configurationPath = Path.Combine(root, "local-low", Guid.NewGuid().ToString("N"), RuntimePatchesConfiguration.FileName);
        await RuntimePatchesConfiguration.WriteAsync(configurationPath, new RuntimePatchesConfiguration());
        string executableSha256 = await HashAsync(Path.Combine(instanceRoot, "hollow_knight.exe"));
        string unitySha256 = await HashAsync(Path.Combine(instanceRoot, "UnityPlayer.dll"));
        string managersSha256 = await HashAsync(Path.Combine(instanceRoot, "hollow_knight_Data", "globalgamemanagers"));
        string runtimePatchesSha256 = await HashAsync(
            Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll"));
        GameBuild build = new()
        {
            Id = buildId,
            DisplayVersion = buildId,
            DepotId = 367521,
            ManifestId = "1",
            ExecutableSha256 = executableSha256,
            UnityPlayerSha256 = unitySha256,
            GlobalGameManagersSha256 = managersSha256
        };
        SpeedrunTemplate template = new()
        {
            Id = templateId,
            Name = $"RuntimePatches {buildId}",
            BuildId = buildId,
            IsOfficial = true,
            RulesRevision = RuntimePatchesPolicy.RulesRevision,
            FileManifestId = $"files-{templateId}",
            RequiredAssetIds = [assetId]
        };
        InstanceRecord instance = new()
        {
            Id = "speedrun-copy",
            Name = "Speedrun Copy",
            RootPath = instanceRoot,
            BuildId = build.Id,
            Purpose = InstancePurpose.OfficialSpeedrun,
            ProvisioningMode = InstanceProvisioningMode.FullCopy,
            SpeedrunTemplateId = template.Id,
            SpeedrunRulesRevision = template.RulesRevision,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var request = new SpeedrunVerificationRequest
        {
            Instance = instance,
            Template = template,
            TemplateSource = SpeedrunTemplateSource.OfficialCatalog,
            ExpectedBuild = build,
            CurrentRulesRevision = template.RulesRevision,
            FileManifest = new SpeedrunFileManifest
            {
                Id = template.FileManifestId,
                BuildId = build.Id,
                RulesRevision = template.RulesRevision,
                Files =
                [
                    new SpeedrunFileRule
                    {
                        RelativePath = "hollow_knight_Data/Managed/Assembly-CSharp.dll",
                        Sha256 = runtimePatchesSha256,
                        Kind = SpeedrunFileKind.Tool,
                        AssetId = assetId,
                        AssetVersion = "1.0.2"
                    }
                ]
            },
            RuntimePatchesConfigurationPath = configurationPath,
            ReportsDirectory = Path.Combine(root, "reports")
        };
        return new VerificationFixture(
            instanceRoot,
            request,
            new SpeedrunEnvironmentVerifier(new FixedTimeProvider(DateTimeOffset.Parse("2026-07-31T00:00:00Z"))));
    }

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed record VerificationFixture(
        string InstanceRoot,
        SpeedrunVerificationRequest Request,
        SpeedrunEnvironmentVerifier Verifier);

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
