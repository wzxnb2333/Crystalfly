using System.Net;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Crystalfly.Core.Networking;

public sealed record SystemProxySnapshot(
    bool Enabled,
    string Mode,
    string? Endpoint,
    string? AutomaticConfigurationUrl,
    string? BypassList = null)
{
    public static SystemProxySnapshot Direct { get; } = new(false, "Direct", null, null);
}

public sealed class SystemProxyChangedEventArgs(SystemProxySnapshot previous, SystemProxySnapshot current)
    : EventArgs
{
    public SystemProxySnapshot Previous { get; } = previous;
    public SystemProxySnapshot Current { get; } = current;
}

public sealed class SystemProxyService : IWebProxy, IDisposable
{
    private const string InternetSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private readonly Func<IWebProxy> proxyResolver;
    private readonly Func<SystemProxySnapshot> snapshotProvider;
    private readonly Timer? monitor;
    private readonly object sync = new();
    private SystemProxySnapshot current;
    private volatile bool disposed;

    public SystemProxyService(TimeSpan? pollingInterval = null)
        : this(
            WebRequest.GetSystemWebProxy,
            CaptureSnapshot,
            startMonitoring: true,
            pollingInterval)
    {
    }

    internal SystemProxyService(
        Func<IWebProxy> proxyResolver,
        Func<SystemProxySnapshot> snapshotProvider,
        bool startMonitoring,
        TimeSpan? pollingInterval = null)
    {
        this.proxyResolver = proxyResolver;
        this.snapshotProvider = snapshotProvider;
        current = snapshotProvider();
        Credentials = CredentialCache.DefaultCredentials;
        if (startMonitoring)
        {
            TimeSpan interval = pollingInterval ?? TimeSpan.FromSeconds(2);
            monitor = new Timer(static state => ((SystemProxyService)state!).RefreshSafely(), this, interval, interval);
        }
    }

    public event EventHandler<SystemProxyChangedEventArgs>? Changed;

    public SystemProxySnapshot Current
    {
        get
        {
            lock (sync)
            {
                return current;
            }
        }
    }

    public ICredentials? Credentials { get; set; }

    public Uri GetProxy(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        IWebProxy proxy = ResolveProxy();
        if (proxy.IsBypassed(destination))
        {
            return destination;
        }

        Uri? resolved = proxy.GetProxy(destination);
        return IsUsableProxyEndpoint(resolved) && !Uri.Equals(resolved, destination)
            ? resolved!
            : destination;
    }

    public bool IsBypassed(Uri host)
    {
        ArgumentNullException.ThrowIfNull(host);
        IWebProxy proxy = ResolveProxy();
        if (proxy.IsBypassed(host))
        {
            return true;
        }

        Uri? resolved = proxy.GetProxy(host);
        return resolved is null
            || Uri.Equals(resolved, host)
            || !IsUsableProxyEndpoint(resolved);
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SystemProxySnapshot next = snapshotProvider();
        SystemProxySnapshot previous;
        lock (sync)
        {
            previous = current;
            if (previous == next)
            {
                return;
            }
            current = next;
        }
        Changed?.Invoke(this, new SystemProxyChangedEventArgs(previous, next));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        monitor?.Dispose();
    }

    private IWebProxy ResolveProxy()
    {
        IWebProxy proxy = proxyResolver();
        proxy.Credentials = Credentials;
        return proxy;
    }

    private void RefreshSafely()
    {
        try
        {
            Refresh();
        }
        catch (ObjectDisposedException) when (disposed)
        {
        }
        catch (Exception) when (!disposed)
        {
            // A transient registry/PAC read failure must not stop future checks.
        }
    }

    private static SystemProxySnapshot CaptureSnapshot()
    {
        if (OperatingSystem.IsWindows())
        {
            SystemProxySnapshot? windows = CaptureWindowsSnapshot();
            if (windows is not null)
            {
                return windows;
            }
        }

        string? environmentProxy = Normalize(Environment.GetEnvironmentVariable("HTTPS_PROXY"))
            ?? Normalize(Environment.GetEnvironmentVariable("HTTP_PROXY"))
            ?? Normalize(Environment.GetEnvironmentVariable("ALL_PROXY"));
        return environmentProxy is null
            ? SystemProxySnapshot.Direct
            : new SystemProxySnapshot(true, "Environment", environmentProxy, null);
    }

    [SupportedOSPlatform("windows")]
    private static SystemProxySnapshot? CaptureWindowsSnapshot()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey);
            bool enabled = key?.GetValue("ProxyEnable") is int value && value != 0;
            string? endpoint = Normalize(key?.GetValue("ProxyServer") as string);
            string? pac = Normalize(key?.GetValue("AutoConfigURL") as string);
            string? bypass = Normalize(key?.GetValue("ProxyOverride") as string);
            if (enabled && endpoint is not null)
            {
                return new SystemProxySnapshot(true, "HTTP", endpoint, pac, bypass);
            }
            return pac is null ? null : new SystemProxySnapshot(true, "PAC", null, pac, bypass);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUsableProxyEndpoint(Uri? proxy)
    {
        if (proxy is null)
        {
            return false;
        }
        if (!proxy.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !proxy.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(proxy.Host)
            || !string.IsNullOrEmpty(proxy.Query)
            || !string.IsNullOrEmpty(proxy.Fragment))
        {
            return false;
        }

        // A system proxy is an endpoint, not a reverse-proxy URL. A path such
        // as /https://github.com/... makes HttpClient attempt to tunnel through
        // the full content URL and results in a misleading 400 response.
        return string.IsNullOrEmpty(proxy.AbsolutePath) || proxy.AbsolutePath == "/";
    }
}
