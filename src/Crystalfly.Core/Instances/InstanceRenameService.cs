using Crystalfly.Core.Models;

namespace Crystalfly.Core.Instances;

public static class InstanceRenameService
{
    public static async Task<InstanceRecord> RenameAsync(
        InstanceRecord record,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(record.RootPath));
        var versionRoot = Directory.GetParent(source)?.FullName
            ?? throw new ArgumentException("Instance root must have a parent directory.", nameof(record));
        var sourceName = Path.GetFileName(source);
        if (!string.Equals(
                InstanceDirectory.ResolveUnderRoot(versionRoot, sourceName),
                source,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Instance root must be a direct child of the version root.");
        }
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Instance directory '{source}' was not found.");
        }
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Instance directory cannot be a reparse point.");
        }

        var name = newName.Trim();
        var destination = InstanceDirectory.ResolveUnderRoot(versionRoot, name);
        var existing = await InstanceSidecar.LoadAsync(source, cancellationToken);
        if (!string.Equals(existing.Id, record.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Instance sidecar does not match the selected instance.");
        }

        if (string.Equals(source, destination, StringComparison.Ordinal))
        {
            var renamedInPlace = existing with { Name = name, RootPath = source };
            await InstanceSidecar.SaveAsync(renamedInPlace, cancellationToken);
            return renamedInPlace;
        }
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)
            && (Directory.Exists(destination) || File.Exists(destination)))
        {
            throw new IOException($"Destination '{destination}' already exists.");
        }

        var intermediate = string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)
            ? InstanceDirectory.ResolveUnderRoot(versionRoot, $".crystalfly-rename-{Guid.NewGuid():N}")
            : destination;
        var moved = false;
        try
        {
            Directory.Move(source, intermediate);
            if (!string.Equals(intermediate, destination, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(intermediate, destination, StringComparison.Ordinal))
            {
                Directory.Move(intermediate, destination);
            }
            moved = true;
            var renamed = existing with { Name = name, RootPath = destination };
            await InstanceSidecar.SaveAsync(renamed, cancellationToken);
            return renamed;
        }
        catch
        {
            var current = moved || Directory.Exists(destination) ? destination : intermediate;
            if (Directory.Exists(current) && !Directory.Exists(source))
            {
                Directory.Move(current, source);
            }
            throw;
        }
    }
}
