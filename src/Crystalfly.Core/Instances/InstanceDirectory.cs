namespace Crystalfly.Core.Instances;

public static class InstanceDirectory
{
    public const string PendingDownloadMarkerFileName = ".crystalfly-download.json";

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM\u00B9", "COM\u00B2", "COM\u00B3",
        "LPT\u00B9", "LPT\u00B2", "LPT\u00B3"
    ];

    public static string ResolveUnderRoot(string versionRoot, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionRoot);
        ValidateName(name);

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot));
        var destination = Path.GetFullPath(Path.Combine(fullRoot, name));
        if (!string.Equals(Path.GetDirectoryName(destination), fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Instance name must resolve to a direct child of the version root.", nameof(name));
        }

        return destination;
    }

    public static void PrepareRegistration(string instancePath)
    {
        var instanceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(instancePath));
        RejectReparseAncestors(instanceRoot);
        if (!GameDirectoryIntegrityChecker.Inspect(instanceRoot).IsValid)
        {
            throw new InvalidDataException("The selected directory is not a complete game installation.");
        }
        var versionRoot = Path.GetDirectoryName(instanceRoot)
            ?? throw new ArgumentException("Instance root must have a parent directory.", nameof(instancePath));
        _ = ResolveUnderRoot(versionRoot, Path.GetFileName(instanceRoot));
        var metadataRoot = Path.Combine(versionRoot, ".crystalfly");
        RejectReparseAncestors(metadataRoot);
        Directory.CreateDirectory(metadataRoot);
        foreach (var directory in new[] { instanceRoot, metadataRoot })
        {
            using var probe = new FileStream(
                Path.Combine(directory, $".crystalfly-write-check-{Guid.NewGuid():N}"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
    }

    /// <summary>
    /// Prepares a version root and its managed metadata directory. New roots
    /// must be empty except for <c>.crystalfly</c> metadata; a root already
    /// registered in settings may contain instances of another game.
    /// </summary>
    public static void PrepareVersionRootRegistration(string versionRoot, bool isRegisteredVersionRoot = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionRoot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot));
        RejectReparseAncestors(root);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"The version root '{root}' does not exist.");
        }
        if (GameDirectoryIntegrityChecker.Inspect(root).IsValid)
        {
            throw new InvalidDataException(
                "The selected directory is a complete game installation, not a version root.");
        }
        if (!isRegisteredVersionRoot && !IsEmptyVersionRoot(root))
        {
            throw new InvalidDataException(
                "The selected directory is not empty. A new version root must be an empty folder.");
        }
        if (IsNestedInVersionRoot(root))
        {
            throw new InvalidDataException(
                "The selected directory is nested inside an existing version root.");
        }
        var metadataRoot = Path.Combine(root, ".crystalfly");
        RejectReparseAncestors(metadataRoot);
        Directory.CreateDirectory(metadataRoot);
        using var probe = new FileStream(
            Path.Combine(metadataRoot, $".crystalfly-write-check-{Guid.NewGuid():N}"),
            FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    /// <summary>
    /// Returns whether a folder can become a new version root: it must exist and
    /// contain no entries other than an already present <c>.crystalfly</c>
    /// metadata directory.
    /// </summary>
    public static bool IsEmptyVersionRoot(string versionRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionRoot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot));
        if (!Directory.Exists(root))
        {
            return false;
        }
        return !Directory.EnumerateFileSystemEntries(root).Any(entry =>
            !string.Equals(
                Path.GetFileName(entry),
                ".crystalfly",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns whether a folder is nested inside another folder that already has
    /// Crystalfly metadata, which means it belongs to an existing version root.
    /// </summary>
    public static bool IsNestedInVersionRoot(string versionRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionRoot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot));
        for (var ancestor = Path.GetDirectoryName(root);
             ancestor is not null;
             ancestor = Path.GetDirectoryName(ancestor))
        {
            if (Directory.Exists(Path.Combine(ancestor, ".crystalfly")))
            {
                return true;
            }
        }
        return false;
    }

    public static void RejectReparseAncestors(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Managed game paths cannot traverse reparse point '{current}'.");
            }
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name is "." or ".." ||
            Path.IsPathFullyQualified(name) ||
            !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Length > 255 ||
            name.EndsWith(' ') ||
            name.EndsWith('.') ||
            string.Equals(name, ".crystalfly", StringComparison.OrdinalIgnoreCase) ||
            IsReservedName(name))
        {
            throw new ArgumentException("Instance name must be a single valid Windows directory name.", nameof(name));
        }
    }

    private static bool IsReservedName(string name)
    {
        var dot = name.IndexOf('.');
        var stem = name.AsSpan(0, dot >= 0 ? dot : name.Length);
        foreach (var reservedName in ReservedNames)
        {
            if (stem.Equals(reservedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
