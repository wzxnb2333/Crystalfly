using System.Net;
using SteamKit2;
using SteamKit2.Discovery;

namespace Crystalfly.Steam.Networking;

public static class SteamNetworkConfiguration
{
    public static SteamConfiguration Create(IWebProxy proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        var serverList = new MemoryServerListProvider();
        // Steam Directory often ranks WebSocket endpoints on 270xx ahead of 443.
        // Forward proxies and Steam accelerators commonly only carry HTTPS on 443.
        serverList.UpdateServerListAsync(
            [ServerRecord.CreateWebSocketServer(SmartCMServerList.DefaultServerWebsocket)])
            .GetAwaiter()
            .GetResult();
        return SteamConfiguration.Create(builder => builder
            .WithProtocolTypes(ProtocolTypes.WebSocket)
            .WithServerListProvider(serverList)
            .WithConnectionTimeout(TimeSpan.FromSeconds(15))
            .WithHttpClientFactory(purpose => CreateHttpClient(proxy, purpose)));
    }

    private static HttpClient CreateHttpClient(IWebProxy proxy, HttpClientPurpose purpose)
    {
        var handler = new SocketsHttpHandler
        {
            Proxy = proxy,
            UseProxy = true,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
        return new HttpClient(handler)
        {
            Timeout = purpose switch
            {
                HttpClientPurpose.CMWebSocket => Timeout.InfiniteTimeSpan,
                HttpClientPurpose.CDN => TimeSpan.FromMinutes(30),
                _ => TimeSpan.FromSeconds(30)
            }
        };
    }
}
