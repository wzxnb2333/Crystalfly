using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Loaders;

public sealed class LoaderManager
{
    private readonly string _instanceRoot;
    private readonly string _transactionRoot;
    private readonly string _receiptPath;
    private readonly string _backupRoot;

    public LoaderManager(string instanceRoot, string transactionRoot, string receiptPath)
    {
        _instanceRoot = Path.GetFullPath(instanceRoot);
        _transactionRoot = Path.GetFullPath(transactionRoot);
        _receiptPath = Path.GetFullPath(receiptPath);
        _backupRoot = Path.Combine(Path.GetDirectoryName(_receiptPath)!, "loader-backups");
    }

    public async Task<LoaderState> GetStateAsync(CancellationToken cancellationToken = default) =>
        await LoaderStateDetector.DetectAsync(
            _instanceRoot, await GetReceiptAsync(cancellationToken), cancellationToken);

    public async Task<InstalledPackageReceipt?> GetReceiptAsync(CancellationToken cancellationToken = default) =>
        File.Exists(_receiptPath)
            ? await AtomicJsonStore.ReadAsync<InstalledPackageReceipt>(_receiptPath, cancellationToken)
            : null;

    public async Task<InstalledPackageReceipt> InstallFromFileAsync(
        LoaderManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (await GetStateAsync(cancellationToken) != LoaderState.Vanilla)
        {
            throw new InvalidOperationException("Loader installation requires a vanilla instance.");
        }
        return await ApplyPackageAsync(manifest, packagePath, null, null, "install-loader", cancellationToken);
    }

    public async Task<InstalledPackageReceipt> InstallFromUriAsync(
        LoaderManifest manifest,
        CancellationToken cancellationToken = default)
    {
        if (await GetStateAsync(cancellationToken) != LoaderState.Vanilla)
        {
            throw new InvalidOperationException("Loader installation requires a vanilla instance.");
        }
        return await ApplyPackageAsync(
            manifest, null, new Uri(manifest.DownloadUrl), null, "install-loader", cancellationToken);
    }

    public async Task<InstalledPackageReceipt> SwitchFromFileAsync(
        LoaderManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var receipt = await RequireVerifiedReceiptAsync(cancellationToken);
        return await ApplyPackageAsync(manifest, packagePath, null, receipt, "switch-loader", cancellationToken);
    }

    public async Task<InstalledPackageReceipt> SwitchFromUriAsync(
        LoaderManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var receipt = await RequireVerifiedReceiptAsync(cancellationToken);
        return await ApplyPackageAsync(
            manifest, null, new Uri(manifest.DownloadUrl), receipt, "switch-loader", cancellationToken);
    }

    public async Task<InstalledPackageReceipt> RepairFromFileAsync(
        LoaderManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var receipt = await GetRepairReceiptAsync(manifest, cancellationToken);
        return await ApplyPackageAsync(manifest, packagePath, null, receipt, "repair-loader", cancellationToken);
    }

    public async Task<InstalledPackageReceipt> RepairFromUriAsync(
        LoaderManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var receipt = await GetRepairReceiptAsync(manifest, cancellationToken);
        return await ApplyPackageAsync(
            manifest, null, new Uri(manifest.DownloadUrl), receipt, "repair-loader", cancellationToken);
    }

