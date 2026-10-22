using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Crystalfly.App.Diagnostics;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Diagnostics;
using Sentry.Protocol.Envelopes;

namespace Crystalfly.Diagnostics.Probe;

internal static class Program
{
    private const string FakeDsn = "https://public@errors.example.invalid/1";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 2) return 2;
        var mode = args[0];
        var root = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(root);
        if (mode == "inspect")
        {
            using var envelope = Envelope.FromEvent(ErrorReportingService.ToSentryEvent(
                ErrorReport.Create(SyntheticException(), ErrorOperation.Startup, false, "diagnostics-validation", DateTimeOffset.UtcNow)));
            using var bytes = new MemoryStream();
            envelope.Serialize(bytes, null);
            Console.WriteLine(Encoding.UTF8.GetString(bytes.ToArray()));
            Console.WriteLine("Allowed=" + ReportingWirePolicy.IsAllowed(bytes.ToArray()));
            return 0;
        }
        if (mode == "crash")
        {
            if (OperatingSystem.IsWindows()) SetErrorMode(0x0002 | 0x8000);
            await CrystalflySettingsStore.SaveAsync(Path.Combine(root, "settings.json"), new CrystalflySettings { OfflineMode = true });
            ErrorReportingRuntime.Start(root, FakeDsn);
            throw SyntheticException();
        }
        if (mode is not ("replay" or "smoke")) return 2;
        if (mode == "smoke" && (args.Length != 3 || !ErrorReportingService.ValidateDsn(args[2]))) return 2;
        var sent = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = new ErrorReportingService(root, mode == "smoke" ? args[2] : FakeDsn,
            "diagnostics-validation-20260927", () => new ProbeHandler(mode == "smoke", sent));
        service.Configure(true, false);
        if (mode == "smoke") service.Capture(SyntheticException(), ErrorOperation.Startup);
        var status = await sent.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await service.FlushAsync(TimeSpan.FromSeconds(2));
        Console.WriteLine(JsonSerializer.Serialize(new { status, sanitizedReports = JsonDocument.Parse(service.Export()).RootElement.GetArrayLength() }));
        return status is >= 200 and < 300 ? 0 : 1;
    }

    private static Exception SyntheticException()
    {
        try { throw new InvalidOperationException("Synthetic Crystalfly diagnostics validation; no user data."); }
        catch (Exception exception) { return exception; }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    private sealed class ProbeHandler(bool realNetwork, TaskCompletionSource<int> sent) : DelegatingHandler(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = false
    })
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = realNetwork ? await base.SendAsync(request, token)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            sent.TrySetResult((int)response.StatusCode);
            return response;
        }
    }
}
