using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class SpeedrunEnvironmentVerifierTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-speedrun-{Guid.NewGuid():N}");

    [Fact]
    public async Task Writes_a_verified_report_for_a_clean_dedicated_full_copy()
    {
        VerificationFixture fixture = await CreateFixtureAsync();

        SpeedrunVerificationResult result = await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request);

        Assert.True(result.Report.IsReadyToLaunch);
        Assert.True(result.Report.IsOfficiallyVerified);
        Assert.Equal(fixture.Request.FileManifest.Id, result.Report.FileManifestId);
        Assert.Empty(result.Report.Issues);
        Assert.Equal(4, result.Report.Files.Count);
        Assert.True(File.Exists(result.ReportPath));
        SpeedrunVerificationReport saved = await AtomicJsonStore.ReadAsync<SpeedrunVerificationReport>(result.ReportPath);
        Assert.Equal(result.Report.Id, saved.Id);
        Assert.Equal(result.Report.ActualBuildFingerprint, saved.ActualBuildFingerprint);
        Assert.Equal(result.Report.Files, saved.Files);
        Assert.Collection(saved.Tools, tool =>
        {
            Assert.Equal(result.Report.Tools[0].AssetId, tool.AssetId);
            Assert.Equal(result.Report.Tools[0].Version, tool.Version);
            Assert.Equal(result.Report.Tools[0].Files, tool.Files);
        });
        Assert.Equal(result.Report.Issues, saved.Issues);
    }

    [Fact]
    public async Task Custom_template_can_pass_preflight_but_is_never_official()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        SpeedrunVerificationRequest request = fixture.Request with
        {
            TemplateSource = SpeedrunTemplateSource.Custom,
            Instance = fixture.Request.Instance with
            {
                Purpose = InstancePurpose.CustomSpeedrun,
                SpeedrunTemplateId = "custom"
            },
            Template = fixture.Request.Template with { Id = "custom", IsOfficial = true }
        };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.True(report.IsReadyToLaunch);
        Assert.False(report.IsOfficiallyVerified);
    }

    [Fact]
    public async Task Changed_rules_revision_invalidates_official_verification()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        SpeedrunVerificationRequest request = fixture.Request with { CurrentRulesRevision = "rules-new" };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.False(report.IsReadyToLaunch);
        Assert.False(report.IsOfficiallyVerified);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.RulesRevisionMismatch);
    }

    [Fact]
    public async Task Official_template_rejects_a_mismatched_file_manifest()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        SpeedrunVerificationRequest request = fixture.Request with
        {
            FileManifest = fixture.Request.FileManifest with { Id = "different-manifest" }
        };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.InvalidFileManifest);
    }

    [Theory]
    [InlineData("BepInEx/core/loader.dll")]
    [InlineData("doorstop_config.ini")]
    [InlineData("winhttp.dll")]
    [InlineData("hollow_knight_Data/Managed/Mods/Example.dll")]
    [InlineData("hollow_knight_Data/Managed/Modding.dll")]
    [InlineData("DebugMod.dll")]
    [InlineData("SpeedrunQoL.dll")]
    [InlineData("Benchwarp.dll")]
    [InlineData("HKTimer.dll")]
    public async Task Official_template_rejects_forbidden_loader_and_mod_files(string relativePath)
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        string path = Path.Combine(fixture.InstanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "forbidden");
        SpeedrunFileRule extraRule = await RuleAsync(fixture.InstanceRoot, relativePath, SpeedrunFileKind.Tool);
        SpeedrunVerificationRequest request = fixture.Request with
        {
            FileManifest = fixture.Request.FileManifest with
            {
                Files = [.. fixture.Request.FileManifest.Files, extraRule]
            }
        };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.False(report.IsReadyToLaunch);
        Assert.Contains(report.Issues, issue =>
            issue.Code == SpeedrunIssueCode.ForbiddenFile && issue.RelativePath == relativePath);
    }

    [Fact]
    public async Task Official_template_rejects_non_whitelisted_files()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.InstanceRoot, "unexpected.dll"), "extra");

        SpeedrunVerificationReport report =
            (await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request)).Report;

        Assert.Contains(report.Issues, issue =>
            issue.Code == SpeedrunIssueCode.UnlistedFile && issue.RelativePath == "unexpected.dll");
    }

    [Fact]
    public async Task Official_template_requires_a_dedicated_full_copy_for_the_selected_template()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        SpeedrunVerificationRequest request = fixture.Request with
        {
            Instance = fixture.Request.Instance with
            {
                Purpose = InstancePurpose.General,
                ProvisioningMode = InstanceProvisioningMode.Imported,
                SpeedrunTemplateId = "another-template",
                LoaderId = "modding-api-37"
            }
        };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.InstanceNotDedicated);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.InstanceNotFullCopy);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.TemplateMismatch);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.ForbiddenFile);
    }

    [Fact]
    public async Task Missing_instance_still_produces_a_failed_report()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        Directory.Delete(fixture.InstanceRoot, recursive: true);

        SpeedrunVerificationResult result = await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request);

        Assert.True(File.Exists(result.ReportPath));
        Assert.False(result.Report.IsReadyToLaunch);
        Assert.Contains(result.Report.Issues, issue => issue.Code == SpeedrunIssueCode.InstanceNotFound);
    }

    [Fact]
    public async Task Race_1578_requires_an_allowed_load_normaliser_selection()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        string toolPath = Path.Combine(fixture.InstanceRoot, "LoadNormaliser.dll");
        await File.WriteAllTextAsync(toolPath, "normaliser");
        SpeedrunFileRule tool = await RuleAsync(
            fixture.InstanceRoot,
            "LoadNormaliser.dll",
            SpeedrunFileKind.Tool,
            "load-normaliser-1.1",
            "1.1");
        SpeedrunTemplate template = fixture.Request.Template with
        {
            Id = "race-1578",
            Name = "Race 1.5.78",
            BuildId = "1.5.78.11833",
            FileManifestId = "files-race-1578",
            RequiredAssetIds = ["load-normaliser-1.1"],
            LoadNormaliserAvailable = true,
            RequiresLoadNormaliserSelection = true,
            AllowedLoadNormaliserSeconds = [1, 2, 3, 5]
        };
        SpeedrunVerificationRequest request = fixture.Request with
        {
            Instance = fixture.Request.Instance with
            {
                BuildId = "1.5.78.11833",
                SpeedrunTemplateId = template.Id
            },
            Template = template,
            ExpectedBuild = fixture.Request.ExpectedBuild with
            {
                Id = "1.5.78.11833",
                DisplayVersion = "1.5.78.11833"
            },
            FileManifest = fixture.Request.FileManifest with
            {
                Id = "files-race-1578",
                BuildId = "1.5.78.11833",
                Files =
                [
                    .. fixture.Request.FileManifest.Files.Where(rule => rule.AssetId is null),
                    tool
                ]
            },
            LoadNormaliserSeconds = 4
        };

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(request)).Report;

        Assert.Equal(4, report.LoadNormaliserSeconds);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.InvalidToolSelection);
    }

    [Fact]
    public async Task Missing_or_changed_whitelisted_file_is_reported()
    {
        VerificationFixture fixture = await CreateFixtureAsync();
        File.Delete(Path.Combine(fixture.InstanceRoot, "hollow_knight_Data", "globalgamemanagers"));
        await File.WriteAllTextAsync(Path.Combine(fixture.InstanceRoot, "hollow_knight.exe"), "changed");

        SpeedrunVerificationReport report = (await fixture.Verifier.VerifyAndWriteReportAsync(fixture.Request)).Report;

        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.MissingFile);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.HashMismatch);
        Assert.Contains(report.Issues, issue => issue.Code == SpeedrunIssueCode.GameFingerprintMismatch);
    }

    private async Task<VerificationFixture> CreateFixtureAsync()
    {
        string instanceRoot = Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(instanceRoot, "hollow_knight_Data"));
        Directory.CreateDirectory(Path.Combine(instanceRoot, "hollow_knight_Data", "Managed"));
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), "game");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "UnityPlayer.dll"), "unity");
        await File.WriteAllTextAsync(
            Path.Combine(instanceRoot, "hollow_knight_Data", "globalgamemanagers"),
            "managers");
        await File.WriteAllTextAsync(
            Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll"),
            "screen-shake-modifier");
        IReadOnlyList<SpeedrunFileRule> files =
        [
            await RuleAsync(instanceRoot, "hollow_knight.exe", SpeedrunFileKind.Game),
            await RuleAsync(instanceRoot, "UnityPlayer.dll", SpeedrunFileKind.Game),
            await RuleAsync(instanceRoot, "hollow_knight_Data/globalgamemanagers", SpeedrunFileKind.Game)
        ];
        GameBuild build = new()
        {
            Id = "1.2.2.1",
            DisplayVersion = "1.2.2.1",
            DepotId = 367521,
            ManifestId = "648876203478229944",
            ExecutableSha256 = files[0].Sha256,
            UnityPlayerSha256 = files[1].Sha256,
            GlobalGameManagersSha256 = files[2].Sha256
        };
        SpeedrunTemplate template = new()
        {
            Id = "single-run-1221",
            Name = "Single Run 1.2.2.1",
            BuildId = build.Id,
            IsOfficial = true,
            RulesRevision = "rules-abc123",
            FileManifestId = "files-single-run-1221",
            RequiredAssetIds = ["screen-shake-modifier-1221"]
        };
        files =
        [
            .. files,
            await RuleAsync(
                instanceRoot,
                "hollow_knight_Data/Managed/Assembly-CSharp.dll",
                SpeedrunFileKind.Tool,
                "screen-shake-modifier-1221",
                "1.2.2.1")
        ];
        InstanceRecord instance = new()
        {
            Id = "speedrun-copy",
            Name = "Speedrun Copy",
            RootPath = instanceRoot,
            BuildId = build.Id,
            Purpose = InstancePurpose.OfficialSpeedrun,
            ProvisioningMode = InstanceProvisioningMode.FullCopy,
            SpeedrunTemplateId = template.Id,
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
                Files = files
            },
            ReportsDirectory = Path.Combine(root, "reports")
        };
        return new VerificationFixture(
            instanceRoot,
            request,
            new SpeedrunEnvironmentVerifier(new FixedTimeProvider(DateTimeOffset.Parse("2026-07-17T00:00:00Z"))));
    }

    private static async Task<SpeedrunFileRule> RuleAsync(
        string instanceRoot,
        string relativePath,
        SpeedrunFileKind kind,
        string? assetId = null,
        string? assetVersion = null)
    {
        string path = Path.Combine(instanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        await using FileStream stream = File.OpenRead(path);
        return new SpeedrunFileRule
        {
            RelativePath = relativePath,
            Sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream)),
            Kind = kind,
            AssetId = assetId,
            AssetVersion = assetVersion
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
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
