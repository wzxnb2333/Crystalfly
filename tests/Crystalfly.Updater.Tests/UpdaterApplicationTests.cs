namespace Crystalfly.Updater.Tests;

public sealed class UpdaterApplicationTests
{
    [Fact]
    public async Task RunAsync_waits_applies_portable_update_then_restarts()
    {
        List<string> calls = [];
        string target = Path.GetFullPath(@"C:\Apps\Crystalfly");
        string restart = Path.Combine(target, "Crystalfly.App.exe");

        int exitCode = await UpdaterApplication.RunAsync(
            [
                "--parent-pid", "42",
                "--mode", "Portable",
                "--asset", @"C:\updates\release.zip",
                "--size", "1",
                "--sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                "--target", target,
                "--restart", restart
            ],
            (_, _, _) => { calls.Add("wait"); return Task.CompletedTask; },
            (_, _, _, _) => Task.FromResult<IDisposable>(new NoopLease()),
            (_, _) => throw new InvalidOperationException("installer must not run"),
            (_, _, _) =>
            {
                calls.Add("apply");
                return Task.FromResult<PortableUpdateOperation?>(null);
            },
            (path, _) => calls.Add("restart:" + path),
            (_, _) => { calls.Add("complete"); return Task.CompletedTask; },
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(["wait", "apply", "restart:" + restart], calls);
    }

    [Fact]
    public async Task RunAsync_returns_installer_exit_code_without_restart_on_failure()
    {
        List<string> calls = [];

        int exitCode = await UpdaterApplication.RunAsync(
            [
                "--parent-pid", "42",
                "--mode", "Installed",
                "--asset", @"C:\updates\setup.exe",
                "--size", "1",
                "--sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                "--target", @"C:\Apps\Crystalfly",
                "--restart", @"C:\Apps\Crystalfly\Crystalfly.App.exe"
            ],
            (_, _, _) => { calls.Add("wait"); return Task.CompletedTask; },
            (_, _, _, _) => Task.FromResult<IDisposable>(new NoopLease()),
            (_, _) => { calls.Add("install"); return Task.FromResult(7); },
            (_, _, _) => throw new InvalidOperationException("portable must not run"),
            (_, _) => calls.Add("restart"),
            (_, _) => { calls.Add("complete"); return Task.CompletedTask; },
            CancellationToken.None);

        Assert.Equal(7, exitCode);
        Assert.Equal(["wait", "install"], calls);
    }

    [Fact]
    public async Task RunAsync_preserves_portable_backup_when_restart_fails()
    {
        List<string> calls = [];
        PortableUpdateOperation operation = new(
            @"C:\Apps\Crystalfly",
            @"C:\Apps\.crystalfly-update-recovery\operation",
            @"C:\Apps\.crystalfly-update-recovery\operation\backup",
            @"C:\Apps\.crystalfly-update-recovery\operation\operation.json",
            @"C:\Apps\.crystalfly-update-recovery\operation\healthy");

        await Assert.ThrowsAsync<IOException>(() => UpdaterApplication.RunAsync(
            [
                "--parent-pid", "42",
                "--mode", "Portable",
                "--asset", @"C:\updates\release.zip",
                "--size", "1",
                "--sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                "--target", @"C:\Apps\Crystalfly",
                "--restart", @"C:\Apps\Crystalfly\Crystalfly.App.exe"
            ],
            (_, _, _) => Task.CompletedTask,
            (_, _, _, _) => Task.FromResult<IDisposable>(new NoopLease()),
            (_, _) => throw new InvalidOperationException("installer must not run"),
            (_, _, _) => Task.FromResult<PortableUpdateOperation?>(operation),
            (_, _) => throw new IOException("simulated restart failure"),
            (_, _) => { calls.Add("complete"); return Task.CompletedTask; },
            CancellationToken.None));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_deletes_verified_asset_after_a_determined_failure()
    {
        string asset = Path.Combine(Path.GetTempPath(), $"Crystalfly-update-{Guid.NewGuid():N}.exe");
        await File.WriteAllTextAsync(asset, "verified asset");

        int exitCode = await UpdaterApplication.RunAsync(
            [
                "--parent-pid", "42",
                "--mode", "Installed",
                "--asset", asset,
                "--size", "13",
                "--sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                "--target", @"C:\Apps\Crystalfly"
            ],
            (_, _, _) => Task.CompletedTask,
            (_, _, _, _) => Task.FromResult<IDisposable>(new NoopLease()),
            (_, _) => Task.FromResult(7),
            (_, _, _) => throw new InvalidOperationException("portable must not run"),
            (_, _) => throw new InvalidOperationException("restart must not run"),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.Equal(7, exitCode);
        Assert.False(File.Exists(asset));
    }

    private sealed class NoopLease : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
