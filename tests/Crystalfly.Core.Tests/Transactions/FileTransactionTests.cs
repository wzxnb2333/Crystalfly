using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Tests.Transactions;

public sealed class FileTransactionTests
{
    [Fact]
    public async Task RollBackLatest_uses_persisted_changes_and_removed_directories()
    {
        using var test = new TestDirectory();
        var target = test.CreateDirectory("target");
        var restorePoint = test.CreateDirectory("journals", "tx-latest");
        var backup = test.CreateDirectory("journals", "tx-latest", "backup");
        var existing = Path.Combine(target, "existing.txt");
        var slot = Path.Combine(target, "slot");
        var restoredEmptyDirectory = Path.Combine(slot, "empty");
        await File.WriteAllTextAsync(existing, "new");
        await File.WriteAllTextAsync(slot, "new-file");
        await File.WriteAllTextAsync(Path.Combine(backup, "existing.txt"), "original");
        var journalPath = Path.Combine(restorePoint, "journal.json");
        await AtomicJsonStore.WriteAsync(journalPath, new TransactionJournal
        {
            Id = "tx-latest",
            Operation = "restore-snapshot",
            State = TransactionState.Applying,
            CreatedAt = DateTimeOffset.UtcNow,
            RootPath = target,
            RestorePointPath = restorePoint,
            RemovedDirectories = [slot, restoredEmptyDirectory],
            Changes =
            [
                new TransactionFileChange
                {
                    RelativePath = "existing.txt",
                    BackupRelativePath = "backup/existing.txt",
                    OriginalSha256 = Sha256("original"),
                    AppliedSha256 = Sha256("new")
                },
                new TransactionFileChange
                {
                    RelativePath = "slot",
                    AppliedSha256 = Sha256("new-file")
                }
            ]
        });

        var result = await FileTransaction.RollBackLatestAsync(journalPath);

        Assert.Equal(TransactionState.RolledBack, result.State);
        Assert.Equal("original", await File.ReadAllTextAsync(existing));
        Assert.True(Directory.Exists(restoredEmptyDirectory));
    }

