using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Crystalfly.Core.Diagnostics;
using Sentry;
using Sentry.Protocol;
using Sentry.Protocol.Envelopes;

namespace Crystalfly.App.Diagnostics;

internal sealed class ErrorReportingService : IErrorReporter, IDisposable
{
    private readonly object sync = new();
    private readonly LocalErrorReportStore store;
    private readonly string cachePath;
    private readonly string release;
    private readonly string? dsn;
    private readonly Func<HttpMessageHandler> handlerFactory;
    private readonly ReportingNetworkGate gate = new();
    private readonly ConcurrentDictionary<SentryId, ErrorReport> pending = new();
    private readonly Timer maintenance;
    private SentryClient? client;
    private HttpClient? httpClient;
    private bool enabled;
    private bool offline = true;
    private bool ready;
    private bool fatalCaptured;
    private bool disposed;

    public ErrorReportingService(string dataRoot, string? dsn, string release,
        Func<HttpMessageHandler>? handlerFactory = null)
    {
        var root = Path.Combine(dataRoot, "diagnostics");
        store = new LocalErrorReportStore(Path.Combine(root, "reports"));
        cachePath = Path.Combine(root, "cache");
        this.dsn = ValidateDsn(dsn) ? dsn : null;
        this.release = ErrorReport.Identifier(release);
        this.handlerFactory = handlerFactory ?? (() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        maintenance = new Timer(_ => Maintain(), null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
    }

    public bool IsConfigured => dsn is not null;
    public string Destination => dsn is null ? string.Empty : new Uri(dsn).Host;

    // No inherited SENTRY_DSN or credentials; source/fork/test builds have no destination by default.
    internal static bool ValidateDsn(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.UserInfo)
        && uri.UserInfo.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
        && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && !uri.IsLoopback && uri.AbsolutePath.Trim('/').All(char.IsAsciiDigit)
        && uri.AbsolutePath.Trim('/').Length > 0;

    public void Configure(bool enabled, bool offline, bool settingsReady = true)
    {
        lock (sync)
        {
            if (disposed) return;
            this.enabled = enabled;
            this.offline = offline;
            ready = settingsReady;
            gate.SetAllowed(false);
            try
            {
                if (!enabled || !settingsReady)
                {
                    StopClient();
                    if (!enabled && settingsReady) ClearCache();
                    return;
                }
                if (client is null && IsConfigured)
                {
                    PruneCache();
                    httpClient = new HttpClient(handlerFactory()) { Timeout = TimeSpan.FromSeconds(5) };
                    var options = CreateOptions();
                    options.Transport = new ReportingTransport(options, gate, httpClient, CacheIsSafe);
                    client = new SentryClient(options);
                }
                gate.SetAllowed(!offline && !fatalCaptured && IsConfigured);
            }
            catch
            {
                // Diagnostics must not break settings, startup, or application operations.
                gate.SetAllowed(false);
                StopClient();
            }
        }
    }

    private SentryOptions CreateOptions()
    {
        var options = new SentryOptions
        {
            Dsn = dsn, Release = release, Environment = "production", ServerName = string.Empty,
            SendDefaultPii = false, IsEnvironmentUser = false, AttachStacktrace = false,
            AutoSessionTracking = false, TracesSampleRate = 0, EnableLogs = false,
            SendClientReports = false, MaxBreadcrumbs = 0, ReportAssembliesMode = ReportAssembliesMode.None,
            EnableBackpressureHandling = false, DisableSentryHttpMessageHandler = true,
            CacheDirectoryPath = cachePath, MaxCacheItems = ErrorReport.MaxReports,
            MaxQueueItems = ErrorReport.MaxReports, InitCacheFlushTimeout = TimeSpan.Zero,
            ShutdownTimeout = TimeSpan.FromMilliseconds(500), NetworkStatusListener = gate
        };
        options.DisableAppDomainUnhandledExceptionCapture();
        options.DisableUnobservedTaskExceptionCapture();
        options.DisableDiagnosticSourceIntegration();
        options.DisableSystemDiagnosticsMetricsIntegration();
        options.DisableAppDomainProcessExitFlush();
        options.SetBeforeBreadcrumb(_ => null);
        options.SetBeforeSendTransaction(_ => null);
        options.SetBeforeSend(@event =>
        {
            try
            {
                if (!pending.TryRemove(@event.EventId, out var report)) return null;
                // Return a new event instead of trying to remove all fields added by SDK enrichment.
                var clean = ToSentryEvent(report);
                using var envelope = Envelope.FromEvent(clean);
                using var bytes = new MemoryStream();
                envelope.Serialize(bytes, null);
                return bytes.Length <= ErrorReport.MaxBytes && ReportingWirePolicy.IsAllowed(bytes.ToArray()) ? clean : null;
            }
            catch { return null; }
        });
        return options;
    }

    internal static SentryEvent ToSentryEvent(ErrorReport report)
    {
        var result = new SentryEvent
        {
            Release = report.Release, Environment = "production", Platform = "csharp",
            Logger = "Crystalfly", Level = report.Fatal ? SentryLevel.Fatal : SentryLevel.Error,
            Message = $"{report.Operation} failed", Fingerprint = [report.Fingerprint],
            SentryExceptions = report.Errors.Select(error => new SentryException
            {
                Type = error.Type, Value = $"Error code {error.Code}",
                Stacktrace = new SentryStackTrace
                {
                    Frames = error.Frames.Reverse().Select(frame => new SentryStackFrame
                    {
                        Module = frame.Module, Function = $"{frame.Type}.{frame.Method}",
                        InApp = frame.Module.StartsWith("Crystalfly", StringComparison.Ordinal)
                    }).ToList()
                }
            }).ToArray()
        };
        result.SetTag("operation", report.Operation.ToString());
        result.SetTag("os_version", report.OperatingSystem);
        result.SetTag("architecture", report.Architecture);
        result.SetTag("report_id", report.Id.ToString("N"));
        return result;
    }

    public void Capture(Exception exception, ErrorOperation operation, bool fatal = false)
    {
        try
        {
            if (!fatal && ErrorReport.ShouldIgnore(exception)) return;
            lock (sync)
            {
                if (disposed || fatal && fatalCaptured) return;
                var report = ErrorReport.Create(exception, operation, fatal, release, DateTimeOffset.UtcNow);
                if (!store.TryAdd(report)) return;
                if (fatal)
                {
                    fatalCaptured = true;
                    gate.SetAllowed(false);
                }
                if (!ready || !enabled || client is null) return;
                PruneCache();
                var sentryEvent = ToSentryEvent(report);
                pending[sentryEvent.EventId] = report;
                try { client.CaptureEvent(sentryEvent); }
                finally { pending.TryRemove(sentryEvent.EventId, out _); }
                // The SDK worker first stores the envelope; networking stays paused during fatal handling.
                if (fatal) client.FlushAsync(TimeSpan.FromMilliseconds(500)).GetAwaiter().GetResult();
            }
        }
        catch { /* Never recurse into error reporting from its own failure path. */ }
    }

    internal Task FlushAsync(TimeSpan timeout)
    {
        lock (sync) return client?.FlushAsync(timeout) ?? Task.CompletedTask;
    }

    public byte[] Export() => JsonSerializer.SerializeToUtf8Bytes(store.Read(DateTimeOffset.UtcNow), ErrorReport.JsonOptions);

    private void Maintain()
    {
        lock (sync)
        {
            if (disposed) return;
            try { store.Read(DateTimeOffset.UtcNow); PruneCache(); }
            catch { gate.SetAllowed(false); }
        }
    }

    private bool CacheIsSafe()
    {
        try { _ = DiagnosticFiles.GetFiles(cachePath); return true; }
        catch { return false; }
    }

    private void PruneCache()
    {
        var now = DateTime.UtcNow;
        var envelopes = DiagnosticFiles.GetFiles(cachePath).Where(file => file.Extension == ".envelope")
            .OrderByDescending(file => file.LastWriteTimeUtc).ToArray();
        for (var index = 0; index < envelopes.Length; index++)
        {
            var file = envelopes[index];
            if (index >= ErrorReport.MaxReports || file.Length > ErrorReport.MaxBytes
                || now - file.LastWriteTimeUtc > ErrorReport.Retention)
            {
                try { file.Delete(); } catch (IOException) { }
            }
        }
    }

    private void ClearCache()
    {
        foreach (var file in DiagnosticFiles.GetFiles(cachePath)) file.Delete();
    }

    private void StopClient()
    {
        client?.Dispose();
        client = null;
        httpClient?.Dispose();
        httpClient = null;
        pending.Clear();
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            maintenance.Dispose();
            gate.SetAllowed(false);
            StopClient();
            gate.Dispose();
        }
    }

    public static string BuildRelease => typeof(ErrorReportingService).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "development";
}
