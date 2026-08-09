using System.Text.Json;
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
                if (existing is null)
                {
                    existing = await RecreateFromMarkerAsync(path, catalog, cancellationToken);
                }
                else
                {
                    existing = await UpgradeVerifiedSteamManifestAsync(existing, catalog, cancellationToken);
                }
                instances.Add(existing);
                continue;
            }

            instances.Add(await RegisterAsync(path, instanceId: null, catalog, cancellationToken));
        }
        return instances;
    }

    private static async Task<InstanceRecord> RecreateFromMarkerAsync(
        string path,
        GameCatalog catalog,
        CancellationToken cancellationToken)
    {
        string? instanceId;
        try
        {
            instanceId = await InstanceSidecar.ReadMarkerInstanceIdAsync(path, cancellationToken);
        }
        catch (JsonException)
        {
            instanceId = null;
        }
        return await RegisterAsync(path, instanceId, catalog, cancellationToken);
    }

    private static async Task<InstanceRecord> RegisterAsync(
        string path,
        string? instanceId,
        GameCatalog catalog,
        CancellationToken cancellationToken)
    {
        var fingerprint = await BuildFingerprintService.CalculateAsync(path, cancellationToken);
        var build = BuildFingerprintService.FindBuild(catalog.Builds, fingerprint);
        var record = new InstanceRecord
        {
            Id = instanceId ?? Guid.NewGuid().ToString("N"),
            Name = Path.GetFileName(path),
            RootPath = path,
            BuildId = build?.Id ?? "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record, cancellationToken);
        return record;
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
