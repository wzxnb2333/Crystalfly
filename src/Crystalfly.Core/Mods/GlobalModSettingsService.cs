using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Mods;

public interface IGlobalModSettingsService
{
    GlobalModSettingsFile ResolveFile(string instanceId, ModManifest mod);

    IReadOnlyList<GlobalModSettingsFile> ListFiles(
        string instanceId,
        IEnumerable<ModManifest> officialMods);

    Task<int> DeleteAsync(
        string instanceId,
        IEnumerable<ModManifest> officialMods,
        CancellationToken cancellationToken = default);
}

public sealed record GlobalModSettingsFile(string ModId, string ModName, string FilePath);

public sealed class GlobalModSettingsService : IGlobalModSettingsService
{
    private readonly LocalLowIsolationService isolation;
    private readonly IHollowKnightProcessProbe processProbe;

    public GlobalModSettingsService(
        LocalLowIsolationService isolation,
        IHollowKnightProcessProbe? processProbe = null)
    {
        this.isolation = isolation ?? throw new ArgumentNullException(nameof(isolation));
        this.processProbe = processProbe ?? new SystemHollowKnightProcessProbe();
    }

    public GlobalModSettingsFile ResolveFile(string instanceId, ModManifest mod)
    {
        var localLowPath = GetCurrentLocalLowPath(instanceId);
        return ResolveFileAtPath(localLowPath, mod);
    }

    public IReadOnlyList<GlobalModSettingsFile> ListFiles(
        string instanceId,
        IEnumerable<ModManifest> officialMods)
    {
        ArgumentNullException.ThrowIfNull(officialMods);
        var localLowPath = GetCurrentLocalLowPath(instanceId);
        var files = ResolveFiles(localLowPath, officialMods);
        return files.Where(file =>
            File.Exists(file.FilePath) || File.Exists(BackupPath(file.FilePath))).ToArray();
    }

    public async Task<int> DeleteAsync(
        string instanceId,
        IEnumerable<ModManifest> officialMods,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(officialMods);
        cancellationToken.ThrowIfCancellationRequested();

        var localLowPath = GetCurrentLocalLowPath(instanceId);
        var files = ResolveFiles(localLowPath, officialMods);
        var existing = files.Where(file =>
            File.Exists(file.FilePath) || File.Exists(BackupPath(file.FilePath))).ToArray();
        if (existing.Length == 0)
        {
            return 0;
        }

        var removals = existing
            .SelectMany(file => new[] { file.FilePath, BackupPath(file.FilePath) })
            .Where(File.Exists)
            .Select(path => Path.GetRelativePath(localLowPath, path).Replace('\\', '/'))
            .ToArray();
        var stagingPath = Path.Combine(
            isolation.StorageRoot,
            "global-mod-settings",
            $".{Guid.NewGuid():N}.staging");
        EnsureNoReparsePointAncestors(stagingPath);
        Directory.CreateDirectory(stagingPath);
        try
        {
            await FileTransaction.ReplaceDirectoryAsync(
                stagingPath,
                localLowPath,
                Path.Combine(isolation.StorageRoot, "transactions"),
                "delete-mod-global-settings",
                removals,
                cancellationToken);
            return existing.Length;
        }
        finally
        {
            LocalLowDirectory.DeleteIfExists(stagingPath);
            DeleteIfEmpty(Path.GetDirectoryName(stagingPath)!);
        }
    }

    private string GetCurrentLocalLowPath(string instanceId)
    {
        var instancePath = isolation.GetInstanceLocalLowPath(instanceId);
        return processProbe.IsRunning() ? isolation.SharedLocalLowPath : instancePath;
    }

    private static IReadOnlyList<GlobalModSettingsFile> ResolveFiles(
        string localLowPath,
        IEnumerable<ModManifest> officialMods)
    {
        var files = officialMods
            .Select(mod => ResolveFileAtPath(localLowPath, mod))
            .ToArray();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (!paths.Add(file.FilePath))
            {
                throw new InvalidDataException(
                    $"Multiple official mods resolve to the same global settings file: '{file.FilePath}'.");
            }
        }
        return files;
    }

    private static GlobalModSettingsFile ResolveFileAtPath(string localLowPath, ModManifest mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ValidateOfficialMod(mod);
        EnsureNoReparsePointAncestors(localLowPath);
        var relativePath = $"{mod.Name}.GlobalSettings.json";
        var filePath = LocalLowDirectory.ResolveUnderRoot(localLowPath, relativePath);
        EnsureNoReparsePointAncestors(filePath);
        return new GlobalModSettingsFile(mod.Id, mod.Name, filePath);
    }

    private static string BackupPath(string filePath) => filePath + ".bak";

    private static void ValidateOfficialMod(ModManifest mod)
    {
        if (!mod.Id.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(mod.SourceName, "HK ModLinks", StringComparison.Ordinal))
        {
            throw new ArgumentException("Global settings are only available for official HK ModLinks mods.", nameof(mod));
        }
        if (string.IsNullOrWhiteSpace(mod.Name)
            || mod.Name is "." or ".."
            || Path.IsPathFullyQualified(mod.Name)
            || !string.Equals(Path.GetFileName(mod.Name), mod.Name, StringComparison.Ordinal)
            || mod.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || mod.Name.EndsWith(' ') || mod.Name.EndsWith('.'))
        {
            throw new InvalidDataException($"Official mod name is unsafe for a settings file: '{mod.Name}'.");
        }
    }

    private static void EnsureNoReparsePointAncestors(string path)
    {
        for (var current = Path.GetFullPath(path);
             current is not null;
             current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Global settings path traverses a reparse point: '{current}'.");
            }
        }
    }

    private static void DeleteIfEmpty(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }
}
