using Crystalfly.Core.Models;

namespace Crystalfly.Core.Instances;

public static class InstanceImportService
{
    public static async Task<IReadOnlyList<InstanceRecord>> DiscoverAsync(
        string versionRoot,
        GameCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var instances = new List<InstanceRecord>();
        foreach (var path in VersionDirectoryScanner.Scan(versionRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(Path.Combine(path, InstanceDirectory.PendingDownloadMarkerFileName)))
            {
                continue;
            }
            if (!File.Exists(Path.Combine(path, "hollow_knight.exe"))
                || !File.Exists(Path.Combine(path, "hollow_knight_Data", "globalgamemanagers")))
            {
                continue;
            }

            if (File.Exists(InstanceSidecar.GetMarkerPath(path)))
            {
                var existing = await InstanceSidecar.LoadAsync(path, cancellationToken);
                existing = await UpgradeVerifiedSteamManifestAsync(existing, catalog, cancellationToken);
                instances.Add(existing);
                continue;
            }

            var fingerprint = await BuildFingerprintService.CalculateAsync(path, cancellationToken);
            var build = BuildFingerprintService.FindBuild(catalog.Builds, fingerprint);
            var record = new InstanceRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Path.GetFileName(path),
                RootPath = path,
                BuildId = build?.Id ?? "unknown",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await InstanceSidecar.SaveAsync(record, cancellationToken);
            instances.Add(record);
        }
        return instances;
    }

    private static async Task<InstanceRecord> UpgradeVerifiedSteamManifestAsync(
        InstanceRecord record,
        GameCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (!BuildIdentity.TryGetSteamManifestId(record.BuildId, out var manifestId))
        {
            return record;
        }

        var candidate = catalog.Builds.FirstOrDefault(build =>
            string.Equals(
                build.ManifestId,
                manifestId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
        if (candidate is null)
        {
            return record;
        }

        var fingerprint = await BuildFingerprintService.CalculateAsync(record.RootPath, cancellationToken);
        if (BuildFingerprintService.FindBuild([candidate], fingerprint) is null)
        {
            return record;
        }

        var upgraded = record with { BuildId = candidate.Id };
        await InstanceSidecar.SaveAsync(upgraded, cancellationToken);
        return upgraded;
    }
}
