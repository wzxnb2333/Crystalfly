using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Transactions;

public static class FileTransaction
{
    private const string JournalFileName = "journal.json";

    public static Task<TransactionJournal> ApplyDirectoryAsync(
        string stagingRoot,
        string targetRoot,
        string journalRoot,
        string operation,
        CancellationToken cancellationToken = default) =>
        ReplaceDirectoryAsync(
            stagingRoot, targetRoot, journalRoot, operation, [], cancellationToken);

    public static async Task<TransactionJournal> ReplaceDirectoryAsync(
        string stagingRoot,
        string targetRoot,
        string journalRoot,
        string operation,
        IEnumerable<string> removeRelativePaths,
        CancellationToken cancellationToken = default)
    {
        var staging = ExistingDirectory(stagingRoot, nameof(stagingRoot));
        var target = Path.GetFullPath(targetRoot);
        var journals = Path.GetFullPath(journalRoot);
        EnsureSeparateRoots(staging, target);
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(journals);

        var files = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .Select(path => new StagedFile(path, NormalizeRelativePath(Path.GetRelativePath(staging, path))))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ValidateDestinations(target, files);
        var stagedPaths = files.Select(file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removals = removeRelativePaths
            .Select(NormalizeRelativePath)
            .Where(path => !stagedPaths.Contains(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ValidateRemovals(target, removals);

        var id = Guid.NewGuid().ToString("N");
        var restorePoint = Path.Combine(journals, id);
        var journalPath = Path.Combine(restorePoint, JournalFileName);
        Directory.CreateDirectory(restorePoint);
        var journal = new TransactionJournal
        {
            Id = id,
            Operation = operation,
            State = TransactionState.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            RootPath = target,
            RestorePointPath = restorePoint
        };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);

        try
        {
            journal = journal with { State = TransactionState.Applying };
            await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                journal = await ApplyFileAsync(file, target, journal, journalPath, cancellationToken);
            }
            foreach (var relativePath in removals)
            {
                cancellationToken.ThrowIfCancellationRequested();
                journal = await DeleteFileAsync(relativePath, target, journal, journalPath, cancellationToken);
            }

            journal = journal with { State = TransactionState.Committed };
            await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
        }
        catch
        {
            journal = await RollBackAsync(journal, journalPath, CancellationToken.None);
            if (journal.State == TransactionState.RolledBack)
            {
                TryDeleteRestorePoint(restorePoint);
            }
            throw;
        }
        TryDeleteRestorePoint(restorePoint);
        return journal;
    }

    public static async Task<IReadOnlyList<TransactionJournal>> RecoverPendingAsync(
        string journalRoot,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(journalRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var results = new List<TransactionJournal>();
        foreach (var journalPath in Directory
            .EnumerateFiles(root, JournalFileName, SearchOption.AllDirectories)
            .ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var journal = await AtomicJsonStore.ReadAsync<TransactionJournal>(journalPath, cancellationToken);
            switch (journal.State)
            {
                case TransactionState.Prepared:
                case TransactionState.Applying:
                case TransactionState.RollingBack:
                    journal = await RollBackAsync(journal, journalPath, CancellationToken.None);
                    if (journal.State == TransactionState.RolledBack)
                    {
                        TryDeleteRestorePoint(journal.RestorePointPath);
                    }
                    break;
                case TransactionState.Committed:
                case TransactionState.RolledBack:
                    TryDeleteRestorePoint(journal.RestorePointPath);
                    break;
                case TransactionState.NeedsAttention:
                    break;
                default:
                    throw new InvalidDataException($"Unknown transaction state: {journal.State}.");
            }
            results.Add(journal);
        }
        return results;
    }

    private static async Task<TransactionJournal> ApplyFileAsync(
        StagedFile file,
        string targetRoot,
        TransactionJournal journal,
        string journalPath,
        CancellationToken cancellationToken)
    {
        var targetPath = ResolveUnderRoot(targetRoot, file.RelativePath);
        var appliedHash = await HashFileAsync(file.SourcePath, cancellationToken);
        var createdPaths = journal.CreatedPaths.ToList();
        var createdDirectories = journal.CreatedDirectories.ToList();
        var backupPaths = new Dictionary<string, string>(journal.BackupPaths, StringComparer.OrdinalIgnoreCase);
        var changes = journal.Changes.ToList();

        var parent = Path.GetDirectoryName(targetPath)!;
        foreach (var directory in GetMissingDirectories(targetRoot, parent))
        {
            createdPaths.Add(directory);
            createdDirectories.Add(directory);
        }

        string? backupRelativePath = null;
        string? originalHash = null;
        if (File.Exists(targetPath))
        {
            originalHash = await HashFileAsync(targetPath, cancellationToken);
            backupRelativePath = NormalizeRelativePath(Path.Combine("backup", file.RelativePath));
            var backupPath = ResolveUnderRoot(journal.RestorePointPath, backupRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.Copy(targetPath, backupPath, overwrite: false);
            if (!StringComparer.OrdinalIgnoreCase.Equals(originalHash, await HashFileAsync(backupPath, cancellationToken)))
            {
                throw new IOException($"Backup verification failed for '{file.RelativePath}'.");
            }
            backupPaths[targetPath] = backupPath;
        }
        else
        {
            createdPaths.Add(targetPath);
        }

        changes.Add(new TransactionFileChange
        {
            RelativePath = file.RelativePath,
            BackupRelativePath = backupRelativePath,
            OriginalSha256 = originalHash,
            AppliedSha256 = appliedHash
        });
        journal = journal with
        {
            CreatedPaths = createdPaths,
            CreatedDirectories = createdDirectories,
            BackupPaths = backupPaths,
            Changes = changes
        };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);

        foreach (var directory in createdDirectories.Where(directory => !Directory.Exists(directory)))
        {
            Directory.CreateDirectory(directory);
        }
        if (originalHash is not null)
        {
            File.Delete(targetPath);
        }
        File.Copy(file.SourcePath, targetPath, overwrite: false);
        if (!StringComparer.OrdinalIgnoreCase.Equals(appliedHash, await HashFileAsync(targetPath, cancellationToken)))
        {
            throw new IOException($"Write verification failed for '{file.RelativePath}'.");
        }
        return journal;
    }

    private static async Task<TransactionJournal> DeleteFileAsync(
        string relativePath,
        string targetRoot,
        TransactionJournal journal,
        string journalPath,
        CancellationToken cancellationToken)
    {
        var targetPath = ResolveUnderRoot(targetRoot, relativePath);
        var originalHash = await HashFileAsync(targetPath, cancellationToken);
        var backupRelativePath = NormalizeRelativePath(Path.Combine("backup", relativePath));
        var backupPath = ResolveUnderRoot(journal.RestorePointPath, backupRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.Copy(targetPath, backupPath, overwrite: false);
        if (!StringComparer.OrdinalIgnoreCase.Equals(originalHash, await HashFileAsync(backupPath, cancellationToken)))
        {
            throw new IOException($"Backup verification failed for '{relativePath}'.");
        }

        var backupPaths = new Dictionary<string, string>(journal.BackupPaths, StringComparer.OrdinalIgnoreCase)
        {
            [targetPath] = backupPath
        };
        var changes = journal.Changes.Append(new TransactionFileChange
        {
            RelativePath = relativePath,
            IsDeletion = true,
            BackupRelativePath = backupRelativePath,
            OriginalSha256 = originalHash,
            AppliedSha256 = originalHash
        }).ToArray();
        journal = journal with { BackupPaths = backupPaths, Changes = changes };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);

        File.Delete(targetPath);
        if (File.Exists(targetPath))
        {
            throw new IOException($"Delete verification failed for '{relativePath}'.");
        }
        return journal;
    }

    private static async Task<TransactionJournal> RollBackAsync(
        TransactionJournal journal,
        string journalPath,
        CancellationToken cancellationToken)
    {
        journal = journal with { State = TransactionState.RollingBack, Error = null };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
        try
        {
            ValidateJournal(journal, journalPath);
            foreach (var change in journal.Changes.Reverse())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = ResolveUnderRoot(journal.RootPath, change.RelativePath);
                if (change.OriginalSha256 is not null)
                {
                    if (!File.Exists(targetPath)
                        || !StringComparer.OrdinalIgnoreCase.Equals(
                            change.OriginalSha256,
                            await HashFileAsync(targetPath, cancellationToken)))
                    {
                        if (change.BackupRelativePath is null)
                        {
                            throw new InvalidDataException($"Missing backup record for '{change.RelativePath}'.");
                        }
                        var backupPath = ResolveUnderRoot(journal.RestorePointPath, change.BackupRelativePath);
                        if (!File.Exists(backupPath)
                            || !StringComparer.OrdinalIgnoreCase.Equals(
                                change.OriginalSha256,
                                await HashFileAsync(backupPath, cancellationToken)))
                        {
                            throw new InvalidDataException($"Backup is missing or invalid for '{change.RelativePath}'.");
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.Copy(backupPath, targetPath, overwrite: true);
                    }
                    if (!StringComparer.OrdinalIgnoreCase.Equals(
                        change.OriginalSha256,
                        await HashFileAsync(targetPath, cancellationToken)))
                    {
                        throw new IOException($"Restore verification failed for '{change.RelativePath}'.");
                    }
                }
                else if (File.Exists(targetPath))
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(
                        change.AppliedSha256,
                        await HashFileAsync(targetPath, cancellationToken)))
                    {
                        throw new InvalidDataException($"New file changed outside the transaction: '{change.RelativePath}'.");
                    }
                    File.Delete(targetPath);
                }
                else if (Directory.Exists(targetPath))
                {
                    throw new InvalidDataException($"Expected a new file but found a directory: '{change.RelativePath}'.");
                }
            }

            foreach (var directory in journal.CreatedDirectories
                .OrderByDescending(path => path.Length))
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }
                if (Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    throw new InvalidDataException($"Created directory is not empty: '{directory}'.");
                }
                Directory.Delete(directory);
            }

