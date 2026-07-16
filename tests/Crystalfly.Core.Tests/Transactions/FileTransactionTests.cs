using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Tests.Transactions;

public sealed class FileTransactionTests
{
    [Fact]
    public async Task ApplyDirectory_replaces_files_and_removes_restore_point_after_commit()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        await File.WriteAllTextAsync(Path.Combine(staging, "existing.txt"), "new");
        await File.WriteAllTextAsync(Path.Combine(staging, "nested", "added.txt").CreateParent(), "added");
        await File.WriteAllTextAsync(Path.Combine(target, "existing.txt"), "old");

        var result = await FileTransaction.ApplyDirectoryAsync(staging, target, journals, "install-package");

        Assert.Equal(TransactionState.Committed, result.State);
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(target, "existing.txt")));
        Assert.Equal("added", await File.ReadAllTextAsync(Path.Combine(target, "nested", "added.txt")));
        Assert.Equal(2, result.Changes.Count);
        Assert.Contains(result.Changes, change => change.RelativePath == "existing.txt"
            && change.OriginalSha256 == Sha256("old")
            && change.AppliedSha256 == Sha256("new"));
        Assert.Contains(result.Changes, change => change.RelativePath == "nested/added.txt"
            && change.OriginalSha256 is null
            && change.AppliedSha256 == Sha256("added"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ApplyDirectory_restores_earlier_changes_when_a_later_write_fails()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        await File.WriteAllTextAsync(Path.Combine(staging, "a.txt"), "new-a");
        await File.WriteAllTextAsync(Path.Combine(staging, "z.txt"), "new-z");
        await File.WriteAllTextAsync(Path.Combine(target, "a.txt"), "old-a");
        await File.WriteAllTextAsync(Path.Combine(target, "z.txt"), "old-z");

        await using (var locked = new FileStream(
            Path.Combine(target, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() =>
                FileTransaction.ApplyDirectoryAsync(staging, target, journals, "install-package"));
        }

        Assert.Equal("old-a", await File.ReadAllTextAsync(Path.Combine(target, "a.txt")));
        Assert.Equal("old-z", await File.ReadAllTextAsync(Path.Combine(target, "z.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task RecoverPending_rolls_back_applying_journal()
    {
        using var test = new TestDirectory();
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var restorePoint = test.CreateDirectory("journals", "tx-1");
        var backup = test.CreateDirectory("journals", "tx-1", "backup");
        await File.WriteAllTextAsync(Path.Combine(target, "existing.txt"), "new");
        await File.WriteAllTextAsync(Path.Combine(target, "added.txt"), "added");
        await File.WriteAllTextAsync(Path.Combine(backup, "existing.txt"), "old");
        var journal = new TransactionJournal
        {
            Id = "tx-1",
            Operation = "install-package",
            State = TransactionState.Applying,
            CreatedAt = DateTimeOffset.UtcNow,
            RootPath = target,
            RestorePointPath = restorePoint,
            Changes =
            [
                new TransactionFileChange
                {
                    RelativePath = "existing.txt",
                    BackupRelativePath = "backup/existing.txt",
                    OriginalSha256 = Sha256("old"),
                    AppliedSha256 = Sha256("new")
                },
                new TransactionFileChange
                {
                    RelativePath = "added.txt",
                    AppliedSha256 = Sha256("added")
                }
            ]
        };
        await AtomicJsonStore.WriteAsync(Path.Combine(restorePoint, "journal.json"), journal);

        var results = await FileTransaction.RecoverPendingAsync(journals);

        Assert.Single(results);
        Assert.Equal(TransactionState.RolledBack, results[0].State);
        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(target, "existing.txt")));
        Assert.False(File.Exists(Path.Combine(target, "added.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task RecoverPending_marks_journal_when_new_file_cannot_be_safely_removed()
    {
        using var test = new TestDirectory();
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var restorePoint = test.CreateDirectory("journals", "tx-unsafe");
        await File.WriteAllTextAsync(Path.Combine(target, "added.txt"), "changed-externally");
        var journal = new TransactionJournal
        {
            Id = "tx-unsafe",
            Operation = "install-package",
            State = TransactionState.Applying,
            CreatedAt = DateTimeOffset.UtcNow,
            RootPath = target,
            RestorePointPath = restorePoint,
            Changes =
            [
                new TransactionFileChange
                {
                    RelativePath = "added.txt",
                    AppliedSha256 = Sha256("transaction-content")
                }
            ]
        };
        var journalPath = Path.Combine(restorePoint, "journal.json");
        await AtomicJsonStore.WriteAsync(journalPath, journal);

        var results = await FileTransaction.RecoverPendingAsync(journals);

        Assert.Equal(TransactionState.NeedsAttention, Assert.Single(results).State);
        Assert.Equal("changed-externally", await File.ReadAllTextAsync(Path.Combine(target, "added.txt")));
        Assert.True(File.Exists(journalPath));
        Assert.Equal(
            TransactionState.NeedsAttention,
            (await AtomicJsonStore.ReadAsync<TransactionJournal>(journalPath)).State);
    }

    [Fact]
    public async Task RecoverPending_never_removes_created_directory_outside_target_root()
    {
        using var test = new TestDirectory();
        var target = test.CreateDirectory("target");
        var outside = test.CreateDirectory("outside");
        var journals = test.CreateDirectory("journals");
        var restorePoint = test.CreateDirectory("journals", "tx-invalid");
        var journalPath = Path.Combine(restorePoint, "journal.json");
        await AtomicJsonStore.WriteAsync(journalPath, new TransactionJournal
        {
            Id = "tx-invalid",
            Operation = "install-package",
            State = TransactionState.Applying,
            CreatedAt = DateTimeOffset.UtcNow,
            RootPath = target,
            RestorePointPath = restorePoint,
            CreatedDirectories = [outside]
        });

        var result = Assert.Single(await FileTransaction.RecoverPendingAsync(journals));

        Assert.Equal(TransactionState.NeedsAttention, result.State);
        Assert.True(Directory.Exists(outside));
        Assert.True(File.Exists(journalPath));
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private sealed class TestDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(_root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}

file static class TestPathExtensions
{
    public static string CreateParent(this string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }
}
