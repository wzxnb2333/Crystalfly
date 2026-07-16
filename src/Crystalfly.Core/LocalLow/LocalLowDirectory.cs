using System.Security.Cryptography;
using System.Text;

namespace Crystalfly.Core.LocalLow;

internal static class LocalLowDirectory
{
    public static async Task CopyAsync(
        string sourceRoot,
        string destinationRoot,
        bool includeLogs,
        CancellationToken cancellationToken)
    {
        sourceRoot = ExistingDirectory(sourceRoot);
        destinationRoot = Path.GetFullPath(destinationRoot);
        if (Directory.Exists(destinationRoot) || File.Exists(destinationRoot))
        {
            throw new IOException($"Destination already exists: '{destinationRoot}'.");
        }

        Directory.CreateDirectory(destinationRoot);
        try
        {
            var entries = EnumerateTree(sourceRoot);
            foreach (var directory in entries
                .Where(entry => entry.IsDirectory)
                .OrderBy(entry => entry.RelativePath.Count(character => character == '/'))
                .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (includeLogs || !IsLogPath(directory.RelativePath, isDirectory: true))
                {
                    Directory.CreateDirectory(ResolveUnderRoot(destinationRoot, directory.RelativePath));
                }
            }

            foreach (var file in entries
                .Where(entry => !entry.IsDirectory)
                .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!includeLogs && IsLogPath(file.RelativePath, isDirectory: false))
                {
                    continue;
                }

                var destination = ResolveUnderRoot(destinationRoot, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var input = new FileStream(
                    file.Path,
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
        catch
        {
            DeleteIfExists(destinationRoot);
            throw;
        }
    }

    public static async Task<string> HashAsync(
        string root,
        bool includeLogs,
        CancellationToken cancellationToken)
    {
        root = ExistingDirectory(root);
        RejectReparsePoint(root);
        using var directoryHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var entries = EnumerateTree(root);

        var directories = entries
            .Where(entry => entry.IsDirectory)
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ToArray();
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (includeLogs || !IsLogPath(directory.RelativePath, isDirectory: true))
            {
                Append(directoryHash, $"D\0{directory.RelativePath}\n");
            }
        }

        var files = entries
            .Where(entry => !entry.IsDirectory)
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ToArray();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!includeLogs && IsLogPath(file.RelativePath, isDirectory: false))
            {
                continue;
            }

            var fileInfo = new FileInfo(file.Path);
            await using var stream = new FileStream(
                file.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var fileHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            Append(directoryHash, $"F\0{file.RelativePath}\0{fileInfo.Length}\0{fileHash}\n");
        }

        return Convert.ToHexString(directoryHash.GetHashAndReset());
    }

    public static void DeleteIfExists(string path)
    {
        path = Path.GetFullPath(path);
        if (!Directory.Exists(path))
        {
            return;
        }

        RejectReparsePoint(path);
        _ = EnumerateTree(path);
        Directory.Delete(path, recursive: true);
    }

    public static string ResolveUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(
            fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes its root: '{relativePath}'.");
        }
        return path;
    }

    public static bool PathEquals(string left, string right) => string.Equals(
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);

    private static bool IsLogPath(string relativePath, bool isDirectory)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var directorySegments = isDirectory ? segments : segments[..^1];
        if (directorySegments.Any(segment => segment.Equals("logs", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        if (isDirectory || segments.Length == 0)
        {
            return false;
        }

        var fileName = segments[^1];
        return Path.GetExtension(fileName).Equals(".log", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("output_log.txt", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Player.log", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Player-prev.log", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("ModLog.txt", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExistingDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException($"Directory does not exist: '{fullPath}'.");
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"LocalLow data cannot contain reparse point '{path}'.");
        }
    }

    private static void Append(IncrementalHash hash, string value) =>
        hash.AppendData(Encoding.UTF8.GetBytes(value));

    private static IReadOnlyList<TreeEntry> EnumerateTree(string root)
    {
        var entries = new List<TreeEntry>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            var directory = pending.Pop();
            foreach (var path in Directory
                .EnumerateFileSystemEntries(directory)
                .OrderDescending(StringComparer.OrdinalIgnoreCase))
            {
                RejectReparsePoint(path);
                var attributes = File.GetAttributes(path);
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                entries.Add(new TreeEntry(
                    path,
                    Normalize(Path.GetRelativePath(root, path)),
                    isDirectory));
                if (isDirectory)
                {
                    pending.Push(path);
                }
            }
        }
        return entries;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private sealed record TreeEntry(string Path, string RelativePath, bool IsDirectory);
}
