using System.IO.Compression;
using System.Text.Json;

namespace Crystalfly.Updater;

internal static class PortableUpdateInstaller
{
    private const string PreservedDataDirectory = "Data";
    private const string ApplicationExecutableName = "Crystalfly.App.exe";
    private const string PortableMarkerName = "portable.flag";
    private const string RecoveryDirectoryName = ".crystalfly-update-recovery";
    private const string BackupDirectoryName = "backup";
    private const string StagingDirectoryName = "staging";
    private const string OperationLogName = "operation.json";
    private const string HealthFileName = "healthy";
    private static readonly TimeSpan HealthHandshakeTimeout = TimeSpan.FromMinutes(2);

    public static async Task<PortableUpdateOperation> ApplyAsync(
        string archivePath,
        string targetDirectory,
        CancellationToken cancellationToken,
        Action<string, string>? moveOverride = null)
    {
        string archive = Path.GetFullPath(archivePath);
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        if (!File.Exists(archive) || !string.Equals(Path.GetExtension(archive), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Portable update asset must be an existing ZIP archive.", nameof(archivePath));
        }
        ValidateTarget(target);
        await RecoverAsync(target, cancellationToken).ConfigureAwait(false);

        string parent = Directory.GetParent(target)?.FullName
            ?? throw new IOException("Portable update target must have a parent directory.");
        string recoveryRoot = Path.Combine(parent, RecoveryDirectoryName);
        string recovery = Path.Combine(recoveryRoot, Guid.NewGuid().ToString("N"));
        string[] originalEntryNames = ProgramEntries(target)
            .Select(path => Path.GetFileName(path))
            .ToArray();
        var operation = new PortableUpdateOperation(
            target,
            recovery,
            Path.Combine(recovery, BackupDirectoryName),
            Path.Combine(recovery, OperationLogName),
            Path.Combine(recovery, HealthFileName),
            originalEntryNames);
        string staging = Path.Combine(recovery, StagingDirectoryName);
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(operation.BackupDirectory);
        PersistOperation(operation);

        try
        {
            await ExtractSafeAsync(archive, staging, cancellationToken).ConfigureAwait(false);
            ValidatePreparedDirectory(staging);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyPreparedDirectory(staging, target, operation.BackupDirectory, moveOverride ?? Move);
            DeleteDirectoryIfPresent(staging);
            return operation;
        }
        catch (PortableUpdateRollbackException)
        {
            throw;
        }
        catch
        {
            DeleteRecoveryDirectory(operation);
            throw;
        }
    }

    public static async Task RecoverAsync(string targetDirectory, CancellationToken cancellationToken)
    {
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        ValidateTarget(target);
        string parent = Directory.GetParent(target)?.FullName
            ?? throw new IOException("Portable update target must have a parent directory.");
        string recoveryRoot = Path.Combine(parent, RecoveryDirectoryName);
        if (!Directory.Exists(recoveryRoot))
        {
            return;
        }

        foreach (string operationLogPath in Directory.EnumerateFiles(
                     recoveryRoot,
                     OperationLogName,
                     SearchOption.AllDirectories).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            PortableUpdateOperation operation = ReadAndValidateOperation(operationLogPath, target, recoveryRoot);
            if (!operation.Committed && !File.Exists(operation.HealthFilePath))
            {
                RestoreBackup(operation);
            }
            DeleteRecoveryDirectory(operation);
        }

        DeleteDirectoryIfEmpty(recoveryRoot);
        await Task.CompletedTask;
    }

    public static async Task CompleteAsync(PortableUpdateOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(operation.HealthFilePath))
        {
            throw new IOException("Updated application did not complete its health handshake.");
        }

        PersistOperation(operation with { Committed = true });
        DeleteRecoveryDirectory(operation);
        string recoveryRoot = Directory.GetParent(operation.RecoveryDirectory)?.FullName
            ?? throw new IOException("Portable update recovery directory must have a parent directory.");
        DeleteDirectoryIfEmpty(recoveryRoot);
        await Task.CompletedTask;
    }