    public async Task UninstallAsync(CancellationToken cancellationToken = default)
    {
        var receipt = await RequireVerifiedReceiptAsync(cancellationToken);
        EnsureBackupRoot(receipt.BackupRoot);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".loader-uninstall-");
        var staging = workspace.CreateDirectory("staging");
        var removals = RestoreOriginals(receipt, staging);
        await FileTransaction.ReplaceDirectoryAsync(
            staging, _instanceRoot, _transactionRoot, "uninstall-loader", removals, cancellationToken);
        File.Delete(_receiptPath);
        DeleteDirectory(receipt.BackupRoot);
        RemoveEmptyLoaderDirectories();
    }

    private async Task<InstalledPackageReceipt> ApplyPackageAsync(
        LoaderManifest manifest,
        string? packagePath,
        Uri? packageUri,
        InstalledPackageReceipt? previousReceipt,
        string operation,
        CancellationToken cancellationToken)
    {
        var size = manifest.SizeBytes
            ?? throw new InvalidDataException($"Loader '{manifest.Id}' does not declare its package size.");
        if (previousReceipt is not null)
        {
            EnsureBackupRoot(previousReceipt.BackupRoot);
        }
        var loaderState = GetLoaderState(manifest.Id);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".loader-install-");
        var extracted = workspace.CreateDirectory("extracted");
        var packageTransactions = workspace.CreateDirectory("package-transactions");
        if (packagePath is not null)
        {
            await PackageInstaller.InstallFromFileAsync(
                packagePath, extracted, packageTransactions, size, manifest.Sha256, cancellationToken);
        }
        else if (packageUri is not null)
        {
            await PackageInstaller.InstallFromUriAsync(
                packageUri, extracted, packageTransactions, size, manifest.Sha256, cancellationToken);
        }
        else
        {
            throw new ArgumentException("A loader package path or URI is required.");
        }

        var staging = workspace.CreateDirectory("staging");
        var prefix = loaderState == LoaderState.ModdingApi
            ? "hollow_knight_Data/Managed"
            : string.Empty;
        CopyTree(extracted, ResolveUnderRoot(staging, prefix));
        var stagedFiles = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Normalize(Path.GetRelativePath(staging, path)),
                StringComparer.OrdinalIgnoreCase);
        if (stagedFiles.Count == 0)
        {
            throw new InvalidDataException("Loader package does not contain files.");
        }

        var newBackupRoot = Path.Combine(_backupRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(newBackupRoot);
        var filesApplied = false;
        try
        {
            var newFiles = await BuildReceiptFilesAsync(
                stagedFiles, previousReceipt, newBackupRoot, cancellationToken);
            var removals = previousReceipt is null
                ? []
                : RestoreObsoleteOriginals(previousReceipt, stagedFiles.Keys, staging);
            await FileTransaction.ReplaceDirectoryAsync(
                staging, _instanceRoot, _transactionRoot, operation, removals, cancellationToken);
            filesApplied = true;

            var receipt = new InstalledPackageReceipt
            {
                PackageId = manifest.Id,
                LoaderState = loaderState,
                BackupRoot = newBackupRoot,
                Files = newFiles
            };
            await AtomicJsonStore.WriteAsync(_receiptPath, receipt, cancellationToken);
            if (previousReceipt is not null
                && !PathEquals(previousReceipt.BackupRoot, newBackupRoot))
            {
                DeleteDirectory(previousReceipt.BackupRoot);
            }
            return receipt;
        }
        catch
        {
            if (!filesApplied)
            {
                DeleteDirectory(newBackupRoot);
            }
            throw;
        }
    }

    private async Task<IReadOnlyList<InstalledFileReceipt>> BuildReceiptFilesAsync(
        IReadOnlyDictionary<string, string> stagedFiles,
        InstalledPackageReceipt? previousReceipt,
        string newBackupRoot,
        CancellationToken cancellationToken)
    {
        var previous = previousReceipt?.Files.ToDictionary(
            file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, InstalledFileReceipt>(StringComparer.OrdinalIgnoreCase);
        var result = new List<InstalledFileReceipt>(stagedFiles.Count);
        foreach (var staged in stagedFiles)
        {
            var appliedHash = await HashFileAsync(staged.Value, cancellationToken);
            string? originalHash;
            string? backupRelativePath;
            if (previous.TryGetValue(staged.Key, out var old))
            {
                originalHash = old.OriginalSha256;
                backupRelativePath = old.BackupRelativePath;
                if (backupRelativePath is not null)
                {
                    CopyBackup(previousReceipt!.BackupRoot, newBackupRoot, backupRelativePath);
                }
            }
            else
            {
                var targetPath = ResolveUnderRoot(_instanceRoot, staged.Key);
                if (File.Exists(targetPath))
                {
                    originalHash = await HashFileAsync(targetPath, cancellationToken);
                    backupRelativePath = staged.Key;
                    CopyFile(targetPath, ResolveUnderRoot(newBackupRoot, backupRelativePath));
                }
                else
                {
                    originalHash = null;
                    backupRelativePath = null;
                }
            }
            result.Add(new InstalledFileReceipt
            {
                RelativePath = staged.Key,
                Sha256 = appliedHash,
                OriginalSha256 = originalHash,
                BackupRelativePath = backupRelativePath
            });
        }
        return result;
    }

    private IReadOnlyList<string> RestoreObsoleteOriginals(
        InstalledPackageReceipt receipt,
        IEnumerable<string> stagedRelativePaths,
        string stagingRoot)
    {
        var staged = stagedRelativePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removals = new List<string>();
        foreach (var old in receipt.Files.Where(file => !staged.Contains(file.RelativePath)))
        {
            if (old.BackupRelativePath is null)
            {
                if (File.Exists(ResolveUnderRoot(_instanceRoot, old.RelativePath)))
                {
                    removals.Add(old.RelativePath);
                }
            }
            else
            {
                CopyFile(
                    ResolveBackup(receipt, old.BackupRelativePath),
                    ResolveUnderRoot(stagingRoot, old.RelativePath));
            }
        }
        return removals;
    }

    private IReadOnlyList<string> RestoreOriginals(InstalledPackageReceipt receipt, string stagingRoot)
    {
        var removals = new List<string>();
        foreach (var file in receipt.Files)
        {
            if (file.BackupRelativePath is null)
            {
                removals.Add(file.RelativePath);
            }
            else
            {
                CopyFile(
                    ResolveBackup(receipt, file.BackupRelativePath),
                    ResolveUnderRoot(stagingRoot, file.RelativePath));
            }
        }
        return removals;
    }

    private async Task<InstalledPackageReceipt> RequireVerifiedReceiptAsync(CancellationToken cancellationToken)
    {
        var receipt = await GetReceiptAsync(cancellationToken)
            ?? throw new InvalidOperationException("There is no installed loader receipt.");
        var state = await LoaderStateDetector.DetectAsync(_instanceRoot, receipt, cancellationToken);
        if (state != receipt.LoaderState)
        {
            throw new InvalidOperationException($"Loader files are {state}; repair the instance before changing them.");
        }
        return receipt;
    }

    private async Task<InstalledPackageReceipt> GetRepairReceiptAsync(
        LoaderManifest manifest,
        CancellationToken cancellationToken)
    {
        var receipt = await GetReceiptAsync(cancellationToken)
            ?? throw new InvalidOperationException("There is no loader receipt to repair.");
        var state = await LoaderStateDetector.DetectAsync(_instanceRoot, receipt, cancellationToken);
        if (state == LoaderState.Conflict || !string.Equals(receipt.PackageId, manifest.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only the receipted loader can be repaired, and conflicts must be resolved first.");
        }
        return receipt;
    }

    private string ResolveBackup(InstalledPackageReceipt receipt, string relativePath)
    {
        EnsureBackupRoot(receipt.BackupRoot);
        var path = ResolveUnderRoot(receipt.BackupRoot, relativePath);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Loader backup is missing: '{relativePath}'.");
        }
        return path;
    }

    private void CopyBackup(string sourceRoot, string targetRoot, string relativePath)
    {
        EnsureBackupRoot(sourceRoot);
        CopyFile(ResolveUnderRoot(sourceRoot, relativePath), ResolveUnderRoot(targetRoot, relativePath));
    }

    private void EnsureBackupRoot(string path)
    {
        var fullRoot = Path.GetFullPath(_backupRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Loader receipt backup path escapes the state directory.");
        }
    }

    private void RemoveEmptyLoaderDirectories()
    {
        RemoveEmptyTree(Path.Combine(_instanceRoot, "BepInEx"));
        RemoveEmptyTree(Path.Combine(_instanceRoot, "hollow_knight_Data", "Managed", "Mods"));
    }

    private static void RemoveEmptyTree(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        if (!Directory.EnumerateFileSystemEntries(root).Any())
        {
            Directory.Delete(root);
        }
    }

    private static LoaderState GetLoaderState(string id) =>
        id.StartsWith("modding-api-", StringComparison.OrdinalIgnoreCase)
            ? LoaderState.ModdingApi
            : id.StartsWith("bepinex-", StringComparison.OrdinalIgnoreCase)
                ? LoaderState.BepInEx
                : throw new InvalidDataException($"Unknown loader family: '{id}'.");

    private static void CopyTree(string sourceRoot, string targetRoot)
    {
        Directory.CreateDirectory(targetRoot);
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            CopyFile(file, Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, file)));
        }
    }

    private static void CopyFile(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: false);
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (relativePath.Length == 0)
        {
            return fullRoot;
        }
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes its root: '{relativePath}'.");
        }
        return path;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory(string root, string prefix)
        {
            Directory.CreateDirectory(root);
            Path = System.IO.Path.Combine(root, $"{prefix}{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateDirectory(string name)
        {
            var path = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose() => DeleteDirectory(Path);
    }
}
