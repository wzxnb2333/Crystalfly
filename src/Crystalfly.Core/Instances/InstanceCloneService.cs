using Crystalfly.Core.Models;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Instances;

public static class InstanceCloneService
{
    public static async Task<InstanceRecord> CloneAsync(
        string sourceRoot,
        string name,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        sourceRoot = Path.GetFullPath(sourceRoot);
        InstanceDirectory.RejectReparseAncestors(sourceRoot);
        _ = InstanceSidecar.GetMetadataPath(sourceRoot, instanceId);
        var versionRoot = Directory.GetParent(sourceRoot)?.FullName
            ?? throw new ArgumentException("Source root must have a parent directory.", nameof(sourceRoot));
        var destinationRoot = InstanceDirectory.ResolveUnderRoot(versionRoot, name);
        if (Directory.Exists(destinationRoot) || File.Exists(destinationRoot))
        {
            throw new IOException($"Destination '{destinationRoot}' already exists.");
        }

        var sourceRecord = await InstanceSidecar.LoadAsync(sourceRoot, cancellationToken)
            ?? throw new InvalidDataException(
                $"Instance sidecar metadata is missing; expected '{InstanceSidecar.GetMarkerPath(sourceRoot)}'. "
                + "The instance may be corrupted.");
        var stagingRoot = Path.Combine(versionRoot, ".crystalfly", "staging", $"clone-{Guid.NewGuid():N}");
        var gameDataRoot = new CrystalflyPaths(versionRoot, false).GetVersionDataRoot(versionRoot, sourceRecord.GameId);
        var sourceState = InstanceDirectory.ResolveUnderRoot(Path.Combine(gameDataRoot, "instances"), sourceRecord.Id);
        var clonedState = InstanceDirectory.ResolveUnderRoot(Path.Combine(gameDataRoot, "instances"), instanceId);
        var stagingState = stagingRoot + "-state";
        var copyGameState = sourceRecord.GameId == GameId.Silksong && Directory.Exists(sourceState);
        if (copyGameState && (Directory.Exists(clonedState) || File.Exists(clonedState)))
            throw new IOException("Clone save state already exists.");
        var destinationOwned = false;
        var stateOwned = false;
        try
        {
            await CopyDirectoryAsync(sourceRoot, stagingRoot, cancellationToken);
            if (copyGameState)
            {
                InstanceDirectory.RejectReparseAncestors(sourceState);
                InstanceDirectory.RejectReparseAncestors(clonedState);
                await CopyDirectoryAsync(sourceState, stagingState, cancellationToken);
                var snapshots = Path.Combine(stagingState, "snapshots");
                if (Directory.Exists(snapshots))
                    foreach (var directory in Directory.EnumerateDirectories(snapshots))
                    {
                        var metadata = Path.Combine(directory, "snapshot.json");
                        var snapshot = await AtomicJsonStore.ReadAsync<NamedSnapshot>(metadata, cancellationToken);
                        if (snapshot.GameId != sourceRecord.GameId || snapshot.Id != Path.GetFileName(directory))
                            throw new InvalidDataException("Invalid snapshot in clone source.");
                        var copied = snapshot with { SourcePath = Path.Combine(clonedState, "local-low"),
                            SnapshotPath = Path.Combine(clonedState, "snapshots", snapshot.Id, "data") };
                        await AtomicJsonStore.WriteAsync(metadata, copied, cancellationToken);
                        await AtomicJsonStore.WriteAsync(metadata, copied, cancellationToken);
                    }
            }
            if (sourceRecord.GameId == GameId.Silksong && !File.Exists(Path.Combine(stagingRoot, "steam_appid.txt")))
                await File.WriteAllTextAsync(Path.Combine(stagingRoot, "steam_appid.txt"), GameDefinition.Silksong.SteamAppId.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
            Directory.Move(stagingRoot, destinationRoot);
            destinationOwned = true;
            if (copyGameState)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(clonedState)!);
                Directory.Move(stagingState, clonedState);
                stateOwned = true;
            }

            var clone = sourceRecord with
            {
                Id = instanceId,
                Name = name,
                RootPath = destinationRoot,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await InstanceSidecar.SaveAsync(clone, cancellationToken);
            return clone;
        }
        catch
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }

            if (destinationOwned && Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, recursive: true);
            }
            if (stateOwned && Directory.Exists(clonedState)) Directory.Delete(clonedState, recursive: true);
            if (Directory.Exists(stagingState)) Directory.Delete(stagingState, recursive: true);

            throw;
        }
    }

    private static async Task CopyDirectoryAsync(
        string sourceRoot,
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(file);
            if (string.Equals(file, InstanceSidecar.GetMarkerPath(sourceRoot), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Cannot clone reparse point '{path}'.");
        }
    }
}
