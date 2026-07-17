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
    private readonly string? _packageCacheRoot;
    private readonly HttpClient? _httpClient;

    public LoaderManager(
        string instanceRoot,
        string transactionRoot,
        string receiptPath,
        string? packageCacheRoot = null,
        HttpClient? httpClient = null)
    {
        _instanceRoot = Path.GetFullPath(instanceRoot);
        _transactionRoot = Path.GetFullPath(transactionRoot);
        _receiptPath = Path.GetFullPath(receiptPath);
        _backupRoot = Path.Combine(Path.GetDirectoryName(_receiptPath)!, "loader-backups");
        _packageCacheRoot = packageCacheRoot is null ? null : Path.GetFullPath(packageCacheRoot);
        _httpClient = httpClient;
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

    public async Task<InstalledPackageReceipt> InstallLocalFromFileAsync(
        LocalLoaderPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.IsVerified)
        {
            throw new InvalidDataException("Local loader packages must remain unverified.");
        }
        if (await GetStateAsync(cancellationToken) != LoaderState.Vanilla)
        {
            throw new InvalidOperationException("Local loader installation requires a vanilla instance.");
        }
        return await ApplyPackageAsync(
            package.Manifest,
            package.PackagePath,
            null,
            null,
            "install-local-loader",
            cancellationToken,
            package.LoaderState,
            isVerified: false);
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
        var targetRoot = GetCommonDirectory(_instanceRoot, Path.GetDirectoryName(_receiptPath)!);
        using var workspace = CreateStateWorkspace(targetRoot, ".crystalfly-loader-uninstall-");
        var staging = workspace.CreateDirectory("staging");
        var stagedInstanceRoot = ResolveAtOrUnderRoot(
            staging,
            Normalize(Path.GetRelativePath(targetRoot, _instanceRoot)));
        var removals = RestoreOriginals(receipt, stagedInstanceRoot)
            .Select(path => Normalize(Path.GetRelativePath(
                targetRoot,
                ResolveUnderRoot(_instanceRoot, path))))
            .ToList();
        AddStateRemovals(receipt, targetRoot, removals);
        await FileTransaction.ReplaceDirectoryAsync(
            staging, targetRoot, _transactionRoot, "uninstall-loader", removals, cancellationToken);
        DeleteDirectory(receipt.BackupRoot);
        RemoveEmptyLoaderDirectories();
        if (await GetStateAsync(cancellationToken) != LoaderState.Vanilla)
        {
            throw new InvalidOperationException(
                "Loader uninstall requires manual cleanup because unmanaged loader files remain.");
        }
    }

    private async Task<InstalledPackageReceipt> ApplyPackageAsync(
        LoaderManifest manifest,
        string? packagePath,
        Uri? packageUri,
        InstalledPackageReceipt? previousReceipt,
        string operation,
        CancellationToken cancellationToken,
        LoaderState? loaderStateOverride = null,
        bool isVerified = true)
    {
        var size = manifest.SizeBytes;
        if (previousReceipt is not null)
        {
            EnsureBackupRoot(previousReceipt.BackupRoot);
        }
        var loaderState = loaderStateOverride ?? GetLoaderState(manifest.Id);
        if (loaderState is not (LoaderState.ModdingApi or LoaderState.BepInEx))
        {
            throw new InvalidDataException("Loader state must be ModdingApi or BepInEx.");
        }
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
                packageUri, extracted, packageTransactions, size, manifest.Sha256,
                _packageCacheRoot, _httpClient,
                cancellationToken: cancellationToken);
        }
        else
        {
            throw new ArgumentException("A loader package path or URI is required.");
        }

        var packageStaging = workspace.CreateDirectory("staging");
        var prefix = loaderState == LoaderState.ModdingApi
            ? "hollow_knight_Data/Managed"
            : string.Empty;
        CopyTree(extracted, ResolveAtOrUnderRoot(packageStaging, prefix));
        var stagedFiles = Directory.EnumerateFiles(packageStaging, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Normalize(Path.GetRelativePath(packageStaging, path)),
                StringComparer.OrdinalIgnoreCase);
        if (stagedFiles.Count == 0)
        {
            throw new InvalidDataException("Loader package does not contain files.");
        }

        var newBackupRoot = Path.Combine(_backupRoot, Guid.NewGuid().ToString("N"));
        EnsureBackupRoot(newBackupRoot);
        var targetRoot = GetCommonDirectory(_instanceRoot, Path.GetDirectoryName(_receiptPath)!);
        using var stateWorkspace = CreateStateWorkspace(targetRoot, ".crystalfly-loader-state-");
        var stateStaging = stateWorkspace.CreateDirectory("staging");
        var stagedInstanceRoot = ResolveAtOrUnderRoot(
            stateStaging,
            Normalize(Path.GetRelativePath(targetRoot, _instanceRoot)));
        CopyTree(packageStaging, stagedInstanceRoot);
        var stagedBackupRoot = ResolveAtOrUnderRoot(
            stateStaging,
            Normalize(Path.GetRelativePath(targetRoot, newBackupRoot)));
        var newFiles = await BuildReceiptFilesAsync(
            stagedFiles, previousReceipt, stagedBackupRoot, cancellationToken);
        List<string> removals = previousReceipt is null
            ? []
            : RestoreObsoleteOriginals(previousReceipt, stagedFiles.Keys, stagedInstanceRoot)
                .Select(path => Normalize(Path.GetRelativePath(
                    targetRoot,
                    ResolveUnderRoot(_instanceRoot, path))))
                .ToList();
        if (previousReceipt is not null)
        {
            AddBackupRemovals(previousReceipt, targetRoot, removals);
        }

        var receipt = new InstalledPackageReceipt
        {
            PackageId = manifest.Id,
            LoaderState = loaderState,
            IsVerified = isVerified,
            BackupRoot = newBackupRoot,
            Files = newFiles
        };
        var stagedReceipt = ResolveAtOrUnderRoot(
            stateStaging,
            Normalize(Path.GetRelativePath(targetRoot, _receiptPath)));
        await AtomicJsonStore.WriteAsync(stagedReceipt, receipt, cancellationToken);
        File.Copy(stagedReceipt, stagedReceipt + ".bak", overwrite: false);
        await FileTransaction.ReplaceDirectoryAsync(
            stateStaging, targetRoot, _transactionRoot, operation, removals, cancellationToken);
        if (previousReceipt is not null
            && !PathEquals(previousReceipt.BackupRoot, newBackupRoot))
        {
            DeleteDirectory(previousReceipt.BackupRoot);
        }
        return receipt;
    }

    private async Task<IReadOnlyList<InstalledFileReceipt>> BuildReceiptFilesAsync(
        IReadOnlyDictionary<string, string> stagedFiles,
        InstalledPackageReceipt? previousReceipt,
        string stagedBackupRoot,
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
                    CopyFile(
                        ResolveBackup(previousReceipt!, backupRelativePath),
                        ResolveUnderRoot(stagedBackupRoot, backupRelativePath));
                }
            }
            else
            {
                var targetPath = ResolveUnderRoot(_instanceRoot, staged.Key);
                if (File.Exists(targetPath))
                {
                    originalHash = await HashFileAsync(targetPath, cancellationToken);
                    backupRelativePath = staged.Key;
                    CopyFile(targetPath, ResolveUnderRoot(stagedBackupRoot, backupRelativePath));
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

    private void AddStateRemovals(
        InstalledPackageReceipt receipt,
        string targetRoot,
        ICollection<string> removals)
    {
        removals.Add(Normalize(Path.GetRelativePath(targetRoot, _receiptPath)));
        if (File.Exists(_receiptPath + ".bak"))
        {
            removals.Add(Normalize(Path.GetRelativePath(targetRoot, _receiptPath + ".bak")));
        }
        AddBackupRemovals(receipt, targetRoot, removals);
    }

    private void AddBackupRemovals(
        InstalledPackageReceipt receipt,
        string targetRoot,
        ICollection<string> removals)
    {
        EnsureBackupRoot(receipt.BackupRoot);
        if (!Directory.Exists(receipt.BackupRoot))
        {
            return;
        }
        foreach (var path in Directory.EnumerateFiles(receipt.BackupRoot, "*", SearchOption.AllDirectories))
        {
            removals.Add(Normalize(Path.GetRelativePath(targetRoot, path)));
        }
    }

    private TemporaryDirectory CreateStateWorkspace(string targetRoot, string prefix)
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        if (IsAtOrUnder(temporaryRoot, targetRoot))
        {
            throw new InvalidOperationException(
                "The system temporary directory must be outside the loader state transaction root.");
        }
        return new TemporaryDirectory(temporaryRoot, prefix);
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

    private static string ResolveAtOrUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!PathEquals(path, fullRoot)
            && !path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes its root: '{relativePath}'.");
        }
        return path;
    }

    private static string GetCommonDirectory(string left, string right)
    {
        var leftPath = Path.GetFullPath(left);
        var rightPath = Path.GetFullPath(right);
        if (!string.Equals(Path.GetPathRoot(leftPath), Path.GetPathRoot(rightPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Instance files and loader receipts must be stored on the same volume.");
        }

        var candidate = new DirectoryInfo(leftPath);
        while (candidate.Parent is not null && !IsAtOrUnder(rightPath, candidate.FullName))
        {
            candidate = candidate.Parent;
        }
        if (!IsAtOrUnder(rightPath, candidate.FullName)
            || PathEquals(candidate.FullName, Path.GetPathRoot(candidate.FullName)!))
        {
            throw new InvalidOperationException(
                "Instance files and loader receipts must share a directory below the volume root.");
        }
        return candidate.FullName;
    }

    private static bool IsAtOrUnder(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return PathEquals(fullPath, fullRoot)
            || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
