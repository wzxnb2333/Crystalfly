using System.IO.Compression;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Thunderstore;

/// <summary>Stages a dependency closure and its receipts in one recoverable file transaction.</summary>
public sealed class ThunderstoreInstallService(InstanceRecord instance, ThunderstorePackageStore store,
    Func<bool>? isGameRunning = null)
{
    public const string MetadataDirectory = ".crystalfly-thunderstore";
    private const string ReceiptRelativePath = MetadataDirectory + "/installed.json";
    private const string DoorstopConfigPath = "doorstop_config.ini";
    private const string DoorstopVersionPath = ".doorstop_version";
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private string Root => Path.GetFullPath(instance.RootPath);
    private string JournalRoot => Path.Combine(Directory.GetParent(Root)!.FullName, ".crystalfly", "transactions");
    private SemaphoreSlim Gate => Gates.GetOrAdd(Root, _ => new SemaphoreSlim(1, 1));

    public async Task<ThunderstoreInstallState> ReadStateAsync(CancellationToken token = default)
    {
        ValidateTarget(checkRunning: false);
        var path = Resolve(ReceiptRelativePath);
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return new();
        var state = await AtomicJsonStore.ReadAsync<ThunderstoreInstallState>(path, token);
        if (state.Packages is null || state.Packages.Any(item => item is null)
            || state.GameId != instance.GameId || state.SchemaVersion != 1
            || state.Packages.Select(item => item.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Packages.Count)
            throw new InvalidDataException("Invalid Thunderstore installation receipt.");
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in state.Packages)
        {
            _ = ThunderstoreIdentity.SafeName(package.FullName);
            _ = ThunderstoreIdentity.Version(package.Version);
            if (package.Files is null || package.Dependencies is null || package.LoaderBackups is null
                || package.IsLoader != SilksongLoaderAdapter.IsLoader(package.FullName)
                || package.ArchiveSha256 is null || package.ArchiveSha256.Length != 64 || !package.ArchiveSha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid Thunderstore package receipt.");
            foreach (var dependency in package.Dependencies) _ = ThunderstoreIdentity.Dependency(dependency);
            foreach (var file in package.Files)
            {
                ValidateOwnedPath(package, file);
                if (!owned.Add(file.Path)) throw new InvalidDataException("Duplicate file ownership in receipt.");
            }
            var backups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var backup in package.LoaderBackups)
            {
                if (!package.IsLoader || backup is null || backup.Path is null || backup.BackupPath is null || backup.Sha256 is null
                    || !IsLegacyLoaderPath(backup.Path)
                    || backup.Path.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase)
                    || backup.BackupPath != LoaderBackupPath(package.FullName, backup.Path)
                    || backup.Sha256.Length != 64 || !backup.Sha256.All(Uri.IsHexDigit)
                    || !backups.Add(backup.Path))
                    throw new InvalidDataException("Invalid previous loader backup receipt.");
                _ = Resolve(backup.BackupPath);
            }
        }
        return state with
        {
            Packages = state.Packages.Select(package => package with
            {
                Files = package.Files.Select(file => package.IsLoader
                    && file.Path.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase)
                        ? file with { IsConfiguration = true } : file).ToArray()
            }).ToArray()
        };
    }

    public async Task InstallAsync(ThunderstoreInstallPlan plan, bool offline = false,
        bool confirmUnverified = false, bool repair = false, CancellationToken token = default,
        IReadOnlyDictionary<string, ThunderstoreCachedPackage>? localArchives = null)
    {
        if (plan.GameId != instance.GameId || plan.BuildId != instance.BuildId)
            throw new InvalidDataException("The installation target changed after preview.");
        if (plan.Warnings.Count > 0 && !confirmUnverified)
            throw new InvalidOperationException("Confirm unverified game compatibility before installing.");
        // Downloads finish before acquiring the write gate; cancellation cannot leave half an installation.
        var archives = new Dictionary<string, ThunderstoreCachedPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in plan.Packages)
        {
            token.ThrowIfCancellationRequested();
            var name = package.Package.FullName;
            archives[name] = localArchives?.TryGetValue(name, out var local) == true
                ? local : await store.GetAsync(package, offline, token);
        }
        await Gate.WaitAsync(token);
        var staging = NewStaging();
        try
        {
            ValidateTarget();
            await RecoverAsync(token);
            var before = await ReadStateAsync(token);
            var existing = before.Packages.ToDictionary(item => item.FullName, StringComparer.OrdinalIgnoreCase);
            var result = existing.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            var removals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var legacyLoader = !existing.ContainsKey(SilksongLoaderAdapter.PackageName)
                && plan.Packages.Any(item => SilksongLoaderAdapter.IsLoader(item.Package.FullName))
                    ? await ReadLegacyLoaderReceiptAsync(token) : null;
            var migrationBackups = new List<ThunderstoreLoaderBackupFile>();
            foreach (var package in plan.Packages)
            {
                var name = package.Package.FullName;
                existing.TryGetValue(name, out var old);
                if (old?.Pinned == true && old.Version != package.Version.VersionNumber)
                    throw new InvalidOperationException($"Version is locked: {name}");
                if (old?.Version != package.PreviousVersion && old?.Version != package.Version.VersionNumber)
                    throw new InvalidOperationException("Installed packages changed after preview. Create a new preview.");
                var enabled = package.DesiredEnabled ?? (old?.Enabled != false || plan.RequiredEnabled.Contains(name, StringComparer.OrdinalIgnoreCase));
                if (!repair && old?.Version == package.Version.VersionNumber && old.Enabled == enabled)
                {
                    result[name] = old with { Pinned = package.DesiredPinned ?? old.Pinned };
                    continue;
                }
                var archiveInfo = archives[name];
                if (await ThunderstorePackageStore.HashAsync(archiveInfo.Path, token) != archiveInfo.Sha256)
                    throw new InvalidDataException("Package changed after validation.");
                await ThunderstorePackageStore.ValidateAsync(archiveInfo.Path, package, token);
                var loader = SilksongLoaderAdapter.IsLoader(name);
                if (loader && (archiveInfo.Sha256 != SilksongLoaderAdapter.Sha256 || package.Version.VersionNumber != SilksongLoaderAdapter.PackageVersion))
                    throw new InvalidDataException("Unapproved loader package.");
                var files = new List<ThunderstoreFileReceipt>();
                var loaderBackups = old?.LoaderBackups.ToList() ?? [];
                using var archive = ZipFile.OpenRead(archiveInfo.Path);
                var packagePaths = archive.Entries
                    .Select(entry => ThunderstorePackageStore.MapPath(name, ThunderstorePackageStore.ValidateEntry(entry), loader))
                    .Where(path => path is not null).Select(path => path!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (loader && legacyLoader is not null)
                {
                    foreach (var file in legacyLoader.Files.Where(file => !file.RelativePath.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var backup = new ThunderstoreLoaderBackupFile(file.RelativePath, LoaderBackupPath(name, file.RelativePath), file.Sha256);
                        await StageLoaderBackupAsync(staging, backup, token);
                        loaderBackups.Add(backup);
                        migrationBackups.Add(backup);
                    }
                    var markerPath = Resolve(DoorstopVersionPath);
                    if (packagePaths.Contains(DoorstopVersionPath)
                        && File.Exists(markerPath)
                        && !legacyLoader.Files.Any(file => file.RelativePath.Equals(DoorstopVersionPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        await ValidateDoorstopVersionAsync(markerPath, token);
                        var backup = new ThunderstoreLoaderBackupFile(DoorstopVersionPath,
                            LoaderBackupPath(name, DoorstopVersionPath), await ThunderstorePackageStore.HashAsync(markerPath, token));
                        await StageLoaderBackupAsync(staging, backup, token);
                        loaderBackups.Add(backup);
                        migrationBackups.Add(backup);
                    }
                }
                foreach (var entry in archive.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    var relative = ThunderstorePackageStore.MapPath(name, ThunderstorePackageStore.ValidateEntry(entry), loader);
                    if (relative is null) continue;
                    var configuration = relative.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase)
                        || relative.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);
                    var conflicting = existing.Values.FirstOrDefault(other => other.FullName != name
                        && other.Files.Any(file => file.Path.Equals(relative, StringComparison.OrdinalIgnoreCase)));
                    writes.TryGetValue(relative, out var owner);
                    if (conflicting is not null || owner is not null && owner != name)
                        throw new InvalidDataException($"File conflict: {relative} ({conflicting?.FullName ?? owner})");
                    writes[relative] = name;
                    var target = Resolve(relative);
                    if (configuration && File.Exists(target))
                    {
                        // Preserve even externally created configuration, without claiming ownership.
                        var oldFile = old?.Files.FirstOrDefault(file => file.Path.Equals(relative, StringComparison.OrdinalIgnoreCase));
                        if (oldFile is not null) files.Add(oldFile);
                        continue;
                    }
                    var legacyOwned = loader && legacyLoader?.Files.Any(file =>
                        file.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase)) == true;
                    var backedUpMetadata = loader && relative.Equals(DoorstopVersionPath, StringComparison.OrdinalIgnoreCase)
                        && migrationBackups.Any(file => file.Path.Equals(relative, StringComparison.OrdinalIgnoreCase));
                    if (File.Exists(target) && old?.Files.Any(file => file.Path.Equals(relative, StringComparison.OrdinalIgnoreCase)) != true
                        && !legacyOwned && !backedUpMetadata)
                        throw new InvalidDataException($"Refusing to overwrite an unmanaged file: {relative}");
                    var staged = Path.Combine(staging, enabled || configuration ? relative : DisabledPath(name, relative));
                    Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                    await using (var input = entry.Open())
                    await using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        await input.CopyToAsync(output, token);
                    ThunderstorePackageStore.ValidateArchitecture(staged);
                    files.Add(new(relative, await ThunderstorePackageStore.HashAsync(staged, token), configuration));
                }
                if (old is not null)
                    foreach (var file in old.Files.Where(file => !file.IsConfiguration))
                        removals.Add(old.Enabled ? file.Path : DisabledPath(name, file.Path));
                if (loader && legacyLoader is not null)
                    foreach (var file in legacyLoader.Files.Where(file => !file.RelativePath.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase)
                                 && !packagePaths.Contains(file.RelativePath)))
                        removals.Add(file.RelativePath);
                result[name] = new ThunderstoreInstalledPackage
                {
                    FullName = name, Version = package.Version.VersionNumber, ArchiveSha256 = archiveInfo.Sha256,
                    Pinned = package.DesiredPinned ?? old?.Pinned ?? package.Pinned, Enabled = enabled,
                    IsLoader = loader, RuntimeVersion = loader ? SilksongLoaderAdapter.RuntimeVersion : null,
                    LocalPackage = package.IsLocal ? package.Package with { Versions = [package.Version] } : old?.LocalPackage,
                    IsLocal = package.IsLocal || localArchives?.ContainsKey(name) == true || old?.IsLocal == true,
                    Dependencies = package.Version.Dependencies.Select(raw =>
                    {
                        var dependency = ThunderstoreIdentity.Dependency(SilksongLoaderAdapter.CanonicalDependency(raw));
                        var resolved = plan.Packages.Single(item => item.Package.FullName.Equals(dependency.Name, StringComparison.OrdinalIgnoreCase));
                        return dependency.Name + "-" + resolved.Version.VersionNumber;
                    }).ToArray(), Files = files, LoaderBackups = loaderBackups
                };
            }
            var after = before with { Packages = result.Values.OrderBy(item => item.FullName, StringComparer.Ordinal).ToArray() };
            foreach (var package in after.Packages.Where(item => item.Enabled))
                foreach (var dependency in package.Dependencies.Select(ThunderstoreIdentity.Dependency))
                    if (!after.Packages.Any(item => item.FullName == dependency.Name && item.Enabled))
                        throw new InvalidDataException($"Enabled package requires a disabled dependency: {package.FullName} → {dependency.Name}");
            foreach (var backup in migrationBackups)
            {
                var source = Resolve(backup.Path);
                if (!File.Exists(source) || !string.Equals(await ThunderstorePackageStore.HashAsync(source, token), backup.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Existing BepInEx files changed during the upgrade; no files were installed.");
            }
            await CommitAsync(staging, after, removals, "thunderstore-install", token);
        }
        finally { CleanupStaging(staging); Gate.Release(); }
    }

    public Task SetEnabledAsync(string name, bool enabled, CancellationToken token = default) => MutateAsync(name, enabled, null, false, token);
    public Task SetPinnedAsync(string name, bool pinned, CancellationToken token = default) => MutateAsync(name, null, pinned, false, token);
    public Task UninstallAsync(string name, CancellationToken token = default) => MutateAsync(name, null, null, true, token);

    private async Task MutateAsync(string name, bool? enabled, bool? pinned, bool uninstall, CancellationToken token)
    {
        await Gate.WaitAsync(token);
        var staging = NewStaging();
        try
        {
            ValidateTarget();
            await RecoverAsync(token);
            var before = await ReadStateAsync(token);
            var selected = before.Packages.Single(item => item.FullName == name);
            var removesActivePackage = uninstall || enabled == false;
            if (removesActivePackage)
            {
                var dependants = before.Packages.Where(item => item.FullName != name && (uninstall || item.Enabled)
                    && item.Dependencies.Any(dependency => ThunderstoreIdentity.Dependency(dependency).Name == name)).ToArray();
                if (dependants.Length != 0) throw new InvalidOperationException("Required by: " + string.Join(", ", dependants.Select(item => item.FullName)));
            }
            if (enabled == true)
                foreach (var dependency in selected.Dependencies.Select(ThunderstoreIdentity.Dependency))
                    if (!before.Packages.Any(item => item.FullName == dependency.Name && item.Enabled))
                        throw new InvalidOperationException($"Enable the dependency first: {dependency.Name}");
            var removals = new List<string>();
            if (uninstall || enabled is not null && enabled != selected.Enabled)
                foreach (var file in selected.Files.Where(file => !file.IsConfiguration))
                {
                    var oldPath = selected.Enabled ? file.Path : DisabledPath(name, file.Path);
                    var source = Resolve(oldPath);
                    if (File.Exists(source))
                    {
                        if (await ThunderstorePackageStore.HashAsync(source, token) != file.Sha256)
                            throw new InvalidDataException($"Managed file was changed; export it or repair before removing: {file.Path}");
                        if (!uninstall)
                        {
                            var nextPath = enabled == true ? file.Path : DisabledPath(name, file.Path);
                            if (File.Exists(Resolve(nextPath))) throw new InvalidDataException($"File conflict: {nextPath}");
                            var target = Path.Combine(staging, nextPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            File.Copy(source, target);
                        }
                        removals.Add(oldPath);
                    }
                }
            if (uninstall && selected.IsLoader)
            {
                foreach (var backup in selected.LoaderBackups)
                {
                    var source = Resolve(backup.BackupPath);
                    if (!File.Exists(source) || !string.Equals(await ThunderstorePackageStore.HashAsync(source, token), backup.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Previous loader backup changed; refusing to restore: {backup.Path}");
                    var restoreTarget = Resolve(backup.Path);
                    if (File.Exists(restoreTarget) && !removals.Contains(backup.Path, StringComparer.OrdinalIgnoreCase))
                        throw new InvalidDataException($"A user file blocks previous loader restoration: {backup.Path}");
                    var target = Path.Combine(staging, backup.Path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target);
                    removals.Add(backup.BackupPath);
                }
            }
            var after = before with { Packages = before.Packages.Where(item => item.FullName != name)
                .Concat(uninstall ? [] : new[] { selected with { Enabled = enabled ?? selected.Enabled, Pinned = pinned ?? selected.Pinned } }).ToArray() };
            await CommitAsync(staging, after, removals, "thunderstore-manage", token);
        }
        finally { CleanupStaging(staging); Gate.Release(); }
    }

    public async Task<IReadOnlyList<ModHealthReport>> AssessHealthAsync(CancellationToken token = default)
    {
        var state = await ReadStateAsync(token);
        var reports = new List<ModHealthReport>();
        foreach (var package in state.Packages.Where(item => !item.IsLoader))
        {
            var missing = new List<string>(); var changed = new List<string>();
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in package.Files.Where(file => !file.IsConfiguration))
            {
                var path = Resolve(package.Enabled ? file.Path : DisabledPath(package.FullName, file.Path));
                if (!File.Exists(path)) { missing.Add(file.Path); continue; }
                var hash = await ThunderstorePackageStore.HashAsync(path, token);
                hashes[file.Path] = hash;
                if (hash != file.Sha256) changed.Add(file.Path);
            }
            reports.Add(new ModHealthReport { ModId = package.FullName, MissingFiles = missing, ModifiedFiles = changed,
                CurrentFileSha256ByPath = hashes, Status = missing.Count > 0 ? ModHealthStatus.CriticalFileMissing
                    : changed.Count > 0 ? ModHealthStatus.ModifiedFile : ModHealthStatus.Healthy });
        }
        return reports;
    }

    public async Task<IReadOnlyList<string>> VerifyAsync(CancellationToken token = default)
    {
        var state = await ReadStateAsync(token);
        var errors = new List<string>();
        foreach (var package in state.Packages)
            foreach (var file in package.Files.Where(file => !file.IsConfiguration))
            {
                var path = Resolve(package.Enabled ? file.Path : DisabledPath(package.FullName, file.Path));
                if (!File.Exists(path) || await ThunderstorePackageStore.HashAsync(path, token) != file.Sha256)
                    errors.Add(package.FullName + ": " + file.Path);
            }
        return errors;
    }

    public async Task<ThunderstorePreset> CreatePresetAsync(string name, CancellationToken token = default) =>
        new(name, instance.GameId, instance.BuildId, (await ReadStateAsync(token)).Packages
            .Select(item => new ThunderstorePresetEntry(item.FullName, item.Version, item.Enabled, item.Pinned)).ToArray());

    private async Task CommitAsync(string staging, ThunderstoreInstallState state, IEnumerable<string> removals,
        string operation, CancellationToken token)
    {
        ValidateTarget();
        await AtomicJsonStore.WriteAsync(Path.Combine(staging, ReceiptRelativePath), state, token);
        await FileTransaction.ReplaceDirectoryAsync(staging, Root, JournalRoot, operation, removals, token);
    }
    private async Task RecoverAsync(CancellationToken token)
    {
        var recovered = await FileTransaction.RecoverPendingAsync(JournalRoot, token);
        if (recovered.Any(item => item.State == TransactionState.NeedsAttention))
            throw new InvalidOperationException("A previous file transaction needs recovery.");
    }
    private string NewStaging() => Path.Combine(store.Root, "staging", Guid.NewGuid().ToString("N"));
    private static void CleanupStaging(string staging)
    {
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
    }
    private static string DisabledPath(string name, string path) => $"{MetadataDirectory}/disabled/{name}/{path}";
    private static string LoaderBackupPath(string name, string path) => $"{MetadataDirectory}/loader-backup/{name}/{path}";
    private string Resolve(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(Root, relative));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path leaves the instance.");
        InstanceDirectory.RejectReparseAncestors(path);
        return path;
    }
    private void ValidateTarget(bool checkRunning = true)
    {
        if (instance.GameId != GameId.Silksong) throw new InvalidDataException("Silksong package cannot be installed into another game.");
        InstanceDirectory.RejectReparseAncestors(Root);
        if (checkRunning && (isGameRunning?.Invoke() ?? new SystemHollowKnightProcessProbe(GameDefinition.Silksong.ProcessName).IsRunning()))
            throw new InvalidOperationException("Close Silksong before changing its mods.");
    }
    private async Task<InstalledPackageReceipt?> ReadLegacyLoaderReceiptAsync(CancellationToken token)
    {
        var stateRoot = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(Root, instance.Id))!;
        var path = Path.Combine(stateRoot, "loader.json");
        InstanceDirectory.RejectReparseAncestors(path);
        InstanceDirectory.RejectReparseAncestors(path + ".bak");
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
        var receipt = await AtomicJsonStore.ReadAsync<InstalledPackageReceipt>(path, token);
        LoaderStateDetector.ValidateReceipt(receipt);
        const string prefix = "bepinex-";
        if (receipt.LoaderState != LoaderState.BepInEx || receipt.IsVerified || !string.IsNullOrEmpty(receipt.BackupRoot)
            || receipt.GameId is not (GameId.Silksong or GameId.HollowKnight)
            || !receipt.PackageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !Version.TryParse(receipt.PackageId[prefix.Length..], out var version) || version.Build < 0
            || version.Major != 5 || version.Minor != 4
            || version > Version.Parse(SilksongLoaderAdapter.RuntimeVersion)
            || receipt.SupportedBuildIds.Count != 0 || receipt.Files.Count == 0
            || receipt.Files.Any(file => file.OriginalSha256 is not null || file.BackupRelativePath is not null
                || !IsLegacyLoaderPath(file.RelativePath)))
            throw new InvalidOperationException("Only a clean externally adopted BepInEx installation can be upgraded by Thunderstore.");
        var requiredFiles = new[]
        {
            "BepInEx/core/BepInEx.dll",
            "BepInEx/core/BepInEx.Preloader.dll",
            "winhttp.dll",
            DoorstopConfigPath
        };
        if (requiredFiles.Any(required => !receipt.Files.Any(file =>
                file.RelativePath.Equals(required, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("The external BepInEx receipt is incomplete; refusing to transfer ownership.");
        foreach (var file in receipt.Files)
        {
            var source = Resolve(file.RelativePath);
            if (!File.Exists(source) || !string.Equals(await ThunderstorePackageStore.HashAsync(source, token), file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Existing BepInEx files changed; refusing to upgrade the loader.");
            if (file.RelativePath.Equals(DoorstopVersionPath, StringComparison.OrdinalIgnoreCase))
                await ValidateDoorstopVersionAsync(source, token);
        }
        return receipt;
    }
    private async Task StageLoaderBackupAsync(string staging, ThunderstoreLoaderBackupFile backup, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var source = Resolve(backup.Path);
        var existingBackup = Resolve(backup.BackupPath);
        if (File.Exists(existingBackup) || Directory.Exists(existingBackup))
            throw new InvalidDataException($"An unreceipted loader backup already exists: {backup.BackupPath}");
        if (!File.Exists(source) || !string.Equals(await ThunderstorePackageStore.HashAsync(source, token), backup.Sha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Existing BepInEx file changed; refusing to transfer ownership: {backup.Path}");
        var target = Path.Combine(staging, backup.BackupPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target);
        if (!string.Equals(await ThunderstorePackageStore.HashAsync(target, token), backup.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Loader backup verification failed: {backup.Path}");
    }
    private async Task ValidateDoorstopVersionAsync(string path, CancellationToken token)
    {
        if (new FileInfo(path).Length > 64)
            throw new InvalidDataException("Existing .doorstop_version metadata is too large to migrate.");
        var text = await File.ReadAllTextAsync(path, token);
        if (text.Length > 64 || !Version.TryParse(text.Trim(), out var version) || version.Build < 0)
            throw new InvalidDataException("Existing .doorstop_version metadata is invalid.");
    }
    private static bool IsLegacyLoaderPath(string path) =>
        IsSafeRelativePath(path)
        && (path.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase)
            || path.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(DoorstopVersionPath, StringComparison.OrdinalIgnoreCase));
    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains('\\')
        && !path.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':'));
    private void ValidateOwnedPath(ThunderstoreInstalledPackage package, ThunderstoreFileReceipt file)
    {
        if (file is null || string.IsNullOrEmpty(file.Path) || file.Sha256 is null)
            throw new InvalidDataException("Invalid managed file path.");
        if (file.Path.Contains('\\') || file.Path.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':')))
            throw new InvalidDataException("Invalid managed file path.");
        _ = Resolve(file.Path);
        var allowed = file.IsConfiguration && (file.Path.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase)
            || package.IsLoader && file.Path.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase));
        if (package.IsLoader && package.FullName == SilksongLoaderAdapter.PackageName)
            allowed |= file.Path.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase)
                || file.Path.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase)
                || file.Path.Equals(DoorstopConfigPath, StringComparison.OrdinalIgnoreCase)
                || file.Path.Equals(DoorstopVersionPath, StringComparison.OrdinalIgnoreCase);
        else
            allowed |= new[] { "plugins", "patchers", "monomod", "core" }.Any(route =>
                file.Path.StartsWith($"BepInEx/{route}/{package.FullName}/", StringComparison.OrdinalIgnoreCase));
        if (!allowed || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid managed file ownership.");
    }
}
