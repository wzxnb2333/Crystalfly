using System.Collections.Concurrent;
using System.Text.Json;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Instances;

public static class InstanceImportService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> discoveryGates = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IReadOnlyList<InstanceRecord>> DiscoverAsync(
        string versionRoot,
        GameCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot));
        var gate = discoveryGates.GetOrAdd(root, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await DiscoverCoreAsync(root, catalog, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<IReadOnlyList<InstanceRecord>> DiscoverCoreAsync(
        string versionRoot, GameCatalog catalog, CancellationToken cancellationToken)
    {
        var instances = new List<InstanceRecord>();
        var issues = new List<InstanceDiscoveryIssue>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in VersionDirectoryScanner.Scan(versionRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                InstanceDirectory.RejectReparseAncestors(path);
                if (!GameDirectoryIntegrityChecker.Inspect(path).IsValid)
                {
                    continue;
                }

                var existing = await InstanceSidecar.LoadStoredAsync(path, cancellationToken);
                if (existing is null)
                {
                    existing = await RecreateFromMarkerAsync(path, catalog, cancellationToken);
                }
                else if (identities.Contains(existing.Id)
                    || await HasDifferentOwnerAsync(existing, path, cancellationToken))
                {
                    existing = await RegisterAsync(path, instanceId: null, catalog, cancellationToken);
                }
                else
                {
                    existing = await RefreshBuildIdentityAsync(
                        existing with { RootPath = path }, catalog, cancellationToken);
                }
                instances.Add(existing);
                identities.Add(existing.Id);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidDataException or ArgumentException or JsonException)
            {
                issues.Add(new InstanceDiscoveryIssue(path, exception));
            }
        }
        return new InstanceDiscoveryResult(instances, issues);
    }

    private static async Task<bool> HasDifferentOwnerAsync(
        InstanceRecord record,
        string scannedPath,
        CancellationToken cancellationToken)
    {
        var recordedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(record.RootPath));
        if (string.Equals(recordedPath, scannedPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(recordedPath), Path.GetDirectoryName(scannedPath), StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(recordedPath))
        {
            return false;
        }
        return string.Equals(
            await InstanceSidecar.ReadMarkerInstanceIdAsync(recordedPath, cancellationToken),
            record.Id,
            StringComparison.Ordinal);
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

    private static async Task<InstanceRecord> RefreshBuildIdentityAsync(
        InstanceRecord record,
        GameCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (catalog.Builds.Count == 0)
        {
            return record;
        }
        var fingerprint = await BuildFingerprintService.CalculateAsync(record.RootPath, cancellationToken);
        var verified = BuildFingerprintService.FindBuild(catalog.Builds, fingerprint);
        var buildId = verified?.Id
            ?? (BuildIdentity.TryGetSteamManifestId(record.BuildId, out _) ? record.BuildId : "unknown");
        if (string.Equals(record.BuildId, buildId, StringComparison.Ordinal))
        {
            return record;
        }

        var upgraded = record with { BuildId = buildId };
        await InstanceSidecar.SaveAsync(upgraded, cancellationToken);
        return upgraded;
    }
}
