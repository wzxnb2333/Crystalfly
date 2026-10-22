namespace Crystalfly.Core.Diagnostics;

/// <summary>Diagnostics never traverse a reparse point or an unbounded directory tree.</summary>
public static class DiagnosticFiles
{
    public static bool IsSafe(string directory)
    {
        for (var item = new DirectoryInfo(Path.GetFullPath(directory)); item is not null; item = item.Parent)
        {
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }
        return true;
    }

    public static IReadOnlyList<FileInfo> GetFiles(string directory)
    {
        if (!IsSafe(directory))
        {
            throw new IOException("Unsafe diagnostics directory.");
        }
        var files = new List<FileInfo>();
        if (!Directory.Exists(directory)) return files;
        var pending = new Queue<(DirectoryInfo Directory, int Depth)>();
        var visited = 0;
        pending.Enqueue((new DirectoryInfo(directory), 0));
        while (pending.TryDequeue(out var current))
        {
            foreach (var entry in current.Directory.EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0
                    || ++visited > 256 || current.Depth > 4)
                {
                    throw new IOException("Unsafe diagnostics cache.");
                }
                if (entry is DirectoryInfo child) pending.Enqueue((child, current.Depth + 1));
                else if (entry is FileInfo file) files.Add(file);
            }
        }
        return files;
    }
}
