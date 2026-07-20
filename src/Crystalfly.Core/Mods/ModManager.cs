using System.IO.Compression;
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
    private readonly string? _packageCacheRoot;
    private readonly HttpClient? _httpClient;

    public ModManager(
        string instanceRoot,
        string transactionRoot,
        string receiptsRoot,
        string? packageCacheRoot = null,
        HttpClient? httpClient = null)
    {
        _instanceRoot = Path.GetFullPath(instanceRoot);
        _transactionRoot = Path.GetFullPath(transactionRoot);
        _receiptsRoot = Path.GetFullPath(receiptsRoot);
        _packageCacheRoot = packageCacheRoot is null ? null : Path.GetFullPath(packageCacheRoot);
        _httpClient = httpClient;
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

    public Task<IReadOnlyList<TransactionJournal>> RecoverPendingAsync(
        CancellationToken cancellationToken = default) =>
        FileTransaction.RecoverPendingAsync(_transactionRoot, cancellationToken);

    public async Task<InstalledModReceipt> VerifyInstalledAsync(
        ModManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var current = (await GetInstalledAsync(cancellationToken)).SingleOrDefault(receipt =>
            string.Equals(receipt.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Mod '{manifest.Id}' is not installed.");
        if (!current.Enabled
            || !string.Equals(current.Version, manifest.Version, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Installed mod '{manifest.Id}' does not match catalog version '{manifest.Version}'.");
        }
        return await VerifyMatchingInstalledAsync(current, manifest, cancellationToken);
    }

    public async Task<InstalledModReceipt> InstallFromFileAsync(
        ModManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default) =>
        await InstallPackageAsync(manifest, packagePath, null, isLocal: false, progress: null, cancellationToken);

    public async Task<InstalledModReceipt> InstallFromUriAsync(
        ModManifest manifest,
        CancellationToken cancellationToken = default) =>
        await InstallFromUriAsync(manifest, progress: null, cancellationToken);

    public async Task<InstalledModReceipt> InstallFromUriAsync(
        ModManifest manifest,
        IProgress<PackageTransferProgress>? progress,
        CancellationToken cancellationToken = default) =>
        await InstallPackageAsync(
            manifest, null, new Uri(manifest.DownloadUrl), isLocal: false, progress, cancellationToken);

    public async Task<InstalledModReceipt> UpdateFromFileAsync(
        ModManifest manifest,
        string packagePath,
        CancellationToken cancellationToken = default) =>
        await UpdatePackageAsync(manifest, packagePath, null, progress: null, cancellationToken);

    public async Task<InstalledModReceipt> UpdateFromUriAsync(
        ModManifest manifest,
        CancellationToken cancellationToken = default) =>
        await UpdateFromUriAsync(manifest, progress: null, cancellationToken);

    public async Task<InstalledModReceipt> UpdateFromUriAsync(
        ModManifest manifest,
        IProgress<PackageTransferProgress>? progress,
        CancellationToken cancellationToken = default) =>
        await UpdatePackageAsync(manifest, null, new Uri(manifest.DownloadUrl), progress, cancellationToken);

    public async Task<IReadOnlyList<InstalledModReceipt>> InstallWithDependenciesFromFilesAsync(
        IEnumerable<ModManifest> catalog,
        IEnumerable<string> requestedIds,
        IReadOnlyDictionary<string, string> packagePaths,
        CancellationToken cancellationToken = default)
    {
        var order = ModDependencyResolver.ResolveInstallOrder(catalog, requestedIds);
        var installed = (await GetInstalledAsync(cancellationToken))
            .ToDictionary(receipt => receipt.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in order)
        {
            if ((!installed.TryGetValue(manifest.Id, out var receipt)
                    || !string.Equals(receipt.Version, manifest.Version, StringComparison.OrdinalIgnoreCase))
                && (!packagePaths.TryGetValue(manifest.Id, out var path) || !File.Exists(path)))
            {
                throw new FileNotFoundException($"Package for mod '{manifest.Id}' was not found.", path);
            }
        }
        return await InstallWithDependenciesAsync(
            order,
            (manifest, update, token) => update
                ? UpdateFromFileAsync(manifest, packagePaths[manifest.Id], token)
                : InstallFromFileAsync(manifest, packagePaths[manifest.Id], token),
            cancellationToken);
    }

    public async Task<IReadOnlyList<InstalledModReceipt>> InstallWithDependenciesFromUrisAsync(
        IEnumerable<ModManifest> catalog,
        IEnumerable<string> requestedIds,
        CancellationToken cancellationToken = default) =>
        await InstallWithDependenciesAsync(
            ModDependencyResolver.ResolveInstallOrder(catalog, requestedIds),
            (manifest, update, token) => update
                ? UpdateFromUriAsync(manifest, token)
                : InstallFromUriAsync(manifest, token),
            cancellationToken);

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
            LoaderId = loaderId,
            FlatFiles = GetLayout(loaderId) == ModLayout.Flat
                ? LocalFlatFiles(package.FullName)
                : []
        };
        return await InstallPackageAsync(
            manifest, package.FullName, null, isLocal: true, progress: null, cancellationToken);
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
        CancellationToken cancellationToken = default) =>
        await SetEnabledCoreAsync(id, enabled, ignoreEnabledDependents: false, cancellationToken);

    public async Task<InstalledModReceipt> DisableIgnoringDependentsAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        await SetEnabledCoreAsync(id, enabled: false, ignoreEnabledDependents: true, cancellationToken);

    private async Task<InstalledModReceipt> SetEnabledCoreAsync(
        string id,
        bool enabled,
        bool ignoreEnabledDependents,
        CancellationToken cancellationToken)
    {
        var installed = await GetInstalledAsync(cancellationToken);
        var receipt = Find(installed, id);
        if (receipt.Enabled == enabled)
        {
            return receipt;
        }
        if (enabled)
        {
            EnsureDependenciesInstalled(receipt.Dependencies, installed);
        }
        else if (!ignoreEnabledDependents)
        {
            EnsureNoEnabledDependents(installed, id);
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
        var updated = receipt with
        {
            Enabled = enabled,
            InstallRoot = newRoot,
            Files = await ReceiptsFromStagingAsync(staging, cancellationToken)
        };
        await ApplyModStateAsync(
            staging,
            receipt.Files.Select(file => file.RelativePath),
            updated,
            receiptIdToRemove: null,
            enabled ? "enable-mod" : "disable-mod",
            cancellationToken);
        RemoveEmptyParentDirectories(receipt.InstallRoot);
        return updated;
    }

    public async Task UninstallAsync(string id, CancellationToken cancellationToken = default) =>
        await UninstallCoreAsync(id, ignoreDependents: false, cancellationToken);

    public async Task UninstallIgnoringDependentsAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        await UninstallCoreAsync(id, ignoreDependents: true, cancellationToken);

    private async Task UninstallCoreAsync(
        string id,
        bool ignoreDependents,
        CancellationToken cancellationToken)
    {
        var installed = await GetInstalledAsync(cancellationToken);
        var receipt = Find(installed, id);
        if (!ignoreDependents)
        {
            EnsureNoDependents(installed, id);
        }
        await VerifyFilesAsync(receipt, cancellationToken);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".mod-uninstall-");
        await ApplyModStateAsync(
            workspace.CreateDirectory("staging"),
            receipt.Files.Select(file => file.RelativePath),
            receipt: null,
            receipt.Id,
            "uninstall-mod",
            cancellationToken);
        RemoveEmptyParentDirectories(receipt.InstallRoot);
    }

    private async Task<InstalledModReceipt> InstallPackageAsync(
        ModManifest manifest,
        string? packagePath,
        Uri? packageUri,
        bool isLocal,
        IProgress<PackageTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var installed = await GetInstalledAsync(cancellationToken);
        EnsureCanInstall(manifest.Id, manifest.Dependencies, installed);
        using var workspace = new TemporaryDirectory(_transactionRoot, ".mod-install-");
        var (layout, activeRoot, staging) = await PreparePackageAsync(
            manifest, packagePath, packageUri, enabled: true, workspace, progress, cancellationToken);
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

    private async Task<InstalledModReceipt> UpdatePackageAsync(
        ModManifest manifest,
        string? packagePath,
        Uri? packageUri,
        IProgress<PackageTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var installed = await GetInstalledAsync(cancellationToken);
        var current = Find(installed, manifest.Id);
        if (current.IsLocal)
        {
            throw new InvalidOperationException($"Local mod '{manifest.Id}' cannot be updated automatically.");
        }
        if (!string.Equals(current.LoaderId, manifest.LoaderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Mod '{manifest.Id}' cannot change loaders during an update.");
        }
        EnsureDependenciesInstalled(manifest.Dependencies, installed);
        await VerifyFilesAsync(current, cancellationToken);

        using var workspace = new TemporaryDirectory(_transactionRoot, ".mod-update-");
        var (layout, installRoot, staging) = await PreparePackageAsync(
            manifest, packagePath, packageUri, current.Enabled, workspace, progress, cancellationToken);
        EnsureUpdateTargetsAvailable(staging, current);
        EnsureInstallRootAvailable(manifest.Id, installRoot, layout, installed);

        var updated = new InstalledModReceipt
        {
            Id = current.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            LoaderId = manifest.LoaderId,
            InstallRoot = installRoot,
            Enabled = current.Enabled,
            IsLocal = false,
            Dependencies = manifest.Dependencies,
            Files = await ReceiptsFromStagingAsync(staging, cancellationToken)
        };
        await ApplyModStateAsync(
            staging,
            current.Files.Select(file => file.RelativePath),
            updated,
            receiptIdToRemove: null,
            "update-mod",
            cancellationToken);
        if (!string.Equals(current.InstallRoot, installRoot, StringComparison.OrdinalIgnoreCase))
        {
            RemoveEmptyParentDirectories(current.InstallRoot);
        }
        return updated;
    }

    private async Task<(ModLayout Layout, string InstallRoot, string Staging)> PreparePackageAsync(
        ModManifest manifest,
        string? packagePath,
        Uri? packageUri,
        bool enabled,
        TemporaryDirectory workspace,
        IProgress<PackageTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var layout = GetLayout(manifest.LoaderId);
        var installRoot = GetInstallRoot(layout, SafeName(manifest.Name), enabled);
        var extracted = workspace.CreateDirectory("extracted");
        var packageTransactions = workspace.CreateDirectory("package-transactions");
        if (packagePath is not null)
        {
            await PackageInstaller.InstallFromFileAsync(
                packagePath, extracted, packageTransactions, manifest.SizeBytes, manifest.Sha256, cancellationToken);
        }
        else if (packageUri is not null)
        {
            await PackageInstaller.InstallFromUriAsync(
                packageUri,
                extracted,
                packageTransactions,
                manifest.SizeBytes,
                manifest.Sha256,
                _packageCacheRoot,
                _httpClient,
                progress,
                cancellationToken: cancellationToken);
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
            if (manifest.FlatFiles.Count == 0)
            {
                throw new InvalidDataException(
                    $"Flat mod '{manifest.Id}' does not declare package files to install.");
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relativePath in manifest.FlatFiles)
            {
                var source = ResolveUnderRoot(extracted, relativePath);
                if (!File.Exists(source))
                {
                    throw new InvalidDataException(
                        $"Flat mod package file was not found: '{relativePath}'.");
                }
                if (!names.Add(Path.GetFileName(source)))
                {
                    throw new InvalidDataException($"Flat mod package contains duplicate file name '{Path.GetFileName(source)}'.");
                }
                CopyFile(source, ResolveUnderRoot(staging, Normalize(Path.Combine(installRoot, Path.GetFileName(source)))));
            }
        }
        else
        {
            CopyTree(extracted, ResolveUnderRoot(staging, installRoot));
        }
        return (layout, installRoot, staging);
    }

    private async Task<IReadOnlyList<InstalledModReceipt>> InstallWithDependenciesAsync(
        IReadOnlyList<ModManifest> order,
        Func<ModManifest, bool, CancellationToken, Task<InstalledModReceipt>> installOrUpdate,
        CancellationToken cancellationToken)
    {
        var results = new List<InstalledModReceipt>(order.Count);
        foreach (var manifest in order)
        {
            var current = (await GetInstalledAsync(cancellationToken)).FirstOrDefault(receipt =>
                string.Equals(receipt.Id, manifest.Id, StringComparison.OrdinalIgnoreCase));
            var receipt = current is null
                ? await installOrUpdate(manifest, false, cancellationToken)
                : string.Equals(current.Version, manifest.Version, StringComparison.OrdinalIgnoreCase)
                    ? await VerifyMatchingInstalledAsync(current, manifest, cancellationToken)
                    : await installOrUpdate(manifest, true, cancellationToken);
            if (!receipt.Enabled)
            {
                receipt = await SetEnabledAsync(receipt.Id, enabled: true, cancellationToken);
            }
            results.Add(receipt);
        }
        return results;
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
            Files = await ReceiptsFromStagingAsync(staging, cancellationToken)
        };
        await ApplyModStateAsync(
            staging,
            removeRelativePaths: [],
            receipt,
            receiptIdToRemove: null,
            "install-mod",
            cancellationToken);
        return receipt;
    }

    private async Task ApplyModStateAsync(
        string modStagingRoot,
        IEnumerable<string> removeRelativePaths,
        InstalledModReceipt? receipt,
        string? receiptIdToRemove,
        string operation,
        CancellationToken cancellationToken)
    {
        var targetRoot = GetCommonDirectory(_instanceRoot, _receiptsRoot);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        if (IsAtOrUnder(temporaryRoot, targetRoot))
        {
            throw new InvalidOperationException(
                "The system temporary directory must be outside the instance state transaction root.");
        }

        using var workspace = new TemporaryDirectory(temporaryRoot, ".crystalfly-mod-state-");
        var staging = workspace.CreateDirectory("staging");
        var stagedInstanceRoot = ResolveAtOrUnderRoot(
            staging,
            Normalize(Path.GetRelativePath(targetRoot, _instanceRoot)));
        CopyTree(modStagingRoot, stagedInstanceRoot);

        if (receipt is not null)
        {
            var stagedReceipt = ResolveAtOrUnderRoot(
                staging,
                Normalize(Path.GetRelativePath(targetRoot, ReceiptPath(receipt.Id))));
            await AtomicJsonStore.WriteAsync(stagedReceipt, receipt, cancellationToken);
            File.Copy(stagedReceipt, stagedReceipt + ".bak", overwrite: false);
        }

        var removals = removeRelativePaths
            .Select(relativePath => Normalize(Path.GetRelativePath(
                targetRoot,
                ResolveUnderRoot(_instanceRoot, relativePath))))
            .ToList();
        if (receiptIdToRemove is not null)
        {
            var receiptPath = ReceiptPath(receiptIdToRemove);
            if (File.Exists(receiptPath))
            {
                removals.Add(Normalize(Path.GetRelativePath(targetRoot, receiptPath)));
            }
            if (File.Exists(receiptPath + ".bak"))
            {
                removals.Add(Normalize(Path.GetRelativePath(targetRoot, receiptPath + ".bak")));
            }
        }

        await FileTransaction.ReplaceDirectoryAsync(
            staging,
            targetRoot,
            _transactionRoot,
            operation,
            removals,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<InstalledFileReceipt>> ReceiptsFromStagingAsync(
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        var receipts = new List<InstalledFileReceipt>();
        foreach (var file in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories))
        {
            receipts.Add(new InstalledFileReceipt
            {
                RelativePath = Normalize(Path.GetRelativePath(stagingRoot, file)),
                Sha256 = await HashFileAsync(file, cancellationToken)
            });
        }
        return receipts.OrderBy(receipt => receipt.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<InstalledModReceipt> VerifyMatchingInstalledAsync(
        InstalledModReceipt current,
        ModManifest manifest,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(current.LoaderId, manifest.LoaderId, StringComparison.OrdinalIgnoreCase)
            || !current.Dependencies.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(manifest.Dependencies))
        {
            throw new InvalidOperationException(
                $"Installed mod '{current.Id}' metadata does not match catalog version '{manifest.Version}'.");
        }
        await VerifyFilesAsync(current, cancellationToken);
        return current;
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

    private void EnsureUpdateTargetsAvailable(string stagingRoot, InstalledModReceipt current)
    {
        var owned = current.Files.Select(file => file.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var staged in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Normalize(Path.GetRelativePath(stagingRoot, staged));
            if (File.Exists(ResolveUnderRoot(_instanceRoot, relativePath)) && !owned.Contains(relativePath))
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

    private static IReadOnlyList<string> LocalFlatFiles(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return archive.Entries
            .Where(entry => entry.Name.Length != 0
                && string.Equals(entry.FullName, entry.Name, StringComparison.Ordinal)
                && string.Equals(Path.GetExtension(entry.Name), ".dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .ToArray();
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
            throw new InvalidOperationException("Instance files and mod receipts must be stored on the same volume.");
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
                "Instance files and mod receipts must share a directory below the volume root.");
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
