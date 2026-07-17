using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Speedrun;

public sealed class SpeedrunEnvironmentVerifier(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<SpeedrunVerificationResult> VerifyAndWriteReportAsync(
        SpeedrunVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string instanceRoot = Path.GetFullPath(request.Instance.RootPath);
        string reportsDirectory = Path.GetFullPath(request.ReportsDirectory);
        var issues = new List<SpeedrunVerificationIssue>();
        var expectedFiles = BuildFileManifest(instanceRoot, request.FileManifest.Files, issues);
        bool official = request.TemplateSource == SpeedrunTemplateSource.OfficialCatalog && request.Template.IsOfficial;

        ValidateTemplate(request, official, issues);
        ValidateTools(request, official, issues);

        var actualFiles = new List<SpeedrunVerifiedFile>();
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(instanceRoot))
        {
            await ScanInstanceAsync(
                instanceRoot,
                expectedFiles,
                seenFiles,
                actualFiles,
                issues,
                cancellationToken);
        }
        else
        {
            AddIssue(issues, SpeedrunIssueCode.InstanceNotFound, "Instance directory does not exist.");
        }
        foreach ((string relativePath, SpeedrunFileRule _) in expectedFiles)
        {
            if (!seenFiles.Contains(relativePath))
                AddIssue(issues, SpeedrunIssueCode.MissingFile, "Whitelisted file is missing.", relativePath);
        }

        BuildFingerprint fingerprint = CreateFingerprint(actualFiles);
        ValidateFingerprint(request.ExpectedBuild, fingerprint, issues);
        IReadOnlyList<SpeedrunVerifiedTool> tools = CreateToolReports(actualFiles, expectedFiles, issues);
        bool ready = issues.Count == 0;
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
            LoadNormaliserSeconds = request.LoadNormaliserSeconds,
            GeneratedAt = generatedAt,
            IsReadyToLaunch = ready,
            IsOfficiallyVerified = ready && official,
            Files = actualFiles.OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            Tools = tools,
            Issues = issues
        };
        string reportPath = Path.Combine(
            reportsDirectory,
            $"verification-{generatedAt:yyyyMMddTHHmmssfffZ}-{report.Id}.json");
        await AtomicJsonStore.WriteAsync(reportPath, report, cancellationToken);
        return new SpeedrunVerificationResult(report, reportPath);
    }

    private static Dictionary<string, SpeedrunFileRule> BuildFileManifest(
        string instanceRoot,
        IReadOnlyList<SpeedrunFileRule> rules,
        List<SpeedrunVerificationIssue> issues)
    {
        var result = new Dictionary<string, SpeedrunFileRule>(StringComparer.OrdinalIgnoreCase);
        foreach (SpeedrunFileRule rule in rules)
        {
            string relativePath;
            try
            {
                relativePath = NormalizeRelativePath(instanceRoot, rule.RelativePath);
                if (Convert.FromHexString(rule.Sha256).Length != 32)
                    throw new FormatException();
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                AddIssue(
                    issues,
                    SpeedrunIssueCode.InvalidFileManifest,
                    "File manifest contains an invalid path or SHA-256 hash.",
                    rule.RelativePath);
                continue;
            }

            if (!result.TryAdd(relativePath, rule with { RelativePath = relativePath }))
                AddIssue(issues, SpeedrunIssueCode.InvalidFileManifest, "File manifest contains a duplicate path.", relativePath);
            if (rule.AssetId is not null && string.IsNullOrWhiteSpace(rule.AssetVersion))
                AddIssue(issues, SpeedrunIssueCode.InvalidFileManifest, "Tool file has no asset version.", relativePath);
        }
        return result;
    }

    private static void ValidateTemplate(
        SpeedrunVerificationRequest request,
        bool official,
        List<SpeedrunVerificationIssue> issues)
    {
        if (request.TemplateSource == SpeedrunTemplateSource.OfficialCatalog && !request.Template.IsOfficial)
            AddIssue(issues, SpeedrunIssueCode.TemplateNotTrusted, "Catalog template is not marked as official.");
        if (official && OfficialSpeedrunTemplatePolicy.GetViolation(request.Template) is { } violation)
            AddIssue(issues, violation, "Official template constraints do not match a supported speedrun template.");
        if (official && request.Instance.Purpose != InstancePurpose.OfficialSpeedrun)
            AddIssue(issues, SpeedrunIssueCode.InstanceNotDedicated, "Official runs require a dedicated speedrun instance.");
        if (official && request.Instance.ProvisioningMode != InstanceProvisioningMode.FullCopy)
            AddIssue(issues, SpeedrunIssueCode.InstanceNotFullCopy, "Official runs require a full-copy instance.");
        if (official && request.Instance.LoaderId is not null)
            AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "Official runs cannot use a loader.");
        if (official && !string.Equals(request.Instance.SpeedrunTemplateId, request.Template.Id, StringComparison.Ordinal))
            AddIssue(issues, SpeedrunIssueCode.TemplateMismatch, "Instance was not created for the selected template.");
        if (official && (!string.Equals(request.Template.FileManifestId, request.FileManifest.Id, StringComparison.Ordinal) ||
            !string.Equals(request.Template.BuildId, request.FileManifest.BuildId, StringComparison.Ordinal) ||
            !string.Equals(request.Template.RulesRevision, request.FileManifest.RulesRevision, StringComparison.Ordinal)))
            AddIssue(issues, SpeedrunIssueCode.InvalidFileManifest, "File manifest does not match the official template.");
        if (!string.Equals(request.Instance.BuildId, request.ExpectedBuild.Id, StringComparison.Ordinal) ||
            !string.Equals(request.Template.BuildId, request.ExpectedBuild.Id, StringComparison.Ordinal))
            AddIssue(issues, SpeedrunIssueCode.BuildMismatch, "Instance, template, and expected build do not match.");
        if (!string.Equals(
            request.Instance.SpeedrunRulesRevision,
            request.CurrentRulesRevision,
            StringComparison.Ordinal))
            AddIssue(issues, SpeedrunIssueCode.RulesRevisionMismatch, "Speedrun rules revision has changed.");
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
                AddIssue(issues, SpeedrunIssueCode.MissingRequiredTool, $"Required tool is missing: {requiredAsset}.");
        }
        if (official)
        {
            foreach (SpeedrunFileRule extra in request.FileManifest.Files.Where(file =>
                file.AssetId is not null &&
                !request.Template.RequiredAssetIds.Contains(file.AssetId, StringComparer.Ordinal)))
                AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "Tool is not allowed by the official template.", extra.RelativePath);
        }

        bool invalidSelection = request.Template.RequiresLoadNormaliserSelection
            ? request.LoadNormaliserSeconds is not { } seconds ||
                !request.Template.AllowedLoadNormaliserSeconds.Contains(seconds)
            : request.LoadNormaliserSeconds is not null;
        if (invalidSelection)
            AddIssue(issues, SpeedrunIssueCode.InvalidToolSelection, "LoadNormaliser selection is not allowed by the template.");
    }

    private static async Task ScanInstanceAsync(
        string instanceRoot,
        IReadOnlyDictionary<string, SpeedrunFileRule> expectedFiles,
        HashSet<string> seenFiles,
        List<SpeedrunVerifiedFile> actualFiles,
        List<SpeedrunVerificationIssue> issues,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(instanceRoot);
        while (pending.TryPop(out string? directory))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = Path.GetRelativePath(instanceRoot, entry).Replace('\\', '/');
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "Reparse points are not allowed.", relativePath);
                    continue;
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (IsForbiddenPath(relativePath, directory: true))
                        AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "Loader or mod directory is forbidden.", relativePath);
                    pending.Push(entry);
                    continue;
                }
                if (string.Equals(relativePath, ".crystalfly-instance.json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(relativePath, "steam_appid.txt", StringComparison.OrdinalIgnoreCase))
                    continue;

                seenFiles.Add(relativePath);
                expectedFiles.TryGetValue(relativePath, out SpeedrunFileRule? rule);
                string hash = await HashFileAsync(entry, cancellationToken);
                actualFiles.Add(new SpeedrunVerifiedFile
                {
                    RelativePath = relativePath,
                    Sha256 = hash,
                    Kind = rule?.Kind ?? SpeedrunFileKind.Unknown,
                    AssetId = rule?.AssetId
                });
                if (IsForbiddenPath(relativePath, directory: false))
                    AddIssue(issues, SpeedrunIssueCode.ForbiddenFile, "Loader or mod file is forbidden.", relativePath);
                else if (rule is null)
                    AddIssue(issues, SpeedrunIssueCode.UnlistedFile, "File is not present in the speedrun whitelist.", relativePath);
                else if (!string.Equals(hash, rule.Sha256, StringComparison.OrdinalIgnoreCase))
                    AddIssue(issues, SpeedrunIssueCode.HashMismatch, "File hash does not match the whitelist.", relativePath);
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
            ExecutableSha256 = hashes.GetValueOrDefault("hollow_knight.exe", ""),
            UnityPlayerSha256 = hashes.GetValueOrDefault("UnityPlayer.dll"),
            GlobalGameManagersSha256 = hashes.GetValueOrDefault("hollow_knight_Data/globalgamemanagers", "")
        };
    }

    private static void ValidateFingerprint(
        GameBuild expected,
        BuildFingerprint actual,
        List<SpeedrunVerificationIssue> issues)
    {
        if (!string.Equals(expected.ExecutableSha256, actual.ExecutableSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(expected.UnityPlayerSha256, actual.UnityPlayerSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                expected.GlobalGameManagersSha256,
                actual.GlobalGameManagersSha256,
                StringComparison.OrdinalIgnoreCase))
            AddIssue(issues, SpeedrunIssueCode.GameFingerprintMismatch, "Game build fingerprint does not match.");
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

    private static bool IsForbiddenPath(string relativePath, bool directory)
    {
        string normalized = relativePath.Replace('\\', '/').Trim('/').ToLowerInvariant();
        string name = Path.GetFileNameWithoutExtension(normalized);
        return normalized == "bepinex" || normalized.StartsWith("bepinex/", StringComparison.Ordinal) ||
            normalized is "doorstop_config.ini" or "winhttp.dll" ||
            normalized.StartsWith("hollow_knight_data/managed/mods", StringComparison.Ordinal) ||
            (!directory && name.StartsWith("mmhook_", StringComparison.Ordinal)) ||
            (!directory && name is "modding" or "moddingapi") ||
            normalized.Contains("debugmod", StringComparison.Ordinal) ||
            normalized.Contains("speedrunqol", StringComparison.Ordinal) ||
            normalized.Contains("benchwarp", StringComparison.Ordinal) ||
            normalized.Contains("hktimer", StringComparison.Ordinal);
    }

    private static string NormalizeRelativePath(string instanceRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathFullyQualified(normalized) || normalized.Contains(':', StringComparison.Ordinal))
            throw new ArgumentException("File path must be relative.", nameof(relativePath));
        string fullPath = Path.GetFullPath(normalized.Replace('/', Path.DirectorySeparatorChar), instanceRoot);
        string rootPrefix = Path.TrimEndingDirectorySeparator(instanceRoot) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("File path escapes the instance root.", nameof(relativePath));
        return Path.GetRelativePath(instanceRoot, fullPath).Replace('\\', '/');
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
        string? relativePath = null) =>
        issues.Add(new SpeedrunVerificationIssue { Code = code, Message = message, RelativePath = relativePath });
}
