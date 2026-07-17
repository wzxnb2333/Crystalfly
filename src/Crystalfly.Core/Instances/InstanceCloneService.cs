using Crystalfly.Core.Models;

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
        _ = InstanceSidecar.GetMetadataPath(sourceRoot, instanceId);
        var versionRoot = Directory.GetParent(sourceRoot)?.FullName
            ?? throw new ArgumentException("Source root must have a parent directory.", nameof(sourceRoot));
        var destinationRoot = InstanceDirectory.ResolveUnderRoot(versionRoot, name);
        if (Directory.Exists(destinationRoot) || File.Exists(destinationRoot))
        {
            throw new IOException($"Destination '{destinationRoot}' already exists.");
        }

        var sourceRecord = await InstanceSidecar.LoadAsync(sourceRoot, cancellationToken);
        var stagingRoot = Path.Combine(versionRoot, ".crystalfly", "staging", $"clone-{Guid.NewGuid():N}");
        var destinationOwned = false;
        try
        {
            await CopyDirectoryAsync(sourceRoot, stagingRoot, cancellationToken);
            Directory.Move(stagingRoot, destinationRoot);
            destinationOwned = true;

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
