using System.IO.Compression;

namespace Crystalfly.Updater.Tests;

public sealed class PortableUpdateInstallerTests : IDisposable
{
    private readonly string testRoot = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly.Updater.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ApplyAsync_replaces_program_files_and_preserves_Data()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        File.WriteAllText(Path.Combine(target, "obsolete.dll"), "obsolete");
        Directory.CreateDirectory(Path.Combine(target, "Data"));
        File.WriteAllText(Path.Combine(target, "Data", "settings.json"), "user-settings");
        string asset = CreateZip(
            ("Crystalfly.App.exe", "new"),
            ("portable.flag", string.Empty),
            ("lib/runtime.dll", "runtime"),
            ("Data/settings.json", "package-default"));

        PortableUpdateOperation operation = await PortableUpdateInstaller.ApplyAsync(asset, target, CancellationToken.None);

        Assert.Equal("new", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        Assert.Equal("runtime", File.ReadAllText(Path.Combine(target, "lib", "runtime.dll")));
        Assert.False(File.Exists(Path.Combine(target, "obsolete.dll")));
        Assert.Equal("user-settings", File.ReadAllText(Path.Combine(target, "Data", "settings.json")));
        File.WriteAllText(operation.HealthFilePath, "healthy");
        await PortableUpdateInstaller.CompleteAsync(operation, CancellationToken.None);
        AssertNoWorkingDirectories();
    }

