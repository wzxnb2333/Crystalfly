using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Thunderstore;

public static class ThunderstoreReceiptProjection
{
    public static async Task<InstalledPackageReceipt?> ReadLoaderAsync(string root, CancellationToken token = default)
    {
        var path = Path.Combine(root, ThunderstoreInstallService.MetadataDirectory, "installed.json");
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
        InstanceDirectory.RejectReparseAncestors(path);
        InstanceDirectory.RejectReparseAncestors(path + ".bak");
        var state = await AtomicJsonStore.ReadAsync<ThunderstoreInstallState>(path, token);
        if (state.GameId != GameId.Silksong || state.SchemaVersion != 1 || state.Packages is null
            || state.Packages.Any(item => item is null)
            || state.Packages.Select(item => item.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Packages.Count)
            throw new InvalidDataException("Invalid Silksong receipt.");
        foreach (var package in state.Packages) _ = ThunderstoreIdentity.SafeName(package.FullName);
        var loader = state.Packages.SingleOrDefault(item => item.FullName == SilksongLoaderAdapter.PackageName);
        if (loader is null) return null;
        if (!loader.IsLoader || loader.Version != SilksongLoaderAdapter.PackageVersion
            || !string.Equals(loader.ArchiveSha256, SilksongLoaderAdapter.Sha256, StringComparison.OrdinalIgnoreCase)
            || loader.RuntimeVersion != SilksongLoaderAdapter.RuntimeVersion
            || loader.Files is null || loader.Dependencies is null || loader.LoaderBackups is null)
            throw new InvalidDataException("Invalid approved Silksong loader receipt.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in loader.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Path) || file.Path.Contains('\\')
                || file.Path.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':'))
                || file.Sha256 is null || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit)
                || !paths.Add(file.Path))
                throw new InvalidDataException("Invalid approved Silksong loader file receipt.");
            var configuration = file.Path.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase)
                || file.Path.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);
            var loaderFile = file.Path.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase)
                || file.Path.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase)
                || file.Path.Equals(".doorstop_version", StringComparison.OrdinalIgnoreCase);
            var legacyRootDoorstopConfig = file.Path.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase)
                && !file.IsConfiguration;
            if (file.IsConfiguration && !configuration
                || !file.IsConfiguration && configuration && !legacyRootDoorstopConfig
                || !configuration && !loaderFile)
                throw new InvalidDataException("Invalid approved Silksong loader file ownership.");
        }
        foreach (var backup in loader.LoaderBackups)
        {
            if (backup is null || string.IsNullOrWhiteSpace(backup.Path) || backup.Path.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase)
                || !IsSafeLoaderFile(backup.Path)
                || backup.BackupPath != $"{ThunderstoreInstallService.MetadataDirectory}/loader-backup/{loader.FullName}/{backup.Path}"
                || backup.Sha256 is null || backup.Sha256.Length != 64 || !backup.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid previous loader backup receipt.");
        }
        return new InstalledPackageReceipt
        {
            GameId = GameId.Silksong, PackageId = "bepinex-silksong-" + SilksongLoaderAdapter.RuntimeVersion,
            LoaderState = LoaderState.BepInEx, IsVerified = false,
            Files = loader.Files.Where(file => !file.IsConfiguration
                && !file.Path.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase)).Select(file => new InstalledFileReceipt
                { RelativePath = file.Path, Sha256 = file.Sha256 }).ToArray()
        };
    }

    private static bool IsSafeLoaderFile(string path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains('\\')
        && !path.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':'))
        && (path.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase)
            || path.Equals(".doorstop_version", StringComparison.OrdinalIgnoreCase));

    public static ModDiscoveryResult Mods(ThunderstoreInstallState state, string root)
    {
        var packages = state.Packages.Where(item => !item.IsLoader).ToArray();
        return new ModDiscoveryResult
        {
            InstalledReceipts = packages.Select(item => new InstalledModReceipt
            {
                GameId = GameId.Silksong, Id = item.FullName, Name = item.FullName, Version = item.Version,
                LoaderId = "bepinex-silksong-" + SilksongLoaderAdapter.RuntimeVersion,
                Enabled = item.Enabled, Pinned = item.Pinned, IsLocal = item.IsLocal,
                InstallRoot = root, Ownership = ModOwnership.Managed,
                Dependencies = item.Dependencies.Select(ThunderstoreIdentity.Dependency)
                    .Where(dependency => dependency.Name != SilksongLoaderAdapter.PackageName).Select(dependency => dependency.Name).ToArray(),
                Files = item.Files.Where(file => !file.IsConfiguration).Select(file => new InstalledFileReceipt
                    { RelativePath = file.Path, Sha256 = file.Sha256 }).ToArray()
            }).ToArray(),
            Mods = packages.Select(item => new ModDiscoveryEntry
            {
                Id = item.FullName, Name = item.FullName, Enabled = item.Enabled, InstallRoot = root,
                LoaderId = "bepinex-silksong-" + SilksongLoaderAdapter.RuntimeVersion, Ownership = ModOwnership.Managed,
                Files = item.Files.Select(file => file.Path).ToArray()
            }).ToArray()
        };
    }
}
