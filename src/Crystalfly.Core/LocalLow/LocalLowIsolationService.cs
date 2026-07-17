using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.LocalLow;

public sealed class LocalLowIsolationService
{
    private const string JournalFileName = "journal.json";
    private const string TakeoverFileName = "takeover.json";
    private readonly string sharedPath;
    private readonly string storagePath;
    private readonly string localLowStatePath;
    private readonly string transactionRoot;
    private readonly Action<LocalLowCheckpoint>? checkpointObserver;

    public LocalLowIsolationService(
        string sharedLocalLowPath,
        string storageRoot,
        Action<LocalLowCheckpoint>? checkpointObserver = null)
    {
        sharedPath = Path.GetFullPath(sharedLocalLowPath);
        storagePath = Path.GetFullPath(storageRoot);
        localLowStatePath = Path.Combine(storagePath, "local-low");
        transactionRoot = Path.Combine(localLowStatePath, "transactions");
        this.checkpointObserver = checkpointObserver;
    }

    public string SharedBackupPath => Path.Combine(localLowStatePath, "shared-backup");

    public string GetInstanceLocalLowPath(string instanceId)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        return Path.Combine(storagePath, "instances", instanceId, "local-low");
    }

    public async Task InitializeBaselinesAsync(
        IEnumerable<string> instanceIds,
        bool allowActiveSessionCompletion = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        string[] ids = instanceIds.Distinct(StringComparer.Ordinal).ToArray();
        foreach (string instanceId in ids)
        {
            ValidateSegment(instanceId, nameof(instanceIds));
        }
        if (ids.Length == 0)
        {
            return;
        }

        var recovery = await RecoverPendingAsync(
            cancellationToken,
            allowActiveSessionCompletion);
        if (recovery.Any(result => result.State == TransactionState.NeedsAttention
            || IsActiveSession(result)))
        {
            throw new InvalidOperationException(
                "An active LocalLow session must finish before instance baselines can be initialized.");
        }

        var takeover = await EnsureTakeoverAsync(cancellationToken);
        foreach (string instanceId in ids)
        {
            await EnsureInstanceBaselineAsync(instanceId, takeover, cancellationToken);
        }
    }

    public async Task<LocalLowSessionJournal> SwitchInAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        var recovery = await RecoverPendingAsync(cancellationToken);
        if (recovery.Any(result => result.State == TransactionState.NeedsAttention
            || IsActiveSession(result)))
        {
            throw new InvalidOperationException(
                "LocalLow recovery needs attention before another instance can start.");
        }

        var takeover = await EnsureTakeoverAsync(cancellationToken);
        var instancePath = await EnsureInstanceBaselineAsync(instanceId, takeover, cancellationToken);
        var id = Guid.NewGuid().ToString("N");
        var journalDirectory = Path.Combine(transactionRoot, id);
        var journalPath = Path.Combine(journalDirectory, JournalFileName);
        var sharedPreservedPath = sharedPath + $".crystalfly-{id}-shared";
        var activationStagingPath = sharedPath + $".crystalfly-{id}-active";
        var instanceStagingPath = instancePath + $".crystalfly-{id}-staging";
        var instancePreviousPath = instancePath + $".crystalfly-{id}-previous";
        EnsurePathsAbsent(
            sharedPreservedPath,
            activationStagingPath,
            instanceStagingPath,
            instancePreviousPath);

        var journal = new LocalLowSessionJournal
        {
            Id = id,
            InstanceId = instanceId,
            State = TransactionState.Prepared,
            Phase = LocalLowSessionPhase.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            SharedPath = sharedPath,
            InstancePath = instancePath,
            SharedPreservedPath = sharedPreservedPath,
            ActivationStagingPath = activationStagingPath,
            InstanceStagingPath = instanceStagingPath,
            InstancePreviousPath = instancePreviousPath,
            SharedSha256 = await LocalLowDirectory.HashAsync(sharedPath, includeLogs: true, cancellationToken),
            InstanceSha256 = await LocalLowDirectory.HashAsync(instancePath, includeLogs: false, cancellationToken)
        };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);

        await LocalLowDirectory.CopyAsync(
            instancePath,
            activationStagingPath,
            includeLogs: false,
            cancellationToken);
        checkpointObserver?.Invoke(LocalLowCheckpoint.ActivationStaged);
        await RequireHashAsync(
            activationStagingPath,
            journal.InstanceSha256,
            includeLogs: false,
            cancellationToken);
        journal = await WriteJournalAsync(
            journalPath,
            journal with
            {
                State = TransactionState.Applying,
                Phase = LocalLowSessionPhase.ActivationStaged
            },
            cancellationToken);

        Directory.Move(sharedPath, sharedPreservedPath);
        checkpointObserver?.Invoke(LocalLowCheckpoint.SharedPreserved);
        journal = await WriteJournalAsync(
            journalPath,
            journal with { Phase = LocalLowSessionPhase.SharedPreserved },
            cancellationToken);

        Directory.Move(activationStagingPath, sharedPath);
        checkpointObserver?.Invoke(LocalLowCheckpoint.InstanceActivated);
        journal = await WriteJournalAsync(
            journalPath,
            journal with { Phase = LocalLowSessionPhase.InstanceActive },
            cancellationToken);
        return journal;
    }

    public async Task<LocalLowSessionJournal> SwitchOutAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(sessionId, nameof(sessionId));
        var journalPath = Path.Combine(transactionRoot, sessionId, JournalFileName);
        var journal = await AtomicJsonStore.ReadAsync<LocalLowSessionJournal>(
            journalPath,
            cancellationToken);
        ValidateJournal(journal, journalPath);
        if (journal.State == TransactionState.NeedsAttention)
        {
            throw new InvalidOperationException("LocalLow transaction needs attention.");
        }
        if (journal.Phase is LocalLowSessionPhase.Prepared
            or LocalLowSessionPhase.ActivationStaged
            or LocalLowSessionPhase.SharedPreserved)
        {
            throw new InvalidOperationException("LocalLow instance was not fully activated.");
        }
        return await CompleteSessionAsync(journal, journalPath, cancellationToken);
    }

    public async Task<IReadOnlyList<LocalLowSessionJournal>> RecoverPendingAsync(
        CancellationToken cancellationToken = default,
        bool allowActiveSessionCompletion = false)
    {
        Directory.CreateDirectory(localLowStatePath);
        Directory.CreateDirectory(transactionRoot);
        var takeover = await RecoverTakeoverAsync(cancellationToken);
        if (takeover?.State == TransactionState.NeedsAttention)
        {
            throw new InvalidOperationException("The original shared LocalLow backup needs attention.");
        }

        var results = new List<LocalLowSessionJournal>();
        foreach (var journalPath in Directory
            .EnumerateFiles(transactionRoot, JournalFileName, SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var journal = await AtomicJsonStore.ReadAsync<LocalLowSessionJournal>(
                journalPath,
                cancellationToken);
            if (journal.State == TransactionState.NeedsAttention)
            {
                results.Add(journal);
                continue;
            }

            try
            {
                ValidateJournal(journal, journalPath);
                journal = journal.Phase switch
                {
                    LocalLowSessionPhase.Prepared
                        or LocalLowSessionPhase.ActivationStaged
                        or LocalLowSessionPhase.SharedPreserved =>
                        await RollBackSwitchInAsync(journal, journalPath, cancellationToken),
                    LocalLowSessionPhase.InstanceActive
                        or LocalLowSessionPhase.CaptureStaged
                        or LocalLowSessionPhase.InstanceCaptured
                        or LocalLowSessionPhase.SharedRestored
                        when allowActiveSessionCompletion =>
                        await CompleteSessionAsync(journal, journalPath, cancellationToken),
                    LocalLowSessionPhase.InstanceActive
                        or LocalLowSessionPhase.CaptureStaged
                        or LocalLowSessionPhase.InstanceCaptured
                        or LocalLowSessionPhase.SharedRestored => journal,
                    LocalLowSessionPhase.Completed or LocalLowSessionPhase.RolledBack =>
                        await CleanFinishedAsync(journal, journalPath),
                    _ => throw new InvalidDataException($"Unknown LocalLow phase: {journal.Phase}.")
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                journal = await MarkNeedsAttentionAsync(journal, journalPath, exception);
            }
            results.Add(journal);
        }
        return results;
    }

    private static bool IsActiveSession(LocalLowSessionJournal journal) =>
        journal.State is TransactionState.Prepared or TransactionState.Applying
        && journal.Phase is LocalLowSessionPhase.InstanceActive
            or LocalLowSessionPhase.CaptureStaged
            or LocalLowSessionPhase.InstanceCaptured
            or LocalLowSessionPhase.SharedRestored;

    private async Task<LocalLowTakeoverRecord> EnsureTakeoverAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(sharedPath);
        Directory.CreateDirectory(localLowStatePath);
        var takeoverPath = Path.Combine(localLowStatePath, TakeoverFileName);
        if (File.Exists(takeoverPath))
        {
            var recovered = await RecoverTakeoverAsync(cancellationToken)
                ?? throw new InvalidDataException("LocalLow takeover record disappeared.");
            if (recovered.State == TransactionState.NeedsAttention)
            {
                throw new InvalidOperationException("The original shared LocalLow backup needs attention.");
            }
            return recovered;
        }

        var stagingPath = SharedBackupPath + ".staging";
        EnsurePathsAbsent(SharedBackupPath, stagingPath);
        var record = new LocalLowTakeoverRecord
        {
            State = TransactionState.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            SharedPath = sharedPath,
            BackupPath = SharedBackupPath,
            StagingPath = stagingPath,
            SharedSha256 = await LocalLowDirectory.HashAsync(
                sharedPath,
                includeLogs: true,
                cancellationToken)
        };
        await AtomicJsonStore.WriteAsync(takeoverPath, record, cancellationToken);

        await LocalLowDirectory.CopyAsync(
            sharedPath,
            stagingPath,
            includeLogs: true,
            cancellationToken);
        checkpointObserver?.Invoke(LocalLowCheckpoint.TakeoverBackupStaged);
        await RequireHashAsync(
            stagingPath,
            record.SharedSha256,
            includeLogs: true,
            cancellationToken);
        Directory.Move(stagingPath, SharedBackupPath);
        checkpointObserver?.Invoke(LocalLowCheckpoint.TakeoverBackupCommitted);
        record = record with { State = TransactionState.Committed };
        await AtomicJsonStore.WriteAsync(takeoverPath, record, cancellationToken);
        return record;
    }

    private async Task<LocalLowTakeoverRecord?> RecoverTakeoverAsync(
        CancellationToken cancellationToken)
    {
        var takeoverPath = Path.Combine(localLowStatePath, TakeoverFileName);
        if (!File.Exists(takeoverPath))
        {
            return null;
        }

        var record = await AtomicJsonStore.ReadAsync<LocalLowTakeoverRecord>(
            takeoverPath,
            cancellationToken);
        ValidateTakeover(record);
        if (record.State == TransactionState.NeedsAttention)
        {
            return record;
        }

        try
        {
            if (Directory.Exists(record.BackupPath))
            {
                await RequireHashAsync(
                    record.BackupPath,
                    record.SharedSha256,
                    includeLogs: true,
                    cancellationToken);
                if (Directory.Exists(record.StagingPath))
                {
                    await RequireHashAsync(
                        record.StagingPath,
                        record.SharedSha256,
                        includeLogs: true,
                        cancellationToken);
                    LocalLowDirectory.DeleteIfExists(record.StagingPath);
                }
            }
            else if (Directory.Exists(record.StagingPath))
            {
                await RequireHashAsync(
                    record.StagingPath,
                    record.SharedSha256,
                    includeLogs: true,
                    cancellationToken);
                Directory.Move(record.StagingPath, record.BackupPath);
            }
            else
            {
                await RequireHashAsync(
                    sharedPath,
                    record.SharedSha256,
                    includeLogs: true,
                    cancellationToken);
                await LocalLowDirectory.CopyAsync(
                    sharedPath,
                    record.StagingPath,
                    includeLogs: true,
                    cancellationToken);
                await RequireHashAsync(
                    record.StagingPath,
                    record.SharedSha256,
                    includeLogs: true,
                    cancellationToken);
                Directory.Move(record.StagingPath, record.BackupPath);
            }

            if (record.State != TransactionState.Committed)
            {
                record = record with { State = TransactionState.Committed, Error = null };
                await AtomicJsonStore.WriteAsync(takeoverPath, record, cancellationToken);
            }
            return record;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            record = record with
            {
                State = TransactionState.NeedsAttention,
                Error = exception.Message
            };
            await AtomicJsonStore.WriteAsync(takeoverPath, record, CancellationToken.None);
            return record;
        }
    }

    private async Task<string> EnsureInstanceBaselineAsync(
        string instanceId,
        LocalLowTakeoverRecord takeover,
        CancellationToken cancellationToken)
    {
        var instancePath = GetInstanceLocalLowPath(instanceId);
        if (Directory.Exists(instancePath))
        {
            _ = await LocalLowDirectory.HashAsync(instancePath, includeLogs: false, cancellationToken);
            return instancePath;
        }

        var parent = Path.GetDirectoryName(instancePath)!;
        Directory.CreateDirectory(parent);
        var stagingPath = instancePath + ".baseline-staging";
        var expectedHash = await LocalLowDirectory.HashAsync(
            takeover.BackupPath,
            includeLogs: false,
            cancellationToken);
        if (Directory.Exists(stagingPath))
        {
            var stagingHash = await LocalLowDirectory.HashAsync(
                stagingPath,
                includeLogs: false,
                cancellationToken);
            if (!HashEquals(stagingHash, expectedHash))
            {
                LocalLowDirectory.DeleteIfExists(stagingPath);
            }
        }
        if (!Directory.Exists(stagingPath))
        {
            await LocalLowDirectory.CopyAsync(
                takeover.BackupPath,
                stagingPath,
                includeLogs: false,
                cancellationToken);
        }
        await RequireHashAsync(stagingPath, expectedHash, includeLogs: false, cancellationToken);
        Directory.Move(stagingPath, instancePath);
        return instancePath;
    }

    private async Task<LocalLowSessionJournal> RollBackSwitchInAsync(
        LocalLowSessionJournal journal,
        string journalPath,
        CancellationToken cancellationToken)
    {
        await RequireHashAsync(
            journal.InstancePath,
            journal.InstanceSha256,
            includeLogs: false,
            cancellationToken);
        var sharedExists = Directory.Exists(journal.SharedPath);
        var preservedExists = Directory.Exists(journal.SharedPreservedPath);
        var activationExists = Directory.Exists(journal.ActivationStagingPath);

        if (activationExists)
        {
            await RequireHashAsync(
                journal.ActivationStagingPath,
                journal.InstanceSha256,
                includeLogs: false,
                cancellationToken);
        }

        if (!sharedExists && preservedExists)
        {
            await RequireHashAsync(
                journal.SharedPreservedPath,
                journal.SharedSha256,
                includeLogs: true,
                cancellationToken);
            Directory.Move(journal.SharedPreservedPath, journal.SharedPath);
        }
        else if (sharedExists && !preservedExists)
        {
            await RequireHashAsync(
                journal.SharedPath,
                journal.SharedSha256,
                includeLogs: true,
                cancellationToken);
        }
        else if (sharedExists && preservedExists)
        {
            await RequireHashAsync(
                journal.SharedPreservedPath,
                journal.SharedSha256,
                includeLogs: true,
                cancellationToken);
            await RequireHashAsync(
                journal.SharedPath,
                journal.InstanceSha256,
                includeLogs: false,
                cancellationToken);
            LocalLowDirectory.DeleteIfExists(journal.SharedPath);
            Directory.Move(journal.SharedPreservedPath, journal.SharedPath);
        }
        else
        {
            throw new InvalidDataException("Neither shared nor preserved LocalLow data exists.");
        }

        if (activationExists)
        {
            LocalLowDirectory.DeleteIfExists(journal.ActivationStagingPath);
        }
        journal = journal with
        {
            State = TransactionState.RolledBack,
            Phase = LocalLowSessionPhase.RolledBack,
            Error = null
        };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
        LocalLowDirectory.DeleteIfExists(Path.GetDirectoryName(journalPath)!);
        return journal;
    }

    private async Task<LocalLowSessionJournal> CompleteSessionAsync(
        LocalLowSessionJournal journal,
        string journalPath,
        CancellationToken cancellationToken)
    {
        var capturedHash = journal.CapturedSha256;
        if (capturedHash is null)
        {
            if (!Directory.Exists(journal.SharedPath)
                || !Directory.Exists(journal.SharedPreservedPath))
            {
                throw new InvalidDataException("Active or preserved LocalLow data is missing.");
            }
            await RequireHashAsync(
                journal.SharedPreservedPath,
                journal.SharedSha256,
                includeLogs: true,
                cancellationToken);
            var activeHash = await LocalLowDirectory.HashAsync(
                journal.SharedPath,
                includeLogs: false,
                cancellationToken);
            if (Directory.Exists(journal.InstanceStagingPath))
            {
                await RequireHashAsync(
                    journal.InstanceStagingPath,
                    activeHash,
                    includeLogs: false,
                    cancellationToken);
            }
            else
            {
                await LocalLowDirectory.CopyAsync(
                    journal.SharedPath,
                    journal.InstanceStagingPath,
                    includeLogs: false,
                    cancellationToken);
                checkpointObserver?.Invoke(LocalLowCheckpoint.CaptureStaged);
            }
            capturedHash = activeHash;
            journal = await WriteJournalAsync(
                journalPath,
                journal with
                {
                    State = TransactionState.Applying,
                    Phase = LocalLowSessionPhase.CaptureStaged,
                    CapturedSha256 = capturedHash
                },
                cancellationToken);
        }
        else
        {
            ValidateHash(capturedHash, nameof(journal.CapturedSha256));
        }

        journal = await CaptureInstanceAsync(journal, journalPath, capturedHash, cancellationToken);
        journal = await RestoreSharedAsync(journal, journalPath, capturedHash, cancellationToken);

        if (Directory.Exists(journal.InstancePreviousPath))
        {
            await RequireHashAsync(
                journal.InstancePreviousPath,
                journal.InstanceSha256,
                includeLogs: false,
                cancellationToken);
            LocalLowDirectory.DeleteIfExists(journal.InstancePreviousPath);
        }
        if (Directory.Exists(journal.InstanceStagingPath))
        {
            await RequireHashAsync(
                journal.InstanceStagingPath,
                capturedHash,
                includeLogs: false,
                cancellationToken);
            LocalLowDirectory.DeleteIfExists(journal.InstanceStagingPath);
        }

        journal = journal with
        {
            State = TransactionState.Committed,
            Phase = LocalLowSessionPhase.Completed,
            Error = null
        };
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
        LocalLowDirectory.DeleteIfExists(Path.GetDirectoryName(journalPath)!);
        return journal;
    }

    private async Task<LocalLowSessionJournal> CaptureInstanceAsync(
        LocalLowSessionJournal journal,
        string journalPath,
        string capturedHash,
        CancellationToken cancellationToken)
    {
        var instanceExists = Directory.Exists(journal.InstancePath);
        var previousExists = Directory.Exists(journal.InstancePreviousPath);
        var stagingExists = Directory.Exists(journal.InstanceStagingPath);

        if (journal.Phase == LocalLowSessionPhase.SharedRestored
            && instanceExists
            && !previousExists
            && !stagingExists)
        {
            await RequireHashAsync(
                journal.InstancePath,
                capturedHash,
                includeLogs: false,
                cancellationToken);
            return journal;
        }

        if (instanceExists && !previousExists && stagingExists)
        {
            await RequireHashAsync(
                journal.InstancePath,
                journal.InstanceSha256,
                includeLogs: false,
                cancellationToken);
            await RequireHashAsync(
                journal.InstanceStagingPath,
                capturedHash,
                includeLogs: false,
                cancellationToken);
            Directory.Move(journal.InstancePath, journal.InstancePreviousPath);
            checkpointObserver?.Invoke(LocalLowCheckpoint.InstancePreserved);
            instanceExists = false;
            previousExists = true;
        }

        if (!instanceExists && previousExists && stagingExists)
        {
            await RequireHashAsync(
                journal.InstancePreviousPath,
                journal.InstanceSha256,
                includeLogs: false,
                cancellationToken);
            await RequireHashAsync(
                journal.InstanceStagingPath,
                capturedHash,
                includeLogs: false,
                cancellationToken);
            Directory.Move(journal.InstanceStagingPath, journal.InstancePath);
            checkpointObserver?.Invoke(LocalLowCheckpoint.InstanceCaptured);
            instanceExists = true;
            stagingExists = false;
        }

        if (!instanceExists || !previousExists || stagingExists)
        {
            throw new InvalidDataException("Instance capture paths are inconsistent.");
        }
        await RequireHashAsync(
            journal.InstancePath,
            capturedHash,
            includeLogs: false,
            cancellationToken);
        await RequireHashAsync(
            journal.InstancePreviousPath,
            journal.InstanceSha256,
            includeLogs: false,
            cancellationToken);
        return await WriteJournalAsync(
            journalPath,
            journal with { Phase = LocalLowSessionPhase.InstanceCaptured },
            cancellationToken);
    }

    private async Task<LocalLowSessionJournal> RestoreSharedAsync(
        LocalLowSessionJournal journal,
        string journalPath,
        string capturedHash,
        CancellationToken cancellationToken)
    {
        var sharedExists = Directory.Exists(journal.SharedPath);
        var preservedExists = Directory.Exists(journal.SharedPreservedPath);
        if (preservedExists)
        {
            await RequireHashAsync(
                journal.SharedPreservedPath,
                journal.SharedSha256,
                includeLogs: true,
                cancellationToken);
            if (sharedExists)
            {
                await RequireHashAsync(
                    journal.SharedPath,
                    capturedHash,
                    includeLogs: false,
                    cancellationToken);
                LocalLowDirectory.DeleteIfExists(journal.SharedPath);
                checkpointObserver?.Invoke(LocalLowCheckpoint.ActiveSharedRemoved);
            }
            Directory.Move(journal.SharedPreservedPath, journal.SharedPath);
            checkpointObserver?.Invoke(LocalLowCheckpoint.SharedRestored);
        }
        else
        {
            if (!sharedExists)
            {
                throw new InvalidDataException("Restored shared LocalLow data is missing.");
            }
            await RequireHashAsync(
                journal.SharedPath,
                journal.SharedSha256,
                includeLogs: true,
                cancellationToken);
        }

        return await WriteJournalAsync(
            journalPath,
            journal with { Phase = LocalLowSessionPhase.SharedRestored },
            cancellationToken);
    }

    private static async Task<LocalLowSessionJournal> CleanFinishedAsync(
        LocalLowSessionJournal journal,
        string journalPath)
    {
        LocalLowDirectory.DeleteIfExists(Path.GetDirectoryName(journalPath)!);
        await Task.CompletedTask;
        return journal;
    }

    private static async Task<LocalLowSessionJournal> MarkNeedsAttentionAsync(
        LocalLowSessionJournal journal,
        string journalPath,
        Exception exception)
    {
        journal = journal with
        {
            State = TransactionState.NeedsAttention,
            Error = exception.Message
        };
        await AtomicJsonStore.WriteAsync(journalPath, journal, CancellationToken.None);
        return journal;
    }

    private static async Task<LocalLowSessionJournal> WriteJournalAsync(
        string journalPath,
        LocalLowSessionJournal journal,
        CancellationToken cancellationToken)
    {
        await AtomicJsonStore.WriteAsync(journalPath, journal, cancellationToken);
        return journal;
    }

    private void ValidateTakeover(LocalLowTakeoverRecord record)
    {
        if (!LocalLowDirectory.PathEquals(record.SharedPath, sharedPath)
            || !LocalLowDirectory.PathEquals(record.BackupPath, SharedBackupPath)
            || !LocalLowDirectory.PathEquals(record.StagingPath, SharedBackupPath + ".staging"))
        {
            throw new InvalidDataException("LocalLow takeover paths are invalid.");
        }
        ValidateHash(record.SharedSha256, nameof(record.SharedSha256));
    }

    private void ValidateJournal(LocalLowSessionJournal journal, string journalPath)
    {
        ValidateSegment(journal.Id, nameof(journal.Id));
        ValidateSegment(journal.InstanceId, nameof(journal.InstanceId));
        var expectedDirectory = Path.Combine(transactionRoot, journal.Id);
        var expectedInstance = GetInstanceLocalLowPath(journal.InstanceId);
        if (!LocalLowDirectory.PathEquals(Path.GetDirectoryName(journalPath)!, expectedDirectory)
            || !LocalLowDirectory.PathEquals(journal.SharedPath, sharedPath)
            || !LocalLowDirectory.PathEquals(journal.InstancePath, expectedInstance)
            || !LocalLowDirectory.PathEquals(
                journal.SharedPreservedPath,
                sharedPath + $".crystalfly-{journal.Id}-shared")
            || !LocalLowDirectory.PathEquals(
                journal.ActivationStagingPath,
                sharedPath + $".crystalfly-{journal.Id}-active")
            || !LocalLowDirectory.PathEquals(
                journal.InstanceStagingPath,
                expectedInstance + $".crystalfly-{journal.Id}-staging")
            || !LocalLowDirectory.PathEquals(
                journal.InstancePreviousPath,
                expectedInstance + $".crystalfly-{journal.Id}-previous"))
        {
            throw new InvalidDataException("LocalLow transaction paths are invalid.");
        }
        ValidateHash(journal.SharedSha256, nameof(journal.SharedSha256));
        ValidateHash(journal.InstanceSha256, nameof(journal.InstanceSha256));
        if (journal.CapturedSha256 is not null)
        {
            ValidateHash(journal.CapturedSha256, nameof(journal.CapturedSha256));
        }
    }

    private static async Task RequireHashAsync(
        string path,
        string expectedHash,
        bool includeLogs,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"LocalLow directory is missing: '{path}'.");
        }
        var actualHash = await LocalLowDirectory.HashAsync(path, includeLogs, cancellationToken);
        if (!HashEquals(actualHash, expectedHash))
        {
            throw new InvalidDataException($"LocalLow hash mismatch for '{path}'.");
        }
    }

    private static void EnsurePathsAbsent(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path) || File.Exists(path))
            {
                throw new IOException($"LocalLow transaction path already exists: '{path}'.");
            }
        }
    }

    private static bool HashEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void ValidateHash(string hash, string name)
    {
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"{name} is not a SHA-256 value.");
        }
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value is "." or ".."
            || !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Value must be a single valid path segment.", parameterName);
        }
    }
}