    public static async Task WaitForHealthAndCompleteAsync(
        PortableUpdateOperation operation,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + HealthHandshakeTimeout;
        while (!File.Exists(operation.HealthFilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("Updated application did not complete its health handshake in time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }

        await CompleteAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExtractSafeAsync(
        string archivePath,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        string stagingPrefix = Path.TrimEndingDirectorySeparator(stagingDirectory)
            + Path.DirectorySeparatorChar;
        bool hasProgramFile = false;
        HashSet<string> normalizedPaths = new(StringComparer.OrdinalIgnoreCase);

        await using FileStream stream = new(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSymbolicLink(entry))
            {
                throw new InvalidDataException($"ZIP entry '{entry.FullName}' is an unsafe reparse point.");
            }

            string entryName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(entryName))
            {
                continue;
            }
            if (Path.IsPathFullyQualified(entryName)
                || entryName.Contains(':', StringComparison.Ordinal))
            {
                throw new InvalidDataException($"ZIP entry '{entry.FullName}' has an unsafe path.");
            }

            string destination = Path.GetFullPath(entryName, stagingDirectory);
            if (!destination.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"ZIP entry '{entry.FullName}' has an unsafe path.");
            }
            string relative = Path.GetRelativePath(stagingDirectory, destination);
            if (!normalizedPaths.Add(relative))
            {
                throw new InvalidDataException($"ZIP contains duplicate path '{entry.FullName}'.");
            }

            bool isDirectory = string.IsNullOrEmpty(entry.Name);
            if (IsPreservedDataPath(relative))
            {
                continue;
            }
            if (isDirectory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            hasProgramFile = true;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using Stream source = entry.Open();
            await using FileStream output = new(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!hasProgramFile)
        {
            throw new InvalidDataException("Portable update package contains no program files.");
        }
    }

    private static void ValidatePreparedDirectory(string staging)
    {
        if (!File.Exists(Path.Combine(staging, ApplicationExecutableName)))
        {
            throw new InvalidDataException($"Portable update package is missing {ApplicationExecutableName}.");
        }
        if (!File.Exists(Path.Combine(staging, PortableMarkerName)))
        {
            throw new InvalidDataException($"Portable update package is missing {PortableMarkerName}.");
        }
    }

    private static void ApplyPreparedDirectory(
        string staging,
        string target,
        string backup,
        Action<string, string> move)
    {
        List<(string Backup, string Original)> movedToBackup = [];
        List<string> installed = [];
        try
        {
            foreach (string existing in ProgramEntries(target).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string backupPath = Path.Combine(backup, Path.GetFileName(existing));
                move(existing, backupPath);
                movedToBackup.Add((backupPath, existing));
            }

            foreach (string prepared in ProgramEntries(staging).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string destination = Path.Combine(target, Path.GetFileName(prepared));
                move(prepared, destination);
                installed.Add(destination);
            }
        }
        catch (Exception applyException)
        {
            try
            {
                foreach (string path in installed.AsEnumerable().Reverse())
                {
                    DeletePath(path);
                }
                foreach ((string backupPath, string originalPath) in movedToBackup.AsEnumerable().Reverse())
                {
                    if (File.Exists(backupPath) || Directory.Exists(backupPath))
                    {
                        move(backupPath, originalPath);
                    }
                }
            }
            catch (Exception rollbackException) when (rollbackException is IOException
                or UnauthorizedAccessException)
            {
                throw new PortableUpdateRollbackException(backup, applyException, rollbackException);
            }
            throw;
        }
    }

    private static void RestoreBackup(PortableUpdateOperation operation)
    {
        string[] backupEntries = Directory.Exists(operation.BackupDirectory)
            ? Directory.EnumerateFileSystemEntries(operation.BackupDirectory).ToArray()
            : [];
        HashSet<string> backupNames = backupEntries
            .Select(path => Path.GetFileName(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool hasOriginalInventory = operation.OriginalEntryNames is not null;
        HashSet<string> originalNames = (operation.OriginalEntryNames ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string entry in ProgramEntries(operation.TargetDirectory).ToArray())
        {
            string name = Path.GetFileName(entry);
            if (backupNames.Contains(name) || (hasOriginalInventory && !originalNames.Contains(name)))
            {
                DeletePath(entry);
            }
        }

        foreach (string backupEntry in backupEntries.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string original = Path.Combine(operation.TargetDirectory, Path.GetFileName(backupEntry));
            Move(backupEntry, original);
        }
    }

    private static IEnumerable<string> ProgramEntries(string directory) =>
        Directory.EnumerateFileSystemEntries(directory).Where(path => !string.Equals(
            Path.GetFileName(path),
            PreservedDataDirectory,
            StringComparison.OrdinalIgnoreCase));

    private static PortableUpdateOperation ReadAndValidateOperation(
        string operationLogPath,
        string target,
        string recoveryRoot)
    {
        PortableUpdateOperation operation;
        try
        {
            operation = JsonSerializer.Deserialize<PortableUpdateOperation>(File.ReadAllText(operationLogPath))
                ?? throw new InvalidDataException("Portable update operation log is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Portable update operation log is invalid.", exception);
        }

        string recovery = Path.GetFullPath(operation.RecoveryDirectory);
        string backup = Path.GetFullPath(operation.BackupDirectory);
        string log = Path.GetFullPath(operation.OperationLogPath);
        string health = Path.GetFullPath(operation.HealthFilePath);
        if (!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(operation.TargetDirectory)), target,
                StringComparison.OrdinalIgnoreCase)
            || !PathSafety.IsStrictDescendant(recoveryRoot, recovery)
            || !string.Equals(backup, Path.Combine(recovery, BackupDirectoryName), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(log, Path.Combine(recovery, OperationLogName), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(health, Path.Combine(recovery, HealthFileName), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(log, Path.GetFullPath(operationLogPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Portable update operation log contains unsafe paths.");
        }

        return operation with
        {
            TargetDirectory = target,
            RecoveryDirectory = recovery,
            BackupDirectory = backup,
            OperationLogPath = log,
            HealthFilePath = health
        };
    }

    private static void PersistOperation(PortableUpdateOperation operation)
    {
        string temporaryPath = operation.OperationLogPath + ".tmp";
        File.WriteAllText(temporaryPath, operation.Serialize());
        File.Move(temporaryPath, operation.OperationLogPath, overwrite: true);
    }

    private static void ValidateTarget(string target)
    {
        if (!Directory.Exists(target))
        {
            throw new DirectoryNotFoundException($"Portable update target was not found: {target}");
        }
        if (IsReparsePoint(target))
        {
            throw new IOException("Portable update target must not be a reparse point.");
        }
    }

    private static bool IsPreservedDataPath(string relativePath)
    {
        string firstSegment = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            2,
            StringSplitOptions.RemoveEmptyEntries)[0];
        return string.Equals(firstSegment, PreservedDataDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int UnixFileTypeMask = 0xF000;
        const int UnixSymbolicLink = 0xA000;
        int unixMode = (entry.ExternalAttributes >> 16) & 0xFFFF;
        return (unixMode & UnixFileTypeMask) == UnixSymbolicLink;
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void Move(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    private static void DeletePath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteRecoveryDirectory(PortableUpdateOperation operation)
    {
        DeleteDirectoryIfPresent(operation.RecoveryDirectory);
        string? recoveryRoot = Directory.GetParent(operation.RecoveryDirectory)?.FullName;
        if (recoveryRoot is not null)
        {
            DeleteDirectoryIfEmpty(recoveryRoot);
        }
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void DeleteDirectoryIfEmpty(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }

    private sealed class PortableUpdateRollbackException(
        string backupPath,
        Exception applyException,
        Exception rollbackException)
        : IOException(
            $"Portable update rollback was incomplete. Recovery backup was retained at '{backupPath}'.",
            new AggregateException(applyException, rollbackException));
}
