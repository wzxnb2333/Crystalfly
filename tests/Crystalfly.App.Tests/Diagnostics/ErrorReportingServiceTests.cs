using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Crystalfly.App.Diagnostics;
using Crystalfly.Core.Diagnostics;

namespace Crystalfly.App.Tests.Diagnostics;

public sealed class ErrorReportingServiceTests : IDisposable
{
    internal const string TestDsn = "https://public@errors.example.invalid/1";
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly-Reporting-" + Guid.NewGuid());

    [Fact]
    public async Task Only_sanitized_whitelisted_fields_reach_wire_and_disk()
    {
        var handler = new Recorder();
        using var service = new ErrorReportingService(root, TestDsn, "1.2.3+abc", () => handler);
        service.Configure(true, false);
        service.Capture(SensitiveException(), ErrorOperation.SaveWrite);
        await handler.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await service.FlushAsync(TimeSpan.FromSeconds(2));
        var bytes = Assert.Single(handler.Requests);
        Assert.True(ReportingWirePolicy.IsAllowed(bytes), Encoding.UTF8.GetString(bytes));
        AssertSanitized(Encoding.UTF8.GetString(bytes));
        AssertSanitized(Encoding.UTF8.GetString(service.Export()));
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            AssertSanitized(await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Offline_and_unreadable_settings_never_send_and_off_clears_queue()
    {
        var handler = new Recorder();
        using var service = new ErrorReportingService(root, TestDsn, "1.0", () => handler);
        service.Configure(true, false, settingsReady: false);
        service.Capture(new IOException(), ErrorOperation.Settings);
        Assert.Empty(handler.Requests);
        service.Configure(true, true);
        service.Capture(new IOException(), ErrorOperation.Download);
        await service.FlushAsync(TimeSpan.FromMilliseconds(700));
        Assert.Empty(handler.Requests);
        Assert.NotEmpty(Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories));
        service.Configure(false, false);
        Assert.Empty(Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories));
        Assert.NotEqual("[]", Encoding.UTF8.GetString(service.Export()));
    }

    [Fact]
    public async Task Restart_replays_only_sdk_queue_after_returning_online()
    {
        using (var offline = new ErrorReportingService(root, TestDsn, "1.0"))
        {
            offline.Configure(true, true);
            offline.Capture(new IOException(), ErrorOperation.Install);
            await offline.FlushAsync(TimeSpan.FromMilliseconds(700));
        }
        var handler = new Recorder();
        using var service = new ErrorReportingService(root, TestDsn, "1.0", () => handler);
        service.Configure(true, false);
        await handler.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await service.FlushAsync(TimeSpan.FromSeconds(2));
        Assert.Single(handler.Requests);
        Assert.Empty(Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Disabling_cancels_in_flight_request_and_clears_pending_cache()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Recorder(async token =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var service = new ErrorReportingService(root, TestDsn, "1.0", () => handler);
        service.Configure(true, false);
        service.Capture(new IOException(), ErrorOperation.Install);
        await handler.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(8));
        service.Configure(false, false);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories));
        service.Capture(new InvalidOperationException(), ErrorOperation.Download);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(0)]
    public async Task Server_failures_back_off_without_recursive_reports(int status)
    {
        var handler = new Recorder(_ =>
        {
            if (status == 0) throw new TaskCanceledException("timeout");
            var response = new HttpResponseMessage((HttpStatusCode)status);
            response.Headers.TryAddWithoutValidation("Retry-After", "120");
            return Task.FromResult(response);
        });
        using var service = new ErrorReportingService(root, TestDsn, "1.0", () => handler);
        service.Configure(true, false);
        service.Capture(new IOException(), ErrorOperation.Download);
        await handler.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await service.FlushAsync(TimeSpan.FromMilliseconds(700));
        service.Capture(new InvalidOperationException(), ErrorOperation.Install);
        await service.FlushAsync(TimeSpan.FromMilliseconds(700));
        Assert.Single(handler.Requests);
        Assert.NotEmpty(Directory.GetFiles(root, "*.envelope", SearchOption.AllDirectories));
        using var json = System.Text.Json.JsonDocument.Parse(service.Export());
        Assert.Equal(2, json.RootElement.GetArrayLength());
    }

    [Fact]
    public void Unconfigured_source_build_can_export_without_creating_network_client()
    {
        Assert.Empty(ErrorReportingRuntime.GetMetadata("ErrorReportingDsn"));
        using var service = new ErrorReportingService(root, null, "1.0", () => throw new InvalidOperationException("Network forbidden"));
        service.Configure(true, false);
        service.Capture(SensitiveException(), ErrorOperation.Download);
        Assert.False(service.IsConfigured);
        Assert.Contains("SaveSensitive", Encoding.UTF8.GetString(service.Export()));
        Assert.False(ErrorReportingService.ValidateDsn("http://public@host.invalid/1"));
        Assert.False(ErrorReportingService.ValidateDsn("https://public:private@host.invalid/1"));
        Assert.False(ErrorReportingService.ValidateDsn("https://public@host.invalid/1?token=secret"));
    }

    [Fact]
    public async Task Failed_preferences_reload_pauses_an_already_initialized_reporter()
    {
        var handler = new Recorder();
        using var service = new ErrorReportingService(root, TestDsn, "1.0", () => handler);
        service.Configure(true, false);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "broken");
        var viewModel = new Crystalfly.App.ViewModels.MainViewModel(root, errorReporterOverride: service);
        try
        {
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => viewModel.InitializeAsync());
            service.Capture(new IOException(), ErrorOperation.Settings);
            await service.FlushAsync(TimeSpan.FromMilliseconds(100));
            Assert.Empty(handler.Requests);
        }
        finally
        {
            // Disposal observes the failed initialization task by design.
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => viewModel.DisposeAsync().AsTask());
        }
    }

    private static Exception SensitiveException()
    {
        try { SaveSensitive(); return new Exception(); }
        catch (IOException exception) { return exception; }
    }
    private static void SaveSensitive() => throw new IOException(@"C:\Users\Alice\user1.dat Bearer secret https://example.invalid/?token=secret");
    private static void AssertSanitized(string text)
    {
        foreach (var forbidden in new[] { "Alice", "secret", "user1.dat", "access_token", "server_name", "\"user\"", "\"request\"", "breadcrumbs", "environment_variables" })
            Assert.DoesNotContain(forbidden, text);
    }

    internal sealed class Recorder(Func<CancellationToken, Task<HttpResponseMessage>>? respond = null) : HttpMessageHandler
    {
        internal ConcurrentQueue<byte[]> Requests { get; } = new();
        internal TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Enqueue(await request.Content!.ReadAsByteArrayAsync(token));
            Arrived.TrySetResult();
            return respond is null ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") } : await respond(token);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
