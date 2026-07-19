using System.IO.Compression;
using Crystalfly.Core.Models;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Speedrun;

public sealed record SpeedrunProvisioningRequest
{
    public required GameCatalog Catalog { get; init; }

    public required string TemplateId { get; init; }

    public required string InstanceRoot { get; init; }

    public required string TransactionRoot { get; init; }

    public required string PackageCacheRoot { get; init; }

    public IReadOnlyDictionary<string, string> LocalPackagePaths { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public int? LoadNormaliserSeconds { get; init; }

    public HttpClient? HttpClient { get; init; }
}

public sealed class SpeedrunRecoveryRequiredException(string message, Exception innerException)
    : IOException(message, innerException);

public sealed class SpeedrunEnvironmentProvisioner
{
    public static async Task<bool> RequiresManualRecoveryAsync(
        string transactionRoot,
        string instanceRoot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recoveries = await FileTransaction.RecoverPendingAsync(
                transactionRoot,
                cancellationToken);
            return recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention
                && PathEquals(recovery.RootPath, instanceRoot));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return true;
        }
    }

    public async Task<TransactionJournal> ProvisionAsync(
        SpeedrunProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var template = SingleById(request.Catalog.SpeedrunTemplates, request.TemplateId, "speedrun template");
        var manifest = SingleById(
            request.Catalog.SpeedrunFileManifests,
            template.FileManifestId,
            "speedrun file manifest");
        ValidateTemplate(template, manifest, request.LoadNormaliserSeconds);

        var instanceRoot = ExistingDirectory(request.InstanceRoot, nameof(request.InstanceRoot));
        var transactionRoot = Path.GetFullPath(request.TransactionRoot);
        var cacheRoot = Path.GetFullPath(request.PackageCacheRoot);
        Directory.CreateDirectory(transactionRoot);
        Directory.CreateDirectory(cacheRoot);
        var workspace = Path.Combine(transactionRoot, $".speedrun-{Guid.NewGuid():N}");
        var staging = Path.Combine(workspace, "staging");
        Directory.CreateDirectory(staging);
        try
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetId in template.RequiredAssetIds.Distinct(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var asset = SingleById(request.Catalog.SpeedrunAssets, assetId, "speedrun asset");
                if (!asset.SupportedBuildIds.Contains(template.BuildId, StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Speedrun asset '{asset.Id}' does not support build '{template.BuildId}'.");
                }
                var rules = manifest.Files.Where(rule =>
                    string.Equals(rule.AssetId, asset.Id, StringComparison.Ordinal)).ToArray();
                if (rules.Length == 0)
                {
                    throw new InvalidDataException(
                        $"Speedrun file manifest has no targets for required asset '{asset.Id}'.");
                }
                if (rules.Any(rule => !string.Equals(rule.AssetVersion, asset.Version, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException(
                        $"Speedrun file manifest version does not match asset '{asset.Id}'.");
                }

                string packagePath = await AcquireAsync(
                    asset,
                    request,
                    transactionRoot,
                    cacheRoot,
                    cancellationToken);
                foreach (var rule in rules)
                {
                    string relativePath = NormalizeRelativePath(instanceRoot, rule.RelativePath);
                    if (!targets.Add(relativePath))
                    {
                        throw new InvalidDataException($"Duplicate speedrun target: '{relativePath}'.");
                    }
                    string target = ResolveUnderRoot(staging, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await CopyAssetAsync(
                        asset,
                        template.BuildId,
                        request.LoadNormaliserSeconds,
                        packagePath,
                        target,
                        cancellationToken);
                }
            }

            try
            {
                return await FileTransaction.ApplyDirectoryAsync(
                    staging,
                    instanceRoot,
                    transactionRoot,
                    "provision-speedrun-environment",
                    cancellationToken);
            }
            catch (Exception failure)
            {
                await ThrowIfRecoveryRequiredAsync(
                    transactionRoot,
                    instanceRoot,
                    failure);
                throw;
            }
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static async Task ThrowIfRecoveryRequiredAsync(
        string transactionRoot,
        string instanceRoot,
        Exception failure)
    {
        if (await RequiresManualRecoveryAsync(
            transactionRoot,
            instanceRoot,
            CancellationToken.None))
        {
            throw new SpeedrunRecoveryRequiredException(
                "Speedrun deployment needs manual recovery; preserve the instance and transaction journal.",
                failure);
        }
    }

    private static async Task<string> AcquireAsync(
        SpeedrunAsset asset,
        SpeedrunProvisioningRequest request,
        string transactionRoot,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        if (request.LocalPackagePaths.TryGetValue(asset.Id, out string? localPath))
        {
            return await PackageInstaller.AcquireVerifiedFileFromFileAsync(
                localPath,
                transactionRoot,
                asset.SizeBytes,
                asset.Sha256,
                cacheRoot,
                cancellationToken);
        }
        return await PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri(asset.DownloadUrl),
            transactionRoot,
            asset.SizeBytes,
            asset.Sha256,
            cacheRoot,
            request.HttpClient,
            cancellationToken: cancellationToken);
    }

    private static async Task CopyAssetAsync(
        SpeedrunAsset asset,
        string buildId,
        int? loadNormaliserSeconds,
        string packagePath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        if (string.Equals(asset.Id, "screen-shake-modifier-1221", StringComparison.Ordinal))
        {
            await CopyFileAsync(packagePath, targetPath, cancellationToken);
            return;
        }
        if (!string.Equals(asset.Id, "load-normaliser-1.1", StringComparison.Ordinal)
            || loadNormaliserSeconds is null)
        {
            throw new InvalidDataException($"Unsupported speedrun asset: '{asset.Id}'.");
        }

        string buildFolder = buildId switch
        {
            "1.4.3.2" => "1432 LoadNormaliser",
            "1.5.78.11833" => "1578 LoadNormaliser",
            _ => throw new InvalidDataException(
                $"LoadNormaliser does not support build '{buildId}'.")
        };
        string entryName =
            $"{buildFolder}/Assembly-CSharp loadNormaliser{loadNormaliserSeconds.Value}s UI.dll";
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Where(entry =>
            string.Equals(entry.FullName.Replace('\\', '/'), entryName, StringComparison.Ordinal)).ToArray();
        if (entries.Length != 1 || entries[0].Length <= 0)
        {
            throw new InvalidDataException(
                $"LoadNormaliser package does not contain exactly one '{entryName}' entry.");
        }
        await using var source = entries[0].Open();
        await using var destination = new FileStream(
            targetPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            targetPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private static void ValidateTemplate(
        SpeedrunTemplate template,
        SpeedrunFileManifest manifest,
        int? loadNormaliserSeconds)
    {
        if (!string.Equals(template.BuildId, manifest.BuildId, StringComparison.Ordinal)
            || !string.Equals(template.RulesRevision, manifest.RulesRevision, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Speedrun template and file manifest do not match.");
        }
        bool invalidSelection = template.RequiresLoadNormaliserSelection
            ? loadNormaliserSeconds is not { } seconds
                || !template.AllowedLoadNormaliserSeconds.Contains(seconds)
            : loadNormaliserSeconds is not null;
        if (invalidSelection)
        {
            throw new InvalidDataException("LoadNormaliser selection is not allowed by the template.");
        }
    }

    private static T SingleById<T>(IEnumerable<T> items, string id, string description)
        where T : class
    {
        var matches = items.Where(item => string.Equals(
            item switch
            {
                SpeedrunTemplate template => template.Id,
                SpeedrunAsset asset => asset.Id,
                SpeedrunFileManifest manifest => manifest.Id,
                _ => throw new InvalidOperationException($"Unsupported catalog type: {typeof(T).Name}.")
            },
            id,
            StringComparison.Ordinal)).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Catalog must contain exactly one {description} with ID '{id}'.");
    }

    private static string ExistingDirectory(string path, string parameterName)
    {
        string fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException($"{parameterName} does not exist: '{fullPath}'.");
    }

    private static string NormalizeRelativePath(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathFullyQualified(relativePath) || relativePath.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Speedrun target must be relative: '{relativePath}'.");
        }
        string fullPath = ResolveUnderRoot(root, relativePath.Replace('\\', '/'));
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        string fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes its root: '{relativePath}'.");
        }
        return fullPath;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