    [Fact]
    public async Task ApplyDirectory_rejects_reparse_point_staging_root()
    {
        using var test = new TestDirectory();
        var target = test.CreateDirectory("target");
        await File.WriteAllTextAsync(Path.Combine(target, "shared.txt"), "original");
        var staging = test.CreateDirectoryLink(target, "staging-link");

        var exception = await Assert.ThrowsAsync<IOException>(() => FileTransaction.ApplyDirectoryAsync(
            staging,
            target,
            test.CreateDirectory("journals"),
            "reject-reparse"));

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(target, "shared.txt")));
    }

    [Fact]
    public async Task ApplyDirectory_rejects_reparse_point_target_root_for_empty_transaction()
    {
        using var test = new TestDirectory();
        var target = test.CreateDirectoryLink(test.CreateDirectory("real-target"), "target-link");
        var journals = test.CreateDirectory("journals");

        var exception = await Assert.ThrowsAsync<IOException>(() => FileTransaction.ApplyDirectoryAsync(
            test.CreateDirectory("staging"),
            target,
            journals,
            "reject-reparse"));

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ApplyDirectory_rejects_target_root_below_reparse_point()
    {
        using var test = new TestDirectory();
        var realParent = test.CreateDirectory("real-parent");
        var linkedParent = test.CreateDirectoryLink(realParent, "linked-parent");
        var target = Path.Combine(linkedParent, "target");
        var staging = test.CreateDirectory("staging");
        var journals = test.CreateDirectory("journals");
        await File.WriteAllTextAsync(Path.Combine(staging, "new.txt"), "new");

        var exception = await Assert.ThrowsAsync<IOException>(() => FileTransaction.ApplyDirectoryAsync(
            staging,
            target,
            journals,
            "reject-reparse-parent"));

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(realParent, "target")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(realParent));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ApplyDirectory_rejects_reparse_points_inside_staging_tree()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var linkedSource = test.CreateDirectory("linked-source");
        await File.WriteAllTextAsync(Path.Combine(linkedSource, "outside.txt"), "outside");
        Directory.CreateSymbolicLink(Path.Combine(staging, "linked"), linkedSource);
        var target = test.CreateDirectory("target");

        var exception = await Assert.ThrowsAsync<IOException>(() => FileTransaction.ApplyDirectoryAsync(
            staging,
            target,
            test.CreateDirectory("journals"),
            "reject-reparse"));

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

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
    public async Task ApplyDirectory_preserves_empty_staging_directories()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        Directory.CreateDirectory(Path.Combine(staging, "hollow_knight_Data", "Managed", "Mods"));

        var result = await FileTransaction.ApplyDirectoryAsync(
            staging, target, journals, "install-loader");

        Assert.Equal(TransactionState.Committed, result.State);
        Assert.True(Directory.Exists(Path.Combine(
            target, "hollow_knight_Data", "Managed", "Mods")));
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
    public async Task ReplaceDirectory_applies_new_files_and_removes_only_declared_files()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        await File.WriteAllTextAsync(Path.Combine(staging, "new.txt"), "new");
        await File.WriteAllTextAsync(Path.Combine(target, "old.txt"), "old");
        await File.WriteAllTextAsync(Path.Combine(target, "keep.txt"), "keep");

        var result = await FileTransaction.ReplaceDirectoryAsync(
            staging, target, journals, "switch-loader", ["old.txt"]);

        Assert.Equal(TransactionState.Committed, result.State);
        Assert.True(File.Exists(Path.Combine(target, "new.txt")));
        Assert.False(File.Exists(Path.Combine(target, "old.txt")));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(target, "keep.txt")));
        Assert.Contains(result.Changes, change => change.RelativePath == "old.txt" && change.IsDeletion);
    }

    [Fact]
    public async Task ReplaceDirectory_removes_declared_empty_directory()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var obsolete = test.CreateDirectory("target", "obsolete");

        var result = await FileTransaction.ReplaceDirectoryAsync(
            staging, target, journals, "switch-loader", ["obsolete"]);

        Assert.Equal(TransactionState.Committed, result.State);
        Assert.False(Directory.Exists(obsolete));
        Assert.Contains(obsolete, result.RemovedDirectories, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_removes_declared_directory_tree_after_declared_files()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var core = test.CreateDirectory("target", "BepInEx", "core");
        await File.WriteAllTextAsync(Path.Combine(core, "BepInEx.dll"), "old");

        var result = await FileTransaction.ReplaceDirectoryAsync(
            staging,
            target,
            journals,
            "switch-loader",
            ["BepInEx", "BepInEx/core", "BepInEx/core/BepInEx.dll"]);

        Assert.Equal(TransactionState.Committed, result.State);
        Assert.False(Directory.Exists(Path.Combine(target, "BepInEx")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_restores_declared_directory_tree_when_later_write_fails()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var core = test.CreateDirectory("target", "BepInEx", "core");
        var loader = Path.Combine(core, "BepInEx.dll");
        await File.WriteAllTextAsync(loader, "old-loader");
        await File.WriteAllTextAsync(Path.Combine(staging, "z.txt"), "new-z");
        var laterTarget = Path.Combine(target, "z.txt");
        await File.WriteAllTextAsync(laterTarget, "old-z");

        await using (var locked = new FileStream(
            laterTarget, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = await Assert.ThrowsAnyAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
                staging,
                target,
                journals,
                "switch-loader",
                ["BepInEx", "BepInEx/core", "BepInEx/core/BepInEx.dll"]));
            Assert.DoesNotContain("Declared removal", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(Directory.Exists(core));
        Assert.Equal("old-loader", await File.ReadAllTextAsync(loader));
        Assert.Equal("old-z", await File.ReadAllTextAsync(laterTarget));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_rejects_incomplete_declared_directory_tree()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var nested = test.CreateDirectory("target", "obsolete", "nested");
        var file = Path.Combine(nested, "old.txt");
        await File.WriteAllTextAsync(file, "old");

        var exception = await Assert.ThrowsAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
            staging,
            target,
            journals,
            "switch-loader",
            ["obsolete", "obsolete/nested/old.txt"]));

        Assert.Contains("not explicitly declared", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("old", await File.ReadAllTextAsync(file));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_rejects_reparse_point_in_declared_directory_tree()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var outside = test.CreateDirectory("outside");
        var outsideFile = Path.Combine(outside, "keep.txt");
        await File.WriteAllTextAsync(outsideFile, "keep");
        var obsolete = test.CreateDirectory("target", "obsolete");
        _ = Directory.CreateSymbolicLink(Path.Combine(obsolete, "linked"), outside);

        var exception = await Assert.ThrowsAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
            staging,
            target,
            journals,
            "switch-loader",
            ["obsolete", "obsolete/linked"]));

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep", await File.ReadAllTextAsync(outsideFile));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_restores_removed_empty_directory_when_later_write_fails()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var obsolete = test.CreateDirectory("target", "obsolete");
        await File.WriteAllTextAsync(Path.Combine(staging, "z.txt"), "new");
        await File.WriteAllTextAsync(Path.Combine(target, "z.txt"), "old");

        await using (var locked = new FileStream(
            Path.Combine(target, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = await Assert.ThrowsAnyAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
                staging, target, journals, "switch-loader", ["obsolete"]));
            Assert.DoesNotContain("Declared removal", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(Directory.Exists(obsolete));
        Assert.Empty(Directory.EnumerateFileSystemEntries(obsolete));
        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(target, "z.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_rejects_declared_non_empty_directory()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var keep = Path.Combine(test.CreateDirectory("target", "obsolete"), "keep.txt");
        await File.WriteAllTextAsync(keep, "keep");

        var exception = await Assert.ThrowsAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
            staging, target, journals, "switch-loader", ["obsolete"]));

        Assert.Contains("not empty", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep", await File.ReadAllTextAsync(keep));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_rolls_back_writes_when_a_declared_delete_fails()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        await File.WriteAllTextAsync(Path.Combine(staging, "a.txt"), "new");
        await File.WriteAllTextAsync(Path.Combine(target, "a.txt"), "old");
        await File.WriteAllTextAsync(Path.Combine(target, "z.txt"), "remove");

        await using (var locked = new FileStream(
            Path.Combine(target, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
                staging, target, journals, "switch-loader", ["z.txt"]));
        }

        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(target, "a.txt")));
        Assert.Equal("remove", await File.ReadAllTextAsync(Path.Combine(target, "z.txt")));
    }

    [Fact]
    public async Task ReplaceDirectory_restores_file_when_created_directory_write_later_fails()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        await File.WriteAllTextAsync(Path.Combine(staging, "slot", "new.txt").CreateParent(), "new");
        await File.WriteAllTextAsync(Path.Combine(staging, "z.txt"), "new-z");
        await File.WriteAllTextAsync(Path.Combine(target, "slot"), "original-file");
        await File.WriteAllTextAsync(Path.Combine(target, "z.txt"), "old-z");

        await using (var locked = new FileStream(
            Path.Combine(target, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
                staging, target, journals, "restore-snapshot", ["slot"]));
        }

        Assert.True(File.Exists(Path.Combine(target, "slot")));
        Assert.Equal("original-file", await File.ReadAllTextAsync(Path.Combine(target, "slot")));
        Assert.False(Directory.Exists(Path.Combine(target, "slot")));
        Assert.Equal("old-z", await File.ReadAllTextAsync(Path.Combine(target, "z.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_restores_file_after_empty_directory_replacement_when_later_write_fails()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        _ = test.CreateDirectory("staging", "slot", "nested");
        await File.WriteAllTextAsync(Path.Combine(staging, "z.txt"), "new-z");
        await File.WriteAllTextAsync(Path.Combine(target, "slot"), "original-file");
        await File.WriteAllTextAsync(Path.Combine(target, "z.txt"), "old-z");

        await using (var locked = new FileStream(
            Path.Combine(target, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
                staging, target, journals, "restore-snapshot", ["slot"]));
        }

        Assert.True(File.Exists(Path.Combine(target, "slot")));
        Assert.Equal("original-file", await File.ReadAllTextAsync(Path.Combine(target, "slot")));
        Assert.False(Directory.Exists(Path.Combine(target, "slot")));
        Assert.Equal("old-z", await File.ReadAllTextAsync(Path.Combine(target, "z.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_restores_directory_tree_when_file_write_later_fails()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        await File.WriteAllTextAsync(Path.Combine(staging, "slot"), "new-file");
        await File.WriteAllTextAsync(Path.Combine(staging, "z.txt"), "new-z");
        await File.WriteAllTextAsync(Path.Combine(target, "slot", "nested", "old.txt").CreateParent(), "original");
        _ = test.CreateDirectory("target", "slot", "empty");
        await File.WriteAllTextAsync(Path.Combine(target, "z.txt"), "old-z");

        await using (var locked = new FileStream(
            Path.Combine(target, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => FileTransaction.ReplaceDirectoryAsync(
                staging, target, journals, "restore-snapshot", ["slot/nested/old.txt"]));
        }

        Assert.True(Directory.Exists(Path.Combine(target, "slot")));
        Assert.Equal(
            "original",
            await File.ReadAllTextAsync(Path.Combine(target, "slot", "nested", "old.txt")));
        Assert.True(Directory.Exists(Path.Combine(target, "slot", "empty")));
        Assert.Equal("old-z", await File.ReadAllTextAsync(Path.Combine(target, "z.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journals));
    }

    [Fact]
    public async Task ReplaceDirectory_rejects_delete_paths_outside_target()
    {
        using var test = new TestDirectory();
        var staging = test.CreateDirectory("staging");
        var target = test.CreateDirectory("target");

        await Assert.ThrowsAsync<InvalidDataException>(() => FileTransaction.ReplaceDirectoryAsync(
            staging,
            target,
            test.CreateDirectory("journals"),
            "uninstall-loader",
            ["../outside.txt"]));
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
    public async Task RecoverPending_restores_directory_tree_removed_for_file_target()
    {
        using var test = new TestDirectory();
        var target = test.CreateDirectory("target");
        var journals = test.CreateDirectory("journals");
        var restorePoint = test.CreateDirectory("journals", "tx-type-change");
        var backup = test.CreateDirectory("journals", "tx-type-change", "backup", "slot", "nested");
        await File.WriteAllTextAsync(Path.Combine(target, "slot"), "new-file");
        await File.WriteAllTextAsync(Path.Combine(backup, "old.txt"), "original");
        var slot = Path.Combine(target, "slot");
        var nested = Path.Combine(slot, "nested");
        var empty = Path.Combine(slot, "empty");
        await AtomicJsonStore.WriteAsync(Path.Combine(restorePoint, "journal.json"), new TransactionJournal
        {
            Id = "tx-type-change",
            Operation = "restore-snapshot",
            State = TransactionState.Applying,
            CreatedAt = DateTimeOffset.UtcNow,
            RootPath = target,
            RestorePointPath = restorePoint,
            RemovedDirectories = [nested, empty, slot],
            Changes =
            [
                new TransactionFileChange
                {
                    RelativePath = "slot/nested/old.txt",
                    IsDeletion = true,
                    BackupRelativePath = "backup/slot/nested/old.txt",
                    OriginalSha256 = Sha256("original"),
                    AppliedSha256 = Sha256("original")
                },
                new TransactionFileChange
                {
                    RelativePath = "slot",
                    AppliedSha256 = Sha256("new-file")
                }
            ]
        });

        var result = Assert.Single(await FileTransaction.RecoverPendingAsync(journals));

        Assert.Equal(TransactionState.RolledBack, result.State);
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(nested, "old.txt")));
        Assert.True(Directory.Exists(empty));
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

        public string CreateDirectoryLink(string target, params string[] parts)
        {
            var path = parts.Aggregate(_root, Path.Combine);
            Directory.CreateSymbolicLink(path, target);
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
