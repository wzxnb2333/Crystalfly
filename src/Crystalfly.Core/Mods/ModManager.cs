using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Models;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Mods;

public sealed class ModManager
{
    private readonly string _instanceRoot;
    private readonly string _transactionRoot;
    private readonly string _receiptsRoot;

    public ModManager(string instanceRoot, string transactionRoot, string receiptsRoot)
    {
        _instanceRoot = Path.GetFullPath(instanceRoot);
        _transactionRoot = Path.GetFullPath(transactionRoot);
        _receiptsRoot = Path.GetFullPath(receiptsRoot);
    }

    public async Task<IReadOnlyList<InstalledModReceipt>> GetInstalledAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_receiptsRoot))
        {
            return [];
        }
        var receipts = new List<InstalledModReceipt>();
        foreach (var path in Directory.EnumerateFiles(_receiptsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            receipts.Add(await AtomicJsonStore.ReadAsync<InstalledModReceipt>(path, cancellationToken));
        }
        return receipts.OrderBy(receipt => receipt.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<InstalledModReceipt> InstallFromFileAsync(
        ModManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default) =>
        await InstallPackageAsync(manifest, packagePath, null, isLocal: false, cancellationToken);

    public async Task<InstalledModReceipt> InstallFromUriAsync(
        ModManifest manifest,
        CancellationToken cancellationToken = default) =>
        await InstallPackageAsync(
            manifest, null, new Uri(manifest.DownloadUrl), isLocal: false, cancellationToken);

    public async Task<InstalledModReceipt> ImportLocalZipAsync(
        string id,
        string name,
        string loaderId,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var package = new FileInfo(packagePath);
        if (!package.Exists)
        {
            throw new FileNotFoundException("Local mod package was not found.", packagePath);
        }
        var manifest = new ModManifest
        {
            Id = id,
            Name = name,
            Version = "local",
            DownloadUrl = "https://local.invalid/package.zip",
            SizeBytes = package.Length,
            Sha256 = await HashFileAsync(package.FullName, cancellationToken),
            LoaderId = loaderId
        };
        return await InstallPackageAsync(
            manifest, package.FullName, null, isLocal: true, cancellationToken);
    }

    public async Task<InstalledModReceipt> ImportLocalDllAsync(
        string id,
        string name,
        string loaderId,
        string dllPath,
        CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(dllPath);
        if (!File.Exists(source) || !string.Equals(Path.GetExtension(source), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Local DLL import requires an existing .dll file.");
        }
        var installed = await GetInstalledAsync(cancellationToken);
        EnsureCanInstall(id, [], installed);
        var layout = GetLayout(loaderId);
        var activeRoot = GetInstallRoot(layout, SafeName(name), enabled: true);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".mod-dll-");
        var staging = workspace.CreateDirectory("staging");
        CopyFile(source, ResolveUnderRoot(staging, Normalize(Path.Combine(activeRoot, Path.GetFileName(source)))));
        return await CommitInstallAsync(
            id, name, "local", loaderId, [], isLocal: true,
            layout, activeRoot, staging, installed, cancellationToken);
    }

    public async Task<InstalledModReceipt> SetEnabledAsync(
        string id,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var installed = await GetInstalledAsync(cancellationToken);
        var receipt = Find(installed, id);
        if (receipt.Enabled == enabled)
        {
            return receipt;
        }
        if (!enabled)
        {
            EnsureNoEnabledDependents(installed, id);
        }
        else
        {
            EnsureDependenciesInstalled(receipt.Dependencies, installed);
        }
        await VerifyFilesAsync(receipt, cancellationToken);

        var layout = GetLayout(receipt.LoaderId);
        var newRoot = GetInstallRoot(layout, SafeName(receipt.Name), enabled);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".mod-toggle-");
        var staging = workspace.CreateDirectory("staging");
        foreach (var file in receipt.Files)
        {
            var relativeInsideMod = RelativeInside(receipt.InstallRoot, file.RelativePath);
            CopyFile(
                ResolveUnderRoot(_instanceRoot, file.RelativePath),
                ResolveUnderRoot(staging, Normalize(Path.Combine(newRoot, relativeInsideMod))));
        }
        EnsureTargetsAbsent(staging);
        var transaction = await FileTransaction.ReplaceDirectoryAsync(
            staging,
            _instanceRoot,
            _transactionRoot,
            enabled ? "enable-mod" : "disable-mod",
            receipt.Files.Select(file => file.RelativePath),
            cancellationToken);
        var updated = receipt with
        {
            Enabled = enabled,
            InstallRoot = newRoot,
            Files = ReceiptsFrom(transaction)
        };
        await AtomicJsonStore.WriteAsync(ReceiptPath(receipt.Id), updated, cancellationToken);
        RemoveEmptyParentDirectories(receipt.InstallRoot);
        return updated;
    }

    public async Task UninstallAsync(string id, CancellationToken cancellationToken = default)
    {
        var installed = await GetInstalledAsync(cancellationToken);
        var receipt = Find(installed, id);
        EnsureNoDependents(installed, id);
        await VerifyFilesAsync(receipt, cancellationToken);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".mod-uninstall-");
        await FileTransaction.ReplaceDirectoryAsync(
            workspace.CreateDirectory("staging"),
            _instanceRoot,
            _transactionRoot,
            "uninstall-mod",
            receipt.Files.Select(file => file.RelativePath),
            cancellationToken);
        File.Delete(ReceiptPath(receipt.Id));
        RemoveEmptyParentDirectories(receipt.InstallRoot);
    }

    private async Task<InstalledModReceipt> InstallPackageAsync(
        ModManifest manifest,
        string? packagePath,
        Uri? packageUri,
        bool isLocal,
        CancellationToken cancellationToken)
    {
        var size = manifest.SizeBytes
            ?? throw new InvalidDataException($"Mod '{manifest.Id}' does not declare its package size.");
        var installed = await GetInstalledAsync(cancellationToken);
        EnsureCanInstall(manifest.Id, manifest.Dependencies, installed);
        var layout = GetLayout(manifest.LoaderId);
        var activeRoot = GetInstallRoot(layout, SafeName(manifest.Name), enabled: true);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".mod-install-");
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
            throw new ArgumentException("A mod package path or URI is required.");
        }
        if (!Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories).Any())
        {
            throw new InvalidDataException("Mod package does not contain files.");
        }
        var staging = workspace.CreateDirectory("staging");
        if (layout == ModLayout.Flat)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories))
            {
                if (!names.Add(Path.GetFileName(source)))
                {
                    throw new InvalidDataException($"Flat mod package contains duplicate file name '{Path.GetFileName(source)}'.");
                }
                CopyFile(source, ResolveUnderRoot(staging, Normalize(Path.Combine(activeRoot, Path.GetFileName(source)))));
            }
        }
        else
        {
            CopyTree(extracted, ResolveUnderRoot(staging, activeRoot));
        }
        return await CommitInstallAsync(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.LoaderId,
            manifest.Dependencies,
            isLocal,
            layout,
            activeRoot,
            staging,
            installed,
            cancellationToken);
    }

    private async Task<InstalledModReceipt> CommitInstallAsync(
        string id,
        string name,
        string version,
        string loaderId,
        IReadOnlyList<string> dependencies,
        bool isLocal,
        ModLayout layout,
        string installRoot,
        string staging,
        IReadOnlyList<InstalledModReceipt> installed,
        CancellationToken cancellationToken)
    {
        EnsureTargetsAbsent(staging);
        EnsureInstallRootAvailable(id, installRoot, layout, installed);
        var transaction = await FileTransaction.ApplyDirectoryAsync(
            staging, _instanceRoot, _transactionRoot, "install-mod", cancellationToken);
        var receipt = new InstalledModReceipt
        {
            Id = id,
            Name = name,
            Version = version,
            LoaderId = loaderId,
            InstallRoot = installRoot,
            Enabled = true,
            IsLocal = isLocal,
            Dependencies = dependencies,
            Files = ReceiptsFrom(transaction)
        };
        try
        {
            await AtomicJsonStore.WriteAsync(ReceiptPath(id), receipt, cancellationToken);
        }
        catch
        {
            var rollbackStaging = Path.Combine(Path.GetDirectoryName(staging)!, "receipt-rollback");
            Directory.CreateDirectory(rollbackStaging);
            await FileTransaction.ReplaceDirectoryAsync(
                rollbackStaging,
                _instanceRoot,
                _transactionRoot,
                "rollback-mod-install",
                receipt.Files.Select(file => file.RelativePath),
                CancellationToken.None);
            throw;
        }
        return receipt;
    }

    private void EnsureTargetsAbsent(string stagingRoot)
    {
        foreach (var staged in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Normalize(Path.GetRelativePath(stagingRoot, staged));
            if (File.Exists(ResolveUnderRoot(_instanceRoot, relativePath)))
            {
                throw new IOException($"Mod target already exists: '{relativePath}'.");
            }
        }
    }

    private static void EnsureCanInstall(
        string id,
        IReadOnlyList<string> dependencies,
        IReadOnlyList<InstalledModReceipt> installed)
    {
        if (installed.Any(receipt => string.Equals(receipt.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Mod '{id}' is already installed.");
        }
        EnsureDependenciesInstalled(dependencies, installed);
    }

    private static void EnsureDependenciesInstalled(
        IEnumerable<string> dependencies,
        IReadOnlyList<InstalledModReceipt> installed)
    {
        var enabled = installed.Where(receipt => receipt.Enabled)
            .Select(receipt => receipt.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = dependencies.Where(dependency => !enabled.Contains(dependency)).ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidOperationException($"Missing enabled mod dependencies: {string.Join(", ", missing)}.");
        }
    }

    private static void EnsureNoDependents(IReadOnlyList<InstalledModReceipt> installed, string id)
    {
        var dependents = FindDependents(installed, id);
        if (dependents.Count != 0)
        {
            throw new InvalidOperationException(
                $"Mod '{id}' is required by: {string.Join(", ", dependents.Select(receipt => receipt.Id))}.");
        }
    }

    private static void EnsureNoEnabledDependents(IReadOnlyList<InstalledModReceipt> installed, string id)
    {
        var dependents = FindDependents(installed, id).Where(receipt => receipt.Enabled).ToArray();
        if (dependents.Length != 0)
        {
            throw new InvalidOperationException(
                $"Mod '{id}' is required by enabled mods: {string.Join(", ", dependents.Select(receipt => receipt.Id))}.");
        }
    }

    private static IReadOnlyList<InstalledModReceipt> FindDependents(
        IReadOnlyList<InstalledModReceipt> installed,
        string id)
    {
        var dependencyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var receipt in installed)
            {
                if (!dependencyIds.Contains(receipt.Id) && receipt.Dependencies.Any(dependencyIds.Contains))
                {
                    dependencyIds.Add(receipt.Id);
                    changed = true;
                }
            }
        }
        dependencyIds.Remove(id);
        return installed.Where(receipt => dependencyIds.Contains(receipt.Id)).ToArray();
    }

    private async Task VerifyFilesAsync(InstalledModReceipt receipt, CancellationToken cancellationToken)
    {
        foreach (var file in receipt.Files)
        {
            var path = ResolveUnderRoot(_instanceRoot, file.RelativePath);
            if (!File.Exists(path)
                || !string.Equals(
                    file.Sha256,
                    await HashFileAsync(path, cancellationToken),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Mod '{receipt.Id}' has drifted files.");
            }
        }
    }

    private string ReceiptPath(string id)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return Path.Combine(_receiptsRoot, $"{hash}.json");
    }

    private static InstalledModReceipt Find(IEnumerable<InstalledModReceipt> installed, string id) =>
        installed.FirstOrDefault(receipt => string.Equals(receipt.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Mod '{id}' is not installed.");

    private static IReadOnlyList<InstalledFileReceipt> ReceiptsFrom(TransactionJournal transaction) =>
        transaction.Changes.Where(change => !change.IsDeletion)
            .Select(change => new InstalledFileReceipt
            {
                RelativePath = change.RelativePath,
                Sha256 = change.AppliedSha256
            })
            .ToArray();

    private static ModLayout GetLayout(string loaderId)
    {
        if (loaderId.Equals("modding-api-37", StringComparison.OrdinalIgnoreCase)
            || loaderId.Equals("modding-api-60", StringComparison.OrdinalIgnoreCase))
        {
            return ModLayout.Flat;
        }
        if (loaderId.StartsWith("modding-api-", StringComparison.OrdinalIgnoreCase))
        {
            return ModLayout.Subdirectory;
        }
        if (loaderId.StartsWith("bepinex-", StringComparison.OrdinalIgnoreCase))
        {
            return ModLayout.BepInEx;
        }
        throw new InvalidDataException($"Unsupported mod loader: '{loaderId}'.");
    }

    private static string GetInstallRoot(ModLayout layout, string name, bool enabled) => layout switch
    {
        ModLayout.Flat when enabled => "hollow_knight_Data/Managed/Mods",
        ModLayout.Flat => $"hollow_knight_Data/Managed/Mods/Disabled/{name}",
        ModLayout.Subdirectory when enabled => $"hollow_knight_Data/Managed/Mods/{name}",
        ModLayout.Subdirectory => $"hollow_knight_Data/Managed/Mods/Disabled/{name}",
        ModLayout.BepInEx when enabled => $"BepInEx/plugins/{name}",
        ModLayout.BepInEx => $"BepInEx/Disabled/{name}",
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray())
            .Trim()
            .TrimEnd('.');
        return result.Length == 0
            ? throw new InvalidDataException("Mod name does not contain a safe directory name.")
            : result;
    }

    private static string RelativeInside(string root, string path)
    {
        var prefix = root.TrimEnd('/') + '/';
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Mod receipt file escapes its install root: '{path}'.");
        }
        return path[prefix.Length..];
    }

    private void EnsureInstallRootAvailable(
        string id,
        string installRoot,
        ModLayout layout,
        IEnumerable<InstalledModReceipt> installed)
    {
        if (layout == ModLayout.Flat)
        {
            return;
        }
        if (installed.Any(receipt =>
            !string.Equals(receipt.Id, id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(receipt.InstallRoot, installRoot, StringComparison.OrdinalIgnoreCase)))
        {
            throw new IOException($"Another mod already owns install directory '{installRoot}'.");
        }
    }

    private void RemoveEmptyParentDirectories(string relativeRoot)
    {
        var root = ResolveUnderRoot(_instanceRoot, relativeRoot);
        var stop = relativeRoot.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(_instanceRoot, "BepInEx")
            : Path.Combine(_instanceRoot, "hollow_knight_Data", "Managed", "Mods");
        for (var current = root;
             Directory.Exists(current)
             && !PathEquals(current, stop)
             && !Directory.EnumerateFileSystemEntries(current).Any();
             current = Path.GetDirectoryName(current)!)
        {
            Directory.Delete(current);
        }
    }

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

    private enum ModLayout
    {
        Flat,
        Subdirectory,
        BepInEx
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

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
