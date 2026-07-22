namespace Crystalfly.Core.Mods;

internal sealed class ModPathPolicy
{
    private readonly string instanceRoot;
    private readonly string managedRoot;
    private readonly string managedDisabledRoot;
    private readonly string bepInExPluginsRoot;
    private readonly string bepInExDisabledRoot;
    private readonly IReadOnlyList<string> recognizedRoots;
    private readonly IReadOnlySet<string> sharedRoots;

    public ModPathPolicy(string instanceRoot)
    {
        this.instanceRoot = Trim(Path.GetFullPath(instanceRoot));
        managedRoot = ResolveFromInstance("hollow_knight_Data/Managed/Mods");
        managedDisabledRoot = ResolveFromInstance("hollow_knight_Data/Managed/Mods/Disabled");
        bepInExPluginsRoot = ResolveFromInstance("BepInEx/plugins");
        bepInExDisabledRoot = ResolveFromInstance("BepInEx/Disabled");
        recognizedRoots = [managedRoot, bepInExPluginsRoot, bepInExDisabledRoot];
        sharedRoots = new HashSet<string>(
            [managedRoot, managedDisabledRoot, bepInExPluginsRoot, bepInExDisabledRoot],
            StringComparer.OrdinalIgnoreCase);
    }

    public ResolvedModPath ResolveRecognized(string relativePath)
    {
        var resolved = ResolveUnderInstance(relativePath);
        if (!recognizedRoots.Any(root => IsAtOrUnder(resolved.FullPath, root)))
        {
            throw new InvalidDataException($"Mod path is not under a recognized root: '{relativePath}'.");
        }
        return resolved;
    }

    public ResolvedModPath ResolveUnderInstance(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        return new ResolvedModPath(normalized, ResolveFromInstance(normalized));
    }

    public ResolvedModPath ResolveUnderInstallRoot(
        string relativePath,
        ResolvedModPath installRoot)
    {
        var resolved = ResolveRecognized(relativePath);
        if (!IsAtOrUnder(resolved.FullPath, installRoot.FullPath))
        {
            throw new InvalidDataException(
                $"Mod path escapes its install root: '{relativePath}'.");
        }
        return resolved;
    }

    public ResolvedModPath ResolveUnderOwnedRoot(
        string relativePath,
        ResolvedModPath installRoot)
    {
        var resolved = ResolveUnderInstance(relativePath);
        if (!IsAtOrUnder(resolved.FullPath, installRoot.FullPath))
        {
            throw new InvalidDataException(
                $"Mod path escapes its owned root: '{relativePath}'.");
        }
        return resolved;
    }

    public bool GetEnabledPlacement(ResolvedModPath path)
    {
        if (IsAtOrUnder(path.FullPath, managedDisabledRoot)
            || IsAtOrUnder(path.FullPath, bepInExDisabledRoot))
        {
            return false;
        }
        if (IsAtOrUnder(path.FullPath, managedRoot)
            || IsAtOrUnder(path.FullPath, bepInExPluginsRoot))
        {
            return true;
        }
        throw new InvalidDataException($"Mod path is not under a recognized root: '{path.RelativePath}'.");
    }

    public bool IsSharedRoot(ResolvedModPath root) => sharedRoots.Contains(Trim(root.FullPath));

    public void EnsureNoReparsePoints(string fullPath)
    {
        if (HasReparsePoint(fullPath))
        {
            throw new InvalidDataException(
                $"Mod path traverses a reparse point: '{ToRelativePath(fullPath)}'.");
        }
    }

    public bool HasReparsePoint(string fullPath)
    {
        var resolved = Trim(Path.GetFullPath(fullPath));
        if (!IsAtOrUnder(resolved, instanceRoot))
        {
            throw new InvalidDataException("Mod path escapes the instance root.");
        }
        var current = instanceRoot;
        if (IsReparsePoint(current))
        {
            return true;
        }
        var relative = Path.GetRelativePath(instanceRoot, resolved);
        foreach (var segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
            {
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<string> EnumerateFilesSafely(
        string fullRoot,
        bool rejectReparsePoints)
    {
        var root = Trim(Path.GetFullPath(fullRoot));
        if (!IsAtOrUnder(root, instanceRoot))
        {
            throw new InvalidDataException("Mod scan root escapes the instance root.");
        }
        if (HasReparsePoint(root))
        {
            if (rejectReparsePoints)
            {
                throw new InvalidDataException(
                    $"Mod scan root traverses a reparse point: '{ToRelativePath(root)}'.");
            }
            return [];
        }
        if (!Directory.Exists(root))
        {
            return [];
        }

        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var current))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(
                         current, "*", SearchOption.TopDirectoryOnly))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    if (rejectReparsePoints)
                    {
                        throw new InvalidDataException(
                            $"Mod scan encountered a reparse point: '{ToRelativePath(entry)}'.");
                    }
                    continue;
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }
        }
        return files;
    }

    public string ToRelativePath(string fullPath) =>
        NormalizeSeparators(Path.GetRelativePath(instanceRoot, Path.GetFullPath(fullPath)));

    public static string NormalizeRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = NormalizeSeparators(relativePath);
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"Mod path must be relative: '{relativePath}'.");
        }
        var segments = normalized.Trim('/').Split('/', StringSplitOptions.None);
        if (segments.Length == 0
            || segments.Any(segment =>
                segment.Length == 0
                || segment is "." or ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || segment.EndsWith(".", StringComparison.Ordinal)
                || char.IsWhiteSpace(segment[^1])))
        {
            throw new InvalidDataException($"Mod path contains an unsafe segment: '{relativePath}'.");
        }
        return string.Join('/', segments);
    }

    private string ResolveFromInstance(string relativePath)
    {
        var fullPath = Trim(Path.GetFullPath(Path.Combine(
            instanceRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar))));
        if (!IsAtOrUnder(fullPath, instanceRoot))
        {
            throw new InvalidDataException($"Mod path escapes the instance root: '{relativePath}'.");
        }
        return fullPath;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool IsAtOrUnder(string path, string root)
    {
        var fullPath = Trim(Path.GetFullPath(path));
        var fullRoot = Trim(Path.GetFullPath(root));
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSeparators(string path) => path.Replace('\\', '/');

    private static string Trim(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

internal readonly record struct ResolvedModPath(string RelativePath, string FullPath);
