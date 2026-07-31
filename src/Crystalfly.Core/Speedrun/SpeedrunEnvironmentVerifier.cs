using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Speedrun;

public sealed class SpeedrunEnvironmentVerifier(TimeProvider? timeProvider = null)
{
    private static readonly string[] ForbiddenPaths =
    [
        "BepInEx",
        "doorstop_config.ini",
        "winhttp.dll",
        "hollow_knight_Data/Managed/Mods",
        "hollow_knight_Data/Managed/Modding.dll",
        "hollow_knight_Data/Managed/ModdingAPI.dll"
    ];

    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<SpeedrunVerificationResult> VerifyAndWriteReportAsync(
        SpeedrunVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string instanceRoot = Path.GetFullPath(request.Instance.RootPath);
        var issues = new List<SpeedrunVerificationIssue>();
        var expectedFiles = BuildFileManifest(instanceRoot, request.FileManifest.Files, issues);
        bool official = request.TemplateSource == SpeedrunTemplateSource.OfficialCatalog
            && request.Template.IsOfficial;

        ValidateTemplate(request, official, issues);
        ValidateTools(request, official, issues);
        if (request.HasTransactionIssue)
        {
            AddIssue(issues, SpeedrunIssueCode.TransactionNeedsAttention, "A file transaction needs attention.");
        }
        if (request.HasLocalLowIssue)
        {
            AddIssue(issues, SpeedrunIssueCode.LocalLowNeedsAttention, "LocalLow state needs attention.");
        }

        var actualFiles = new List<SpeedrunVerifiedFile>();
        RuntimePatchesConfiguration? configuration = null;
        if (!Directory.Exists(instanceRoot))
        {
            AddIssue(issues, SpeedrunIssueCode.InstanceNotFound, "Instance directory does not exist.");
        }
        else
        {
            await VerifyCoreFileAsync(
                instanceRoot,
                "hollow_knight.exe",
                request.ExpectedBuild.ExecutableSha256,
                required: true,
                actualFiles,
                issues,
                cancellationToken);
            await VerifyCoreFileAsync(
                instanceRoot,
                "hollow_knight_Data/globalgamemanagers",
                request.ExpectedBuild.GlobalGameManagersSha256,
                required: true,
                actualFiles,
                issues,
                cancellationToken);
            await VerifyCoreFileAsync(
                instanceRoot,
                "UnityPlayer.dll",
                request.ExpectedBuild.UnityPlayerSha256,
                required: request.ExpectedBuild.UnityPlayerSha256 is not null,
                actualFiles,
                issues,
                cancellationToken);

            foreach ((string relativePath, SpeedrunFileRule rule) in expectedFiles)
            {
                await VerifyManifestFileAsync(
                    instanceRoot,
                    relativePath,
                    rule,
                    actualFiles,
                    issues,
                    cancellationToken);
            }
            ValidateForbiddenMarkers(instanceRoot, issues);
            configuration = await ReadConfigurationAsync(request, issues, cancellationToken);
        }

        BuildFingerprint fingerprint = CreateFingerprint(actualFiles);
        ValidateFingerprint(request.ExpectedBuild, fingerprint, issues);
        IReadOnlyList<SpeedrunVerifiedTool> tools = CreateToolReports(actualFiles, expectedFiles, issues);
        bool ready = issues.All(static issue => issue.Severity != SpeedrunIssueSeverity.EnvironmentError);
        DateTimeOffset generatedAt = timeProvider.GetUtcNow();
        var report = new SpeedrunVerificationReport
        {
            Id = Guid.NewGuid().ToString("N"),
            InstanceId = request.Instance.Id,
            TemplateId = request.Template.Id,
            TemplateSource = request.TemplateSource,
            TemplateRulesRevision = request.Instance.SpeedrunRulesRevision ?? string.Empty,
            CurrentRulesRevision = request.CurrentRulesRevision,
            FileManifestId = request.FileManifest.Id,
            ExpectedBuildId = request.ExpectedBuild.Id,
            ActualBuildFingerprint = fingerprint,
            GeneratedAt = generatedAt,
            IsReadyToLaunch = ready,
            IsOfficiallyVerified = ready && official,
            RuntimePatchesConfiguration = configuration,
            Files = actualFiles.OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            Tools = tools,
            Issues = issues
        };
        string reportPath = Path.Combine(
            Path.GetFullPath(request.ReportsDirectory),
            $"verification-{generatedAt:yyyyMMddTHHmmssfffZ}-{report.Id}.json");
        await AtomicJsonStore.WriteAsync(reportPath, report, cancellationToken);
        return new SpeedrunVerificationResult(report, reportPath);
    }

