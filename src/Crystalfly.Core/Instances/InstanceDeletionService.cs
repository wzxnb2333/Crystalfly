using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Instances;

public sealed record InstanceDeletionConditions
{
    public bool HasBlockingQueueTasks { get; init; }

    public bool TransactionsHealthy { get; init; }
}

public sealed record InstanceDeletionResult(
    string InstanceId,
    string DeletedRootPath,
    string PendingPath,
    bool CleanupCompleted,
    string? CleanupError);

public sealed record InstanceDeletionRecoveryResult(
    string InstanceId,
    string PendingPath,
    bool Completed,
    string? Error);

public sealed class InstanceDeletionService
{
    private const string JournalFileName = "delete.json";
    private readonly string versionRoot;
    private readonly string pendingRoot;
    private readonly IHollowKnightProcessProbe processProbe;
    private readonly Func<string> operationIdFactory;
    private readonly Action<string, string> moveDirectory;
    private readonly Action<string> deleteDirectory;

    public InstanceDeletionService(
        string versionRoot,
        IHollowKnightProcessProbe? processProbe = null)
        : this(
            versionRoot,
            processProbe ?? new SystemHollowKnightProcessProbe(),
            () => Guid.NewGuid().ToString("N"),
            Directory.Move,
            path => Directory.Delete(path, recursive: true))
    {
    }