            journal = journal with { State = TransactionState.RolledBack };
        }
        catch (Exception exception)
        {
            journal = journal with
            {
                State = TransactionState.NeedsAttention,
                Error = exception.Message
            };
        }
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
        return journal;
    }

    private static void ValidateJournal(TransactionJournal journal, string journalPath)
    {
        if (!Path.IsPathFullyQualified(journal.RootPath)
            || !Path.IsPathFullyQualified(journal.RestorePointPath)
            || !PathEquals(journal.RestorePointPath, Path.GetDirectoryName(Path.GetFullPath(journalPath))!))
        {
            throw new InvalidDataException("Transaction journal paths are invalid.");
        }

        foreach (var path in journal.CreatedPaths)
        {
            EnsureUnderRoot(journal.RootPath, path, "created path");
        }
        foreach (var directory in journal.CreatedDirectories)
        {
            EnsureUnderRoot(journal.RootPath, directory, "created directory");
        }
        foreach (var backup in journal.BackupPaths)
        {
            EnsureUnderRoot(journal.RootPath, backup.Key, "backup target");
            EnsureUnderRoot(journal.RestorePointPath, backup.Value, "backup path");
        }
        foreach (var change in journal.Changes)
        {
            _ = ResolveUnderRoot(journal.RootPath, change.RelativePath);
            if (change.BackupRelativePath is not null)
            {
                _ = ResolveUnderRoot(journal.RestorePointPath, change.BackupRelativePath);
            }
        }
    }

    private static void EnsureUnderRoot(string root, string path, string description)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidDataException($"Transaction {description} is not an absolute path: '{path}'.");
        }
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Transaction {description} escapes its root: '{path}'.");
        }
    }

    private static void ValidateDestinations(string targetRoot, IReadOnlyList<StagedFile> files)
    {
        foreach (var file in files)
        {
            var targetPath = ResolveUnderRoot(targetRoot, file.RelativePath);
            if (Directory.Exists(targetPath))
            {
                throw new IOException($"A directory already exists at file target '{file.RelativePath}'.");
            }
            if (File.Exists(targetPath) && IsReparsePoint(targetPath))
            {
                throw new IOException($"A symbolic link exists at file target '{file.RelativePath}'.");
            }
            var parent = Path.GetDirectoryName(targetPath);
            while (parent is not null)
            {
                if (File.Exists(parent))
                {
                    throw new IOException($"A file blocks target directory '{parent}'.");
                }
                if (Directory.Exists(parent) && IsReparsePoint(parent))
                {
                    throw new IOException($"A symbolic link blocks target directory '{parent}'.");
                }
                if (PathEquals(parent, targetRoot))
                {
                    break;
                }
                parent = Path.GetDirectoryName(parent);
            }
        }
    }

    private static void ValidateRemovals(string targetRoot, IReadOnlyList<string> relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var path = ResolveUnderRoot(targetRoot, relativePath);
            if (!File.Exists(path) || Directory.Exists(path))
            {
                throw new IOException($"Declared removal is not an existing file: '{relativePath}'.");
            }
            if (IsReparsePoint(path))
            {
                throw new IOException($"Declared removal is a symbolic link: '{relativePath}'.");
            }
        }
    }

    private static IEnumerable<string> GetMissingDirectories(string root, string directory)
    {
        var missing = new Stack<string>();
        for (var current = directory;
             !PathEquals(current, root) && !Directory.Exists(current);
             current = Path.GetDirectoryName(current)!)
        {
            missing.Push(current);
        }
        return missing;
    }

    private static string ExistingDirectory(string path, string parameterName)
    {
        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException($"{parameterName} does not exist: '{fullPath}'.");
    }

    private static void EnsureSeparateRoots(string left, string right)
    {
        var separator = Path.DirectorySeparatorChar.ToString();
        var leftPrefix = left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + separator;
        var rightPrefix = right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + separator;
        if (PathEquals(left, right)
            || leftPrefix.StartsWith(rightPrefix, StringComparison.OrdinalIgnoreCase)
            || rightPrefix.StartsWith(leftPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Staging and target directories must not overlap.");
        }
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes its root: '{relativePath}'.");
        }
        return path;
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void TryDeleteRestorePoint(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record StagedFile(string SourcePath, string RelativePath);
}
