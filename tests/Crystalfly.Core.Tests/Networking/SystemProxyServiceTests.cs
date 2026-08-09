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
}