    private static async Task<RuntimePatchesConfiguration?> ReadConfigurationAsync(
        SpeedrunVerificationRequest request,
        List<SpeedrunVerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            RuntimePatchesConfiguration configuration = await RuntimePatchesConfiguration.ReadAsync(
                request.RuntimePatchesConfigurationPath,
                cancellationToken);
            if (RuntimePatchesPolicy.Normalize(request.ExpectedBuild.Id, configuration) != configuration)
            {
                throw new InvalidDataException("Configuration enables an unsupported RuntimePatches feature.");
            }
            AddRuleWarnings(configuration, issues);
            return configuration;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException)
        {
            AddIssue(
                issues,
                SpeedrunIssueCode.InvalidRuntimePatchesConfiguration,
                $"RuntimePatches configuration is invalid: {exception.Message}");
            return null;
        }
    }

    private static void AddRuleWarnings(
        RuntimePatchesConfiguration configuration,
        List<SpeedrunVerificationIssue> issues)
    {
        if (configuration.ScreenShakeModifier)
        {
            AddIssue(
                issues,
                SpeedrunIssueCode.ScreenShakeModifierRuleWarning,
                "Confirm that ScreenShakeModifier is permitted for the selected category.",
                severity: SpeedrunIssueSeverity.RuleWarning);
        }
        if (configuration.MiniSaveStates)
        {
            AddIssue(
                issues,
                SpeedrunIssueCode.MiniSaveStatesRuleWarning,
                "MiniSaveStates is only for explicitly permitted individual-level categories and must not be loaded during a run.",
                severity: SpeedrunIssueSeverity.RuleWarning);
        }
        if (configuration.FasterIntroSkip)
        {
            AddIssue(
                issues,
                SpeedrunIssueCode.FasterIntroSkipRuleWarning,
                "FasterIntroSkip must be disabled for categories that time the opening cinematic.",
                severity: SpeedrunIssueSeverity.RuleWarning);
        }
        if (configuration.TextMasher)
        {
            AddIssue(
                issues,
                SpeedrunIssueCode.TextMasherRuleWarning,
                "Confirm that TextMasher is permitted for the selected category.",
                severity: SpeedrunIssueSeverity.RuleWarning);
        }
    }

    private static Dictionary<string, SpeedrunFileRule> BuildFileManifest(
        string instanceRoot,
        IReadOnlyList<SpeedrunFileRule> rules,
        List<SpeedrunVerificationIssue> issues)
    {
        var result = new Dictionary<string, SpeedrunFileRule>(StringComparer.OrdinalIgnoreCase);
        foreach (SpeedrunFileRule rule in rules)
        {
            try
            {
                string relativePath = NormalizeRelativePath(instanceRoot, rule.RelativePath);
                if (Convert.FromHexString(rule.Sha256).Length != 32)
                {
                    throw new FormatException();
                }
                if (!result.TryAdd(relativePath, rule with { RelativePath = relativePath }))
                {
                    AddIssue(issues, SpeedrunIssueCode.InvalidFileManifest, "File manifest contains a duplicate path.", relativePath);
                }
                if (rule.AssetId is not null && string.IsNullOrWhiteSpace(rule.AssetVersion))
                {
                    AddIssue(issues, SpeedrunIssueCode.InvalidFileManifest, "Tool file has no asset version.", relativePath);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                AddIssue(
                    issues,
                    SpeedrunIssueCode.InvalidFileManifest,
                    "File manifest contains an invalid path or SHA-256 hash.",
                    rule.RelativePath);
            }
        }
        return result;
    }

    private static void ValidateTemplate(
        SpeedrunVerificationRequest request,
        bool official,
        List<SpeedrunVerificationIssue> issues)
    {
        if (request.TemplateSource == SpeedrunTemplateSource.OfficialCatalog && !request.Template.IsOfficial)
        {
            AddIssue(issues, SpeedrunIssueCode.TemplateNotTrusted, "Catalog template is not marked as official.");
        }
        if (official && OfficialSpeedrunTemplatePolicy.GetViolation(request.Template) is { } violation)
        {
            AddIssue(issues, violation, "Official template is not a supported RuntimePatches template.");
        }
        if (official && request.Instance.Purpose != InstancePurpose.OfficialSpeedrun)
        {
            AddIssue(issues, SpeedrunIssueCode.InstanceNotDedicated, "Official runs require a dedicated speedrun instance.");
        }
        if (official && request.Instance.ProvisioningMode != InstanceProvisioningMode.FullCopy)
        {
            AddIssue(issues, SpeedrunIssueCode.InstanceNotFullCopy, "Official runs require a full-copy instance.");
        }
        if (official && request.Instance.LoaderId is not null)
        {
            AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "RuntimePatches environments cannot use a loader.");
        }
        if (official && !string.Equals(request.Instance.SpeedrunTemplateId, request.Template.Id, StringComparison.Ordinal))
        {
            AddIssue(issues, SpeedrunIssueCode.TemplateMismatch, "Instance was not created for the selected template.");
        }
        if (official && (!string.Equals(request.Template.FileManifestId, request.FileManifest.Id, StringComparison.Ordinal)
            || !string.Equals(request.Template.BuildId, request.FileManifest.BuildId, StringComparison.Ordinal)
            || !string.Equals(request.Template.RulesRevision, request.FileManifest.RulesRevision, StringComparison.Ordinal)))
        {
            AddIssue(issues, SpeedrunIssueCode.InvalidFileManifest, "File manifest does not match the official template.");
        }
        if (!string.Equals(request.Instance.BuildId, request.ExpectedBuild.Id, StringComparison.Ordinal)
            || !string.Equals(request.Template.BuildId, request.ExpectedBuild.Id, StringComparison.Ordinal))
        {
            AddIssue(issues, SpeedrunIssueCode.BuildMismatch, "Instance, template, and expected build do not match.");
        }
        if (!string.Equals(request.Instance.SpeedrunRulesRevision, request.CurrentRulesRevision, StringComparison.Ordinal))
        {
            AddIssue(issues, SpeedrunIssueCode.RulesRevisionMismatch, "Speedrun rules revision has changed.");
        }
    }

    private static void ValidateTools(
        SpeedrunVerificationRequest request,
        bool official,
        List<SpeedrunVerificationIssue> issues)
    {
        string[] providedAssets = request.FileManifest.Files
            .Select(static file => file.AssetId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(static id => id!)
            .ToArray();
        foreach (string requiredAsset in request.Template.RequiredAssetIds)
        {
            if (!providedAssets.Contains(requiredAsset, StringComparer.Ordinal))
            {
                AddIssue(issues, SpeedrunIssueCode.MissingRequiredTool, $"Required tool is missing: {requiredAsset}.");
            }
        }
        if (official && request.FileManifest.Files.Any(file =>
            file.AssetId is not null
            && !request.Template.RequiredAssetIds.Contains(file.AssetId, StringComparer.Ordinal)))
        {
            AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "The manifest contains an unsupported tool.");
        }
        if (request.LoadNormaliserSeconds is not null)
        {
            AddIssue(issues, SpeedrunIssueCode.InvalidToolSelection, "RuntimePatches templates do not support LoadNormaliser.");
        }
    }

    private static async Task VerifyCoreFileAsync(
        string instanceRoot,
        string relativePath,
        string? expectedSha256,
        bool required,
        List<SpeedrunVerifiedFile> actualFiles,
        List<SpeedrunVerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        string path = ResolveUnderRoot(instanceRoot, relativePath);
        if (!File.Exists(path))
        {
            if (required)
            {
                AddIssue(issues, SpeedrunIssueCode.MissingFile, "Required game file is missing.", relativePath);
            }
            return;
        }
        string sha256 = await HashFileAsync(path, cancellationToken);
        actualFiles.Add(new SpeedrunVerifiedFile
        {
            RelativePath = relativePath,
            Sha256 = sha256,
            Kind = SpeedrunFileKind.Game
        });
        if (expectedSha256 is not null
            && !string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(issues, SpeedrunIssueCode.HashMismatch, "Game file hash does not match.", relativePath);
        }
    }

    private static async Task VerifyManifestFileAsync(
        string instanceRoot,
        string relativePath,
        SpeedrunFileRule rule,
        List<SpeedrunVerifiedFile> actualFiles,
        List<SpeedrunVerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        string path = ResolveUnderRoot(instanceRoot, relativePath);
        if (!File.Exists(path))
        {
            AddIssue(issues, SpeedrunIssueCode.MissingFile, "RuntimePatches file is missing.", relativePath);
            return;
        }
        string sha256 = await HashFileAsync(path, cancellationToken);
        actualFiles.Add(new SpeedrunVerifiedFile
        {
            RelativePath = relativePath,
            Sha256 = sha256,
            Kind = rule.Kind,
            AssetId = rule.AssetId
        });
        if (!string.Equals(sha256, rule.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(issues, SpeedrunIssueCode.HashMismatch, "RuntimePatches DLL hash does not match.", relativePath);
        }
    }

    private static void ValidateForbiddenMarkers(
        string instanceRoot,
        List<SpeedrunVerificationIssue> issues)
    {
        foreach (string relativePath in ForbiddenPaths)
        {
            string path = ResolveUnderRoot(instanceRoot, relativePath);
            if (File.Exists(path) || Directory.Exists(path))
            {
                AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "Loader or Mod marker is present.", relativePath);
            }
        }
    }

    private static BuildFingerprint CreateFingerprint(IReadOnlyList<SpeedrunVerifiedFile> files)
    {
        var hashes = files.ToDictionary(
            static file => file.RelativePath,
            static file => file.Sha256,
            StringComparer.OrdinalIgnoreCase);
        return new BuildFingerprint
        {
            ExecutableSha256 = hashes.GetValueOrDefault("hollow_knight.exe", string.Empty),
            UnityPlayerSha256 = hashes.GetValueOrDefault("UnityPlayer.dll"),
            GlobalGameManagersSha256 = hashes.GetValueOrDefault("hollow_knight_Data/globalgamemanagers", string.Empty)
        };
    }

    private static void ValidateFingerprint(
        GameBuild expected,
        BuildFingerprint actual,
        List<SpeedrunVerificationIssue> issues)
    {
        if (!string.Equals(expected.ExecutableSha256, actual.ExecutableSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(expected.UnityPlayerSha256, actual.UnityPlayerSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(expected.GlobalGameManagersSha256, actual.GlobalGameManagersSha256, StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(issues, SpeedrunIssueCode.GameFingerprintMismatch, "Game build fingerprint does not match.");
        }
    }

    private static IReadOnlyList<SpeedrunVerifiedTool> CreateToolReports(
        IReadOnlyList<SpeedrunVerifiedFile> files,
        IReadOnlyDictionary<string, SpeedrunFileRule> rules,
        List<SpeedrunVerificationIssue> issues)
    {
        var result = new List<SpeedrunVerifiedTool>();
        foreach (IGrouping<string, SpeedrunVerifiedFile> group in files
            .Where(static file => file.AssetId is not null)
            .GroupBy(static file => file.AssetId!, StringComparer.Ordinal))
        {
            string[] versions = group
                .Select(file => rules[file.RelativePath].AssetVersion)
                .Where(static version => version is not null)
                .Distinct(StringComparer.Ordinal)
                .Select(static version => version!)
                .ToArray();
            if (versions.Length != 1)
            {
                AddIssue(issues, SpeedrunIssueCode.InvalidFileManifest, $"Tool has inconsistent versions: {group.Key}.");
                continue;
            }
            result.Add(new SpeedrunVerifiedTool
            {
                AssetId = group.Key,
                Version = versions[0],
                Files = group.OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray()
            });
        }
        return result.OrderBy(static tool => tool.AssetId, StringComparer.Ordinal).ToArray();
    }

    private static string NormalizeRelativePath(string instanceRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathFullyQualified(normalized) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("File path must be relative.", nameof(relativePath));
        }
        return Path.GetRelativePath(instanceRoot, ResolveUnderRoot(instanceRoot, normalized)).Replace('\\', '/');
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("File path escapes the instance root.", nameof(relativePath));
        }
        return fullPath;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void AddIssue(
        List<SpeedrunVerificationIssue> issues,
        SpeedrunIssueCode code,
        string message,
        string? relativePath = null,
        SpeedrunIssueSeverity severity = SpeedrunIssueSeverity.EnvironmentError) =>
        issues.Add(new SpeedrunVerificationIssue
        {
            Code = code,
            Severity = severity,
            Message = message,
            RelativePath = relativePath
        });
}
