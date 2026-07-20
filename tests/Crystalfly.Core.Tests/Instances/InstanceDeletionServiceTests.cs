using System.Text.Json;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceDeletionServiceTests : IDisposable
{
    private readonly string versionRoot = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-delete-{Guid.NewGuid():N}");

    [Fact]
    public async Task Delete_moves_instance_and_state_before_removing_them()
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var moves = new List<(string Source, string Destination)>();
        var service = CreateService(
            moveDirectory: (source, destination) =>
            {
                moves.Add((source, destination));
                Directory.Move(source, destination);
            });

        var result = await service.DeleteAsync(instance, ReadyConditions());

        Assert.True(result.CleanupCompleted);
        Assert.Equal(instance.Id, result.InstanceId);
        Assert.False(Directory.Exists(instance.RootPath));
        Assert.False(Directory.Exists(GetStateRoot(instance.Id)));
        Assert.False(Directory.Exists(result.PendingPath));
        Assert.Equal(2, moves.Count);
        Assert.All(moves, move => Assert.StartsWith(result.PendingPath, move.Destination));
    }

    [Theory]
    [InlineData("nested/instance")]
    [InlineData("../outside")]
    public async Task Delete_rejects_roots_that_are_not_direct_children(string relativePath)
    {
        Directory.CreateDirectory(versionRoot);
        var root = Path.GetFullPath(Path.Combine(versionRoot, relativePath));
        var instance = new InstanceRecord
        {
            Id = "bad-root",
            Name = "Bad root",
            RootPath = root,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().DeleteAsync(instance, ReadyConditions()));
    }

    [Fact]
    public async Task Delete_rejects_crystalfly_control_directory()
    {
        var root = Directory.CreateDirectory(Path.Combine(versionRoot, ".crystalfly")).FullName;
        var instance = new InstanceRecord
        {
            Id = "control",
            Name = "Control",
            RootPath = root,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().DeleteAsync(instance, ReadyConditions()));
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public async Task Delete_rejects_reparse_point_instance_root()
    {
        Directory.CreateDirectory(versionRoot);
        var target = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"crystalfly-link-{Guid.NewGuid():N}"));
        var link = Path.Combine(versionRoot, "linked");
        try
        {
            Directory.CreateSymbolicLink(link, target.FullName);
            var instance = new InstanceRecord
            {
                Id = "linked-id",
                Name = "Linked",
                RootPath = link,
                BuildId = "1.5.78.11833",
                CreatedAt = DateTimeOffset.UtcNow
            };

            await Assert.ThrowsAsync<IOException>(() =>
                CreateService().DeleteAsync(instance, ReadyConditions()));
            Assert.True(Directory.Exists(target.FullName));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
            if (Directory.Exists(target.FullName))
            {
                Directory.Delete(target.FullName, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Delete_rejects_sidecar_id_mismatch()
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        await InstanceSidecar.SaveAsync(instance with { Id = "other-id" });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService().DeleteAsync(instance, ReadyConditions()));
        Assert.True(Directory.Exists(instance.RootPath));
    }

    [Fact]
    public async Task Delete_rejects_running_game()
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(isRunning: true).DeleteAsync(instance, ReadyConditions()));
        Assert.True(Directory.Exists(instance.RootPath));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Delete_rejects_blocking_queue_or_unhealthy_transactions(
        bool hasBlockingQueueTasks,
        bool transactionsHealthy)
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var conditions = new InstanceDeletionConditions
        {
            HasBlockingQueueTasks = hasBlockingQueueTasks,
            TransactionsHealthy = transactionsHealthy
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().DeleteAsync(instance, conditions));
        Assert.True(Directory.Exists(instance.RootPath));
    }

    [Fact]
    public async Task Delete_rechecks_conditions_immediately_before_moving_directories()
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var evaluationCount = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().DeleteAsync(
                instance,
                _ => ValueTask.FromResult(++evaluationCount == 1
                    ? ReadyConditions()
                    : new InstanceDeletionConditions
                    {
                        HasBlockingQueueTasks = true,
                        TransactionsHealthy = true
                    })));

        Assert.Equal(2, evaluationCount);
        Assert.True(Directory.Exists(instance.RootPath));
        Assert.True(Directory.Exists(GetStateRoot(instance.Id)));
    }

    [Fact]
    public async Task Delete_rejects_reparse_point_instance_state()
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var stateRoot = GetStateRoot(instance.Id);
        var target = Path.Combine(Path.GetTempPath(), $"crystalfly-state-link-{Guid.NewGuid():N}");
        Directory.Move(stateRoot, target);
        Directory.CreateSymbolicLink(stateRoot, target);
        try
        {
            await Assert.ThrowsAsync<IOException>(() =>
                CreateService().DeleteAsync(instance, ReadyConditions()));
            Assert.True(Directory.Exists(instance.RootPath));
            Assert.True(Directory.Exists(target));
        }
        finally
        {
            if (Directory.Exists(stateRoot))
            {
                Directory.Delete(stateRoot);
            }
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Delete_rolls_back_game_directory_when_second_move_fails()
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var moveCount = 0;
        var service = CreateService(moveDirectory: (source, destination) =>
        {
            if (++moveCount == 2)
            {
                throw new IOException("Injected state move failure.");
            }
            Directory.Move(source, destination);
        });

        await Assert.ThrowsAsync<IOException>(() =>
            service.DeleteAsync(instance, ReadyConditions()));

        Assert.True(Directory.Exists(instance.RootPath));
        Assert.True(Directory.Exists(GetStateRoot(instance.Id)));
        Assert.Equal(instance, await InstanceSidecar.LoadAsync(instance.RootPath));
    }

    [Fact]
    public async Task RecoverPending_removes_committed_delete_left_by_cleanup_failure()
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var service = CreateService(deleteDirectory: _ => throw new IOException("Injected cleanup failure."));
        var result = await service.DeleteAsync(instance, ReadyConditions());

        Assert.False(result.CleanupCompleted);
        Assert.True(Directory.Exists(result.PendingPath));
        Assert.False(Directory.Exists(instance.RootPath));

        var recoveries = await CreateService().RecoverPendingAsync();

        var recovery = Assert.Single(recoveries);
        Assert.Equal(instance.Id, recovery.InstanceId);
        Assert.True(recovery.Completed);
        Assert.False(Directory.Exists(result.PendingPath));
    }

    [Fact]
    public async Task RecoverPending_removes_empty_preparation_directory()
    {
        var pending = Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "delete-pending",
            "empty"));

        var recoveries = await CreateService().RecoverPendingAsync();

        Assert.True(Assert.Single(recoveries).Completed);
        Assert.False(Directory.Exists(pending.FullName));
    }

    [Theory]
    [InlineData("game")]
    [InlineData("instance-state")]
    public async Task RecoverPending_marks_needs_attention_when_original_and_pending_both_exist(
        string pendingDirectory)
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var failedCleanup = CreateService(
            deleteDirectory: _ => throw new IOException("Injected cleanup failure."));
        var deletion = await failedCleanup.DeleteAsync(instance, ReadyConditions());
        Directory.CreateDirectory(pendingDirectory == "game"
            ? instance.RootPath
            : GetStateRoot(instance.Id));

        var recovery = Assert.Single(await CreateService().RecoverPendingAsync());

        Assert.False(recovery.Completed);
        Assert.True(Directory.Exists(deletion.PendingPath));
        await AssertNeedsAttentionAsync(deletion.PendingPath);
    }

    [Theory]
    [InlineData("game")]
    [InlineData("instance-state")]
    public async Task RecoverPending_marks_needs_attention_when_original_and_pending_are_both_missing(
        string pendingDirectory)
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var failedCleanup = CreateService(
            deleteDirectory: _ => throw new IOException("Injected cleanup failure."));
        var deletion = await failedCleanup.DeleteAsync(instance, ReadyConditions());
        Directory.Delete(Path.Combine(deletion.PendingPath, pendingDirectory), recursive: true);

        var recovery = Assert.Single(await CreateService().RecoverPendingAsync());

        Assert.False(recovery.Completed);
        Assert.True(Directory.Exists(deletion.PendingPath));
        await AssertNeedsAttentionAsync(deletion.PendingPath);
    }

    [Fact]
    public async Task RecoverPending_reports_invalid_journals_and_continues_with_valid_residue()
    {
        var invalidJson = Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "delete-pending",
            "invalid-json"));
        await File.WriteAllTextAsync(Path.Combine(invalidJson.FullName, "delete.json"), "{broken");

        var invalidPath = Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "delete-pending",
            "invalid-path"));
        await File.WriteAllTextAsync(
            Path.Combine(invalidPath.FullName, "delete.json"),
            CrystalflyJson.Serialize(new
            {
                schemaVersion = 1,
                operationId = "invalid-path",
                instanceId = "..",
                state = TransactionState.Committed,
                instanceRoot = Path.Combine(versionRoot, "practice"),
                stateRoot = Path.Combine(versionRoot, ".crystalfly", "instances", ".."),
                pendingRoot = invalidPath.FullName,
                error = (string?)null
            }));

        var instance = await CreateInstanceAsync("valid", "valid-id");
        var failedCleanup = CreateService(
            deleteDirectory: _ => throw new IOException("Injected cleanup failure."));
        var deletion = await failedCleanup.DeleteAsync(instance, ReadyConditions());

        var recoveries = await CreateService().RecoverPendingAsync();

        Assert.Equal(3, recoveries.Count);
        Assert.Equal(2, recoveries.Count(result => !result.Completed));
        Assert.Contains(recoveries, result => result.InstanceId == instance.Id && result.Completed);
        Assert.True(Directory.Exists(invalidJson.FullName));
        Assert.True(Directory.Exists(invalidPath.FullName));
        Assert.False(Directory.Exists(deletion.PendingPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoverPending_marks_needs_attention_when_original_path_is_not_a_regular_directory(
        bool reparsePoint)
    {
        Directory.CreateDirectory(versionRoot);
        var instanceRoot = Path.Combine(versionRoot, "practice");
        var stateRoot = Directory.CreateDirectory(GetStateRoot("practice-id")).FullName;
        string? linkTarget = null;
        if (reparsePoint)
        {
            linkTarget = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(),
                $"crystalfly-delete-original-link-{Guid.NewGuid():N}")).FullName;
            Directory.CreateSymbolicLink(instanceRoot, linkTarget);
        }
        else
        {
            await File.WriteAllTextAsync(instanceRoot, "not a directory");
        }
        var operationRoot = await WriteRecoveryJournalAsync(
            "unsafe-original",
            "practice-id",
            TransactionState.Prepared,
            instanceRoot,
            stateRoot);
        try
        {
            var recovery = Assert.Single(await CreateService().RecoverPendingAsync());

            Assert.False(recovery.Completed);
            Assert.True(Directory.Exists(operationRoot));
            await AssertNeedsAttentionAsync(operationRoot);
        }
        finally
        {
            if (reparsePoint && Directory.Exists(instanceRoot))
            {
                Directory.Delete(instanceRoot);
            }
            if (linkTarget is not null && Directory.Exists(linkTarget))
            {
                Directory.Delete(linkTarget, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoverPending_marks_needs_attention_when_pending_path_is_not_a_regular_directory(
        bool reparsePoint)
    {
        var instance = await CreateInstanceAsync("practice", "practice-id");
        var failedCleanup = CreateService(
            deleteDirectory: _ => throw new IOException("Injected cleanup failure."));
        var deletion = await failedCleanup.DeleteAsync(instance, ReadyConditions());
        var pendingGame = Path.Combine(deletion.PendingPath, "game");
        string? linkTarget = null;
        if (reparsePoint)
        {
            linkTarget = Path.Combine(
                Path.GetTempPath(),
                $"crystalfly-delete-pending-link-{Guid.NewGuid():N}");
            Directory.Move(pendingGame, linkTarget);
            Directory.CreateSymbolicLink(pendingGame, linkTarget);
        }
        else
        {
            Directory.Delete(pendingGame, recursive: true);
            await File.WriteAllTextAsync(pendingGame, "not a directory");
        }
        try
        {
            var recovery = Assert.Single(await CreateService().RecoverPendingAsync());

            Assert.False(recovery.Completed);
            Assert.True(Directory.Exists(deletion.PendingPath));
            await AssertNeedsAttentionAsync(deletion.PendingPath);
        }
        finally
        {
            if (reparsePoint && Directory.Exists(pendingGame))
            {
                Directory.Delete(pendingGame);
            }
            if (linkTarget is not null && Directory.Exists(linkTarget))
            {
                Directory.Delete(linkTarget, recursive: true);
            }
        }
    }

    private async Task<InstanceRecord> CreateInstanceAsync(string name, string id)
    {
        var root = Directory.CreateDirectory(Path.Combine(versionRoot, name)).FullName;
        await File.WriteAllTextAsync(Path.Combine(root, "hollow_knight.exe"), "game");
        return await SaveInstanceAsync(root, id);
    }

    private static async Task<InstanceRecord> SaveInstanceAsync(string root, string id)
    {
        var instance = new InstanceRecord
        {
            Id = id,
            Name = Path.GetFileName(root),
            RootPath = root,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(instance);
        return instance;
    }

    private string GetStateRoot(string instanceId) =>
        Path.Combine(versionRoot, ".crystalfly", "instances", instanceId);

    private static async Task AssertNeedsAttentionAsync(string pendingPath)
    {
        await using var stream = File.OpenRead(Path.Combine(pendingPath, "delete.json"));
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("needsAttention", document.RootElement.GetProperty("state").GetString());
    }

    private async Task<string> WriteRecoveryJournalAsync(
        string operationId,
        string instanceId,
        TransactionState state,
        string instanceRoot,
        string stateRoot)
    {
        var operationRoot = Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "delete-pending",
            operationId)).FullName;
        await File.WriteAllTextAsync(
            Path.Combine(operationRoot, "delete.json"),
            CrystalflyJson.Serialize(new
            {
                schemaVersion = 1,
                operationId,
                instanceId,
                state,
                instanceRoot,
                stateRoot,
                pendingRoot = operationRoot,
                error = (string?)null
            }));
        return operationRoot;
    }

    private InstanceDeletionService CreateService(
        bool isRunning = false,
        Action<string, string>? moveDirectory = null,
        Action<string>? deleteDirectory = null) =>
        new(
            versionRoot,
            new StubProcessProbe(isRunning),
            () => Guid.NewGuid().ToString("N"),
            moveDirectory ?? Directory.Move,
            deleteDirectory ?? (path => Directory.Delete(path, recursive: true)));

    private static InstanceDeletionConditions ReadyConditions() => new()
    {
        TransactionsHealthy = true
    };

    public void Dispose()
    {
        if (Directory.Exists(versionRoot))
        {
            Directory.Delete(versionRoot, recursive: true);
        }
    }

    private sealed class StubProcessProbe(bool isRunning) : IHollowKnightProcessProbe
    {
        public bool IsRunning() => isRunning;
    }
}