    [Theory]
    [InlineData("../escaped.txt")]
    [InlineData("folder/../../escaped.txt")]
    [InlineData("C:/escaped.txt")]
    public async Task ApplyAsync_rejects_zip_path_traversal_without_changing_target(string entryName)
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        string asset = CreateZip((entryName, "bad"), ("Crystalfly.App.exe", "new"), ("portable.flag", string.Empty));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            PortableUpdateInstaller.ApplyAsync(asset, target, CancellationToken.None));

        Assert.Contains("unsafe", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        Assert.False(File.Exists(Path.Combine(testRoot, "escaped.txt")));
        AssertNoWorkingDirectories();
    }

    [Fact]
    public async Task ApplyAsync_rejects_zip_symbolic_link_entry()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        string asset = Path.Combine(testRoot, "release.zip");
        using (FileStream stream = File.Create(asset))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("link");
            entry.ExternalAttributes = unchecked((int)0xA1FF0000);
            using StreamWriter writer = new(entry.Open());
            writer.Write("outside");
        }

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            PortableUpdateInstaller.ApplyAsync(asset, target, CancellationToken.None));

        Assert.Contains("reparse", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        AssertNoWorkingDirectories();
    }

    [Fact]
    public async Task ApplyAsync_rejects_reparse_point_target()
    {
        Directory.CreateDirectory(testRoot);
        string realTarget = Path.Combine(testRoot, "real-target");
        Directory.CreateDirectory(realTarget);
        string targetLink = Path.Combine(testRoot, "target-link");
        Directory.CreateSymbolicLink(targetLink, realTarget);
        string asset = CreateZip(("Crystalfly.App.exe", "new"), ("portable.flag", string.Empty));

        IOException exception = await Assert.ThrowsAsync<IOException>(() =>
            PortableUpdateInstaller.ApplyAsync(asset, targetLink, CancellationToken.None));

        Assert.Contains("reparse", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(realTarget));
        Directory.Delete(targetLink);
    }

    [Fact]
    public async Task ApplyAsync_rolls_back_when_backup_move_fails()
    {
        string target = CreateTarget();
        string movablePath = Path.Combine(target, "a-movable.dll");
        string lockedPath = Path.Combine(target, "z-locked.dll");
        File.WriteAllText(movablePath, "movable-old");
        File.WriteAllText(lockedPath, "locked-old");
        string asset = CreateZip(("Crystalfly.App.exe", "new"), ("portable.flag", string.Empty));
        using FileStream lockStream = new(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        await Assert.ThrowsAsync<IOException>(() =>
            PortableUpdateInstaller.ApplyAsync(asset, target, CancellationToken.None));

        Assert.Equal("movable-old", File.ReadAllText(movablePath));
        Assert.Equal("locked-old", File.ReadAllText(lockedPath));
        Assert.False(File.Exists(Path.Combine(target, "Crystalfly.App.exe")));
        AssertNoWorkingDirectories();
    }

    [Fact]
    public async Task ApplyAsync_rejects_package_without_program_files()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        string asset = CreateZip(("Data/settings.json", "default"));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            PortableUpdateInstaller.ApplyAsync(asset, target, CancellationToken.None));

        Assert.Contains("program file", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        AssertNoWorkingDirectories();
    }

    [Fact]
    public async Task ApplyAsync_rejects_package_without_portable_marker_without_changing_target()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        string asset = CreateZip(("Crystalfly.App.exe", "new"));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            PortableUpdateInstaller.ApplyAsync(asset, target, CancellationToken.None));

        Assert.Contains("portable.flag", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        AssertNoWorkingDirectories();
    }

    [Fact]
    public async Task ApplyAsync_rolls_back_old_program_when_installation_move_fails()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        File.WriteAllText(Path.Combine(target, "portable.flag"), string.Empty);
        string asset = CreateZip(("Crystalfly.App.exe", "new"), ("portable.flag", string.Empty));
        var moves = 0;

        await Assert.ThrowsAsync<IOException>(() => PortableUpdateInstaller.ApplyAsync(
            asset,
            target,
            CancellationToken.None,
            (source, destination) =>
            {
                moves++;
                if (moves == 3)
                {
                    throw new IOException("simulated install failure");
                }
                File.Move(source, destination);
            }));

        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        Assert.True(File.Exists(Path.Combine(target, "portable.flag")));
        AssertNoRecoveryDirectories();
    }

    [Fact]
    public async Task RecoverAsync_restores_backup_left_by_interrupted_update()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "partial-new");
        File.WriteAllText(Path.Combine(target, "portable.flag"), string.Empty);
        File.WriteAllText(Path.Combine(target, "new-runtime.dll"), "partial-new");
        PortableUpdateOperation operation = CreateInterruptedOperation(target);

        await PortableUpdateInstaller.RecoverAsync(target, CancellationToken.None);

        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        Assert.Equal("old-marker", File.ReadAllText(Path.Combine(target, "portable.flag")));
        Assert.False(File.Exists(Path.Combine(target, "new-runtime.dll")));
        Assert.False(Directory.Exists(operation.RecoveryDirectory));
    }

    [Fact]
    public async Task RecoverAsync_keeps_untouched_originals_when_interrupted_before_first_move()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        File.WriteAllText(Path.Combine(target, "portable.flag"), "old-marker");
        PortableUpdateOperation operation = CreateOperation(
            target,
            ["Crystalfly.App.exe", "portable.flag"]);

        await PortableUpdateInstaller.RecoverAsync(target, CancellationToken.None);

        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        Assert.Equal("old-marker", File.ReadAllText(Path.Combine(target, "portable.flag")));
        Assert.False(Directory.Exists(operation.RecoveryDirectory));
    }

    [Fact]
    public async Task RecoverAsync_removes_partial_new_files_when_original_inventory_was_empty()
    {
        string target = CreateTarget();
        Directory.CreateDirectory(Path.Combine(target, "Data"));
        PortableUpdateOperation operation = CreateOperation(target, []);
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "partial-new");
        File.WriteAllText(Path.Combine(target, "portable.flag"), string.Empty);

        await PortableUpdateInstaller.RecoverAsync(target, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(target, "Crystalfly.App.exe")));
        Assert.False(File.Exists(Path.Combine(target, "portable.flag")));
        Assert.True(Directory.Exists(Path.Combine(target, "Data")));
        Assert.False(Directory.Exists(operation.RecoveryDirectory));
    }

    [Fact]
    public async Task CompleteAsync_removes_backup_only_after_health_handshake()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "old");
        File.WriteAllText(Path.Combine(target, "portable.flag"), string.Empty);
        string asset = CreateZip(("Crystalfly.App.exe", "new"), ("portable.flag", string.Empty));

        PortableUpdateOperation operation = await PortableUpdateInstaller.ApplyAsync(asset, target, CancellationToken.None);

        Assert.True(Directory.Exists(operation.RecoveryDirectory));
        Assert.True(File.Exists(operation.OperationLogPath));
        File.WriteAllText(operation.HealthFilePath, "healthy");
        await PortableUpdateInstaller.CompleteAsync(operation, CancellationToken.None);

        Assert.False(Directory.Exists(operation.RecoveryDirectory));
    }

    [Fact]
    public async Task RecoverAsync_keeps_healthy_new_version_when_cleanup_was_interrupted()
    {
        string target = CreateTarget();
        File.WriteAllText(Path.Combine(target, "Crystalfly.App.exe"), "healthy-new");
        File.WriteAllText(Path.Combine(target, "portable.flag"), string.Empty);
        PortableUpdateOperation operation = CreateInterruptedOperation(target);
        File.WriteAllText(operation.HealthFilePath, "healthy");

        await PortableUpdateInstaller.RecoverAsync(target, CancellationToken.None);

        Assert.Equal("healthy-new", File.ReadAllText(Path.Combine(target, "Crystalfly.App.exe")));
        Assert.True(File.Exists(Path.Combine(target, "portable.flag")));
        Assert.False(Directory.Exists(operation.RecoveryDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private string CreateTarget()
    {
        Directory.CreateDirectory(testRoot);
        string target = Path.Combine(testRoot, "target");
        Directory.CreateDirectory(target);
        return target;
    }

    private string CreateZip(params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(testRoot);
        string asset = Path.Combine(testRoot, $"{Guid.NewGuid():N}.zip");
        using FileStream stream = File.Create(asset);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }

        return asset;
    }

    private void AssertNoWorkingDirectories()
    {
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(testRoot),
            path => Path.GetFileName(path).StartsWith(".crystalfly-", StringComparison.OrdinalIgnoreCase));
    }

    private void AssertNoRecoveryDirectories()
    {
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(testRoot),
            path => Path.GetFileName(path).Equals(".crystalfly-update-recovery", StringComparison.OrdinalIgnoreCase));
    }

    private void AssertRecoveryDirectoryRetained()
    {
        Assert.Contains(
            Directory.EnumerateDirectories(testRoot),
            path => Path.GetFileName(path).Equals(".crystalfly-update-recovery", StringComparison.OrdinalIgnoreCase));
    }

    private PortableUpdateOperation CreateInterruptedOperation(string target)
    {
        string parent = Directory.GetParent(target)!.FullName;
        string recovery = Path.Combine(parent, ".crystalfly-update-recovery", Guid.NewGuid().ToString("N"));
        string backup = Path.Combine(recovery, "backup");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "Crystalfly.App.exe"), "old");
        File.WriteAllText(Path.Combine(backup, "portable.flag"), "old-marker");
        var operation = new PortableUpdateOperation(
            target,
            recovery,
            backup,
            Path.Combine(recovery, "operation.json"),
            Path.Combine(recovery, "healthy"),
            ["Crystalfly.App.exe", "portable.flag"]);
        File.WriteAllText(operation.OperationLogPath, operation.Serialize());
        return operation;
    }

    private PortableUpdateOperation CreateOperation(
        string target,
        IReadOnlyList<string> originalEntryNames)
    {
        string parent = Directory.GetParent(target)!.FullName;
        string recovery = Path.Combine(parent, ".crystalfly-update-recovery", Guid.NewGuid().ToString("N"));
        string backup = Path.Combine(recovery, "backup");
        Directory.CreateDirectory(backup);
        var operation = new PortableUpdateOperation(
            target,
            recovery,
            backup,
            Path.Combine(recovery, "operation.json"),
            Path.Combine(recovery, "healthy"),
            originalEntryNames);
        File.WriteAllText(operation.OperationLogPath, operation.Serialize());
        return operation;
    }
}
