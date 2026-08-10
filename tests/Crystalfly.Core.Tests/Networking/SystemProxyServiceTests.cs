using System.Net;
using Crystalfly.Core.Networking;

namespace Crystalfly.Core.Tests.Networking;

public sealed class SystemProxyServiceTests
{
    [Fact]
    public void Requests_resolve_the_current_system_proxy_instead_of_caching_the_first_proxy()
    {
        var proxies = new Queue<IWebProxy>(
        [
            new WebProxy("http://127.0.0.1:7890"),
            new WebProxy("http://127.0.0.1:7891")
        ]);
        using var service = new SystemProxyService(
            () => proxies.Dequeue(),
            () => SystemProxySnapshot.Direct,
            startMonitoring: false);
        var destination = new Uri("https://store.steampowered.com/");

        Uri first = service.GetProxy(destination);
        Uri second = service.GetProxy(destination);

        Assert.Equal(new Uri("http://127.0.0.1:7890"), first);
        Assert.Equal(new Uri("http://127.0.0.1:7891"), second);
    }

    [Fact]
    public void Refresh_raises_changed_only_when_the_effective_proxy_settings_change()
    {
        var snapshots = new Queue<SystemProxySnapshot>(
        [
            SystemProxySnapshot.Direct,
            SystemProxySnapshot.Direct,
            new(true, "HTTP", "127.0.0.1:7890", null),
            new(true, "HTTP", "127.0.0.1:7890", null),
            new(true, "PAC", null, "https://proxy.test/config.pac")
        ]);
        using var service = new SystemProxyService(
            () => WebRequest.GetSystemWebProxy(),
            () => snapshots.Dequeue(),
            startMonitoring: false);
        var changes = new List<SystemProxySnapshot>();
        service.Changed += (_, args) => changes.Add(args.Current);

        service.Refresh();
        service.Refresh();
        service.Refresh();
        service.Refresh();

        Assert.Equal(2, changes.Count);
        Assert.Equal("127.0.0.1:7890", changes[0].Endpoint);
        Assert.Equal("https://proxy.test/config.pac", changes[1].AutomaticConfigurationUrl);
    }

    [Fact]
    public void Requests_ignore_reverse_proxy_content_urls_misreported_as_system_proxies()
    {
        var destination = new Uri(
            "https://github.com/wzxnb2333/Crystalfly/releases/latest/download/update-manifest.v1.json");
        using var service = new SystemProxyService(
            () => new WebProxy(
                "https://gh-proxy.com/https://github.com/wzxnb2333/Crystalfly/releases/latest/download/update-manifest.v1.json"),
            () => new SystemProxySnapshot(
                true,
                "HTTP",
                "https://gh-proxy.com/https://github.com/wzxnb2333/Crystalfly/releases/latest/download/update-manifest.v1.json",
                null),
            startMonitoring: false);

        Uri proxy = service.GetProxy(destination);

        Assert.Equal(destination, proxy);
    }

    [Fact]
    public void Direct_resolver_result_is_reported_as_bypassed_instead_of_used_as_a_tunnel()
    {
        var destination = new Uri("https://raw.githubusercontent.com/owner/repo/main/catalog.json");
        using var service = new SystemProxyService(
            () => new DestinationProxy(),
            () => SystemProxySnapshot.Direct,
            startMonitoring: false);

        Assert.True(service.IsBypassed(destination));
        Assert.Equal(destination, service.GetProxy(destination));
    }

    private sealed class DestinationProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination) => destination;

        public bool IsBypassed(Uri host) => false;
    }
}
