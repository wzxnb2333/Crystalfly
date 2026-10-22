using System.Diagnostics;
using Crystalfly.App.Diagnostics;
using Crystalfly.Core.Diagnostics;

namespace Crystalfly.App.Tests.Diagnostics;

public sealed class CrashReportingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly-Crash-" + Guid.NewGuid());

    [Fact]
    public async Task Unhandled_child_process_crash_is_preserved_and_replayed_on_restart()
    {
        var crash = await RunProbeAsync("crash");
        Assert.NotEqual(0, crash.ExitCode);
        var cache = Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories);
        Assert.Single(cache);
        var bytes = await File.ReadAllBytesAsync(cache[0]);
        Assert.True(ReportingWirePolicy.IsAllowed(bytes));
        Assert.DoesNotContain(root, System.Text.Encoding.UTF8.GetString(bytes));
        var replay = await RunProbeAsync("replay");
        Assert.True(replay.ExitCode == 0, replay.Output);
        Assert.Contains("\"status\":200", replay.Output);
        Assert.Empty(Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Corrupt_expired_and_oversized_cache_do_not_block_a_valid_report()
    {
        using (var offline = new ErrorReportingService(root, ErrorReportingServiceTests.TestDsn, "1.0"))
        {
            offline.Configure(true, true);
            offline.Capture(new IOException(), ErrorOperation.Download);
            await offline.FlushAsync(TimeSpan.FromMilliseconds(700));
        }
        var good = Assert.Single(Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories));
        var directory = Path.GetDirectoryName(good)!;
        var corrupt = Path.Combine(directory, "broken.envelope");
        await File.WriteAllTextAsync(corrupt, "broken");
        var oversized = Path.Combine(directory, "oversized.envelope");
        await File.WriteAllBytesAsync(oversized, new byte[ErrorReport.MaxBytes + 1]);
        var expired = Path.Combine(directory, "expired.envelope");
        File.Copy(good, expired);
        File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-8));
        var handler = new ErrorReportingServiceTests.Recorder();
        using var service = new ErrorReportingService(root, ErrorReportingServiceTests.TestDsn, "1.0", () => handler);
        service.Configure(true, false);
        await handler.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await service.FlushAsync(TimeSpan.FromSeconds(2));
        Assert.Single(handler.Requests);
        Assert.False(File.Exists(expired));
        Assert.False(File.Exists(oversized));
    }

    private async Task<(int ExitCode, string Output)> RunProbeAsync(string mode)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(directory.FullName, "tests", "Crystalfly.Diagnostics.Probe", "bin", configuration,
            "net10.0-windows10.0.17763.0", "Crystalfly.Diagnostics.Probe.dll");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add(mode);
        start.ArgumentList.Add(root);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        catch { process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