    internal InstanceDeletionService(
        string versionRoot,
        IHollowKnightProcessProbe processProbe,
        Func<string> operationIdFactory,
        Action<string, string> moveDirectory,
        Action<string> deleteDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionRoot);
        ArgumentNullException.ThrowIfNull(processProbe);
        ArgumentNullException.ThrowIfNull(operationIdFactory);
        ArgumentNullException.ThrowIfNull(moveDirectory);
        ArgumentNullException.ThrowIfNull(deleteDirectory);
        this.versionRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot));
        pendingRoot = Path.Combine(this.versionRoot, ".crystalfly", "delete-pending");
        this.processProbe = processProbe;
        this.operationIdFactory = operationIdFactory;
        this.moveDirectory = moveDirectory;
        this.deleteDirectory = deleteDirectory;
    }

    public async Task<InstanceDeletionResult> DeleteAsync(
        InstanceRecord instance,
        InstanceDeletionConditions conditions,
        CancellationToken cancellationToken = default) =>
        await DeleteAsync(
            instance,
            _ => ValueTask.FromResult(conditions),
            cancellationToken);

    public async Task<InstanceDeletionResult> DeleteAsync(
        InstanceRecord instance,
        Func<CancellationToken, ValueTask<InstanceDeletionConditions>> evaluateConditions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(evaluateConditions);
        var instanceRoot = ValidateInstanceRoot(instance.RootPath);
        ValidateConditions(await evaluateConditions(cancellationToken));

        var sidecar = await InstanceSidecar.LoadAsync(instanceRoot, cancellationToken);
        if (!string.Equals(sidecar.Id, instance.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Instance sidecar ID does not match the selected instance.");
        }

        var stateRoot = InstanceDirectory.ResolveUnderRoot(
            Path.Combine(versionRoot, ".crystalfly", "instances"),
            instance.Id);
        if (!Directory.Exists(stateRoot))
        {
            throw new InvalidDataException("Instance state directory is missing.");
        }
        RejectReparsePoint(stateRoot);

        var operationId = operationIdFactory();
        var operationRoot = InstanceDirectory.ResolveUnderRoot(pendingRoot, operationId);
        if (Directory.Exists(operationRoot) || File.Exists(operationRoot))
        {
            throw new IOException($"Delete operation '{operationId}' already exists.");
        }
        Directory.CreateDirectory(operationRoot);
        var pendingGameRoot = Path.Combine(operationRoot, "game");
        var pendingStateRoot = Path.Combine(operationRoot, "instance-state");
        var journalPath = Path.Combine(operationRoot, JournalFileName);
        var journal = new InstanceDeletionJournal
        {
            OperationId = operationId,
            InstanceId = instance.Id,
            State = TransactionState.Prepared,
            InstanceRoot = instanceRoot,
            StateRoot = stateRoot,
            PendingRoot = operationRoot
        };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);

        var gameMoved = false;
        var stateMoved = false;
        try
        {
            journal = journal with { State = TransactionState.Applying };
            await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
            ValidateConditions(await evaluateConditions(cancellationToken));
            moveDirectory(instanceRoot, pendingGameRoot);
            gameMoved = true;
            moveDirectory(stateRoot, pendingStateRoot);
            stateMoved = true;
            journal = journal with { State = TransactionState.Committed };
            await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
        }
        catch (Exception exception)
        {
            var rollbackError = TryRollBack(
                instanceRoot,
                stateRoot,
                pendingGameRoot,
                pendingStateRoot,
                gameMoved,
                stateMoved);
            journal = journal with
            {
                State = rollbackError is null ? TransactionState.RolledBack : TransactionState.NeedsAttention,
                Error = rollbackError?.Message ?? exception.Message
            };
            try
            {
                await AtomicJsonStore.WriteAsync(journalPath, journal, CancellationToken.None);
                if (rollbackError is null)
                {
                    TryDelete(operationRoot);
                }
            }
            catch (Exception journalError)
            {
                rollbackError = rollbackError is null
                    ? journalError
                    : new AggregateException(rollbackError, journalError);
            }

            if (rollbackError is not null)
            {
                throw new AggregateException(exception, rollbackError);
            }
            throw;
        }

        var cleanupError = TryDelete(operationRoot);
        return new InstanceDeletionResult(
            instance.Id,
            instanceRoot,
            operationRoot,
            cleanupError is null,
            cleanupError?.Message);
    }

    public async Task<IReadOnlyList<InstanceDeletionRecoveryResult>> RecoverPendingAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(pendingRoot))
        {
            return [];
        }

        var results = new List<InstanceDeletionRecoveryResult>();
        foreach (var operationRoot in Directory.EnumerateDirectories(pendingRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            InstanceDeletionJournal? journal = null;
            Exception? error = null;
            try
            {
                RejectReparsePoint(operationRoot);
                if (!Directory.EnumerateFileSystemEntries(operationRoot).Any())
                {
                    error = TryDelete(operationRoot);
                    results.Add(new InstanceDeletionRecoveryResult(
                        string.Empty,
                        operationRoot,
                        error is null,
                        error?.Message));
                    continue;
                }
                journal = await AtomicJsonStore.ReadAsync<InstanceDeletionJournal>(
                    Path.Combine(operationRoot, JournalFileName),
                    cancellationToken);
                ValidateJournal(journal, operationRoot);
                var pendingGameRoot = Path.Combine(operationRoot, "game");
                var pendingStateRoot = Path.Combine(operationRoot, "instance-state");
                var originalGameExists = PathExists(journal.InstanceRoot);
                var pendingGameExists = PathExists(pendingGameRoot);
                var originalStateExists = PathExists(journal.StateRoot);
                var pendingStateExists = PathExists(pendingStateRoot);
                var invalidSides = originalGameExists == pendingGameExists
                    || originalStateExists == pendingStateExists;
                var unsafePath = originalGameExists && !IsRegularDirectory(journal.InstanceRoot)
                    || pendingGameExists && !IsRegularDirectory(pendingGameRoot)
                    || originalStateExists && !IsRegularDirectory(journal.StateRoot)
                    || pendingStateExists && !IsRegularDirectory(pendingStateRoot);
                var invalidPhase = journal.State switch
                {
                    TransactionState.Prepared or TransactionState.RolledBack =>
                        !originalGameExists || !originalStateExists,
                    TransactionState.Committed => !pendingGameExists || !pendingStateExists,
                    _ => false
                };
                if (invalidSides || unsafePath || invalidPhase)
                {
                    error = new InvalidDataException(
                        "Delete recovery found an ambiguous or phase-inconsistent path pair.");
                    journal = journal with
                    {
                        State = TransactionState.NeedsAttention,
                        Error = error.Message
                    };
                    await AtomicJsonStore.WriteAsync(
                        Path.Combine(operationRoot, JournalFileName),
                        journal,
                        CancellationToken.None);
                }
                if (journal.State is TransactionState.Applying or TransactionState.RollingBack)
                {
                    error = TryRollBack(
                        journal.InstanceRoot,
                        journal.StateRoot,
                        pendingGameRoot,
                        pendingStateRoot,
                        pendingGameExists,
                        pendingStateExists);
                    journal = journal with
                    {
                        State = error is null ? TransactionState.RolledBack : TransactionState.NeedsAttention,
                        Error = error?.Message
                    };
                    await AtomicJsonStore.WriteAsync(
                        Path.Combine(operationRoot, JournalFileName),
                        journal,
                        CancellationToken.None);
                }

                if (journal.State is TransactionState.Prepared
                    or TransactionState.Committed
                    or TransactionState.RolledBack)
                {
                    error = TryDelete(operationRoot);
                }
                else if (journal.State == TransactionState.NeedsAttention)
                {
                    error ??= new InvalidOperationException(
                        journal.Error ?? "Delete recovery needs manual attention.");
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or System.Text.Json.JsonException)
            {
                error = exception;
            }

            results.Add(new InstanceDeletionRecoveryResult(
                journal?.InstanceId ?? string.Empty,
                operationRoot,
                error is null,
                error?.Message));
        }
        return results;
    }

    private void ValidateConditions(InstanceDeletionConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (processProbe.IsRunning())
        {
            throw new InvalidOperationException("A Hollow Knight process is running.");
        }
        if (conditions.HasBlockingQueueTasks)
        {
            throw new InvalidOperationException("The instance has an unfinished or retryable download task.");
        }
        if (!conditions.TransactionsHealthy)
        {
            throw new InvalidOperationException("The instance has a transaction that needs attention.");
        }
    }

    private string ValidateInstanceRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = Directory.GetParent(root)?.FullName;
        if (!string.Equals(parent, versionRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Instance root must be a direct child of the version root.",
                nameof(path));
        }
        var expected = InstanceDirectory.ResolveUnderRoot(versionRoot, Path.GetFileName(root));
        if (!string.Equals(root, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Instance root must be a direct child of the version root.",
                nameof(path));
        }
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Instance root '{root}' was not found.");
        }
        RejectReparsePoint(root);
        return root;
    }

    private Exception? TryRollBack(
        string instanceRoot,
        string stateRoot,
        string pendingGameRoot,
        string pendingStateRoot,
        bool gameMoved,
        bool stateMoved)
    {
        try
        {
            if (stateMoved && Directory.Exists(pendingStateRoot))
            {
                if (Directory.Exists(stateRoot) || File.Exists(stateRoot))
                {
                    throw new IOException($"Cannot restore occupied state path '{stateRoot}'.");
                }
                moveDirectory(pendingStateRoot, stateRoot);
            }
            if (gameMoved && Directory.Exists(pendingGameRoot))
            {
                if (Directory.Exists(instanceRoot) || File.Exists(instanceRoot))
                {
                    throw new IOException($"Cannot restore occupied instance path '{instanceRoot}'.");
                }
                moveDirectory(pendingGameRoot, instanceRoot);
            }
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private Exception? TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                RejectReparsePoint(path);
                deleteDirectory(path);
            }
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception;
        }
    }

    private void ValidateJournal(InstanceDeletionJournal journal, string operationRoot)
    {
        if (journal.SchemaVersion != InstanceDeletionJournal.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(journal.InstanceId)
            || !string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(journal.PendingRoot)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(operationRoot)),
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                ValidateDirectChildPath(journal.InstanceRoot),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(journal.InstanceRoot)),
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                InstanceDirectory.ResolveUnderRoot(
                    Path.Combine(versionRoot, ".crystalfly", "instances"),
                    journal.InstanceId),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(journal.StateRoot)),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Delete journal contains invalid paths or identifiers.");
        }
    }

    private string ValidateDirectChildPath(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!string.Equals(
            Directory.GetParent(fullPath)?.FullName,
            versionRoot,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Delete journal instance root is outside the version root.");
        }
        return InstanceDirectory.ResolveUnderRoot(versionRoot, Path.GetFileName(fullPath));
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Cannot delete reparse point '{path}'.");
        }
    }

    private static bool PathExists(string path) => Directory.Exists(path) || File.Exists(path);

    private static bool IsRegularDirectory(string path) =>
        Directory.Exists(path)
        && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;

    private sealed record InstanceDeletionJournal
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        public required string OperationId { get; init; }

        public required string InstanceId { get; init; }

        public TransactionState State { get; init; }

        public required string InstanceRoot { get; init; }

        public required string StateRoot { get; init; }

        public required string PendingRoot { get; init; }

        public string? Error { get; init; }
    }
}
