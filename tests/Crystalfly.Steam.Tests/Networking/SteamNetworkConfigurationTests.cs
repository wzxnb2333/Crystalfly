using System.Net;
using System.Net.Sockets;
using System.Text;
using Crystalfly.Steam.Networking;
using SteamKit2;

namespace Crystalfly.Steam.Tests.Networking;

public sealed class SteamNetworkConfigurationTests
{
    [Fact]
    public void Configuration_uses_the_proxy_friendly_websocket_transport()
    {
        SteamConfiguration configuration = SteamNetworkConfiguration.Create(new WebProxy());

        Assert.Equal(ProtocolTypes.WebSocket, configuration.ProtocolTypes);
    }

    [Fact]
    public async Task Configuration_prefers_the_proxy_compatible_https_CM_endpoint()
    {
        SteamConfiguration configuration = SteamNetworkConfiguration.Create(new WebProxy());

        var servers = (await configuration.ServerListProvider.FetchServerListAsync()).ToArray();

        Assert.NotEmpty(servers);
        Assert.All(servers, server =>
        {
            Assert.Equal(443, server.GetPort());
            Assert.Equal(ProtocolTypes.WebSocket, server.ProtocolTypes);
        });
    }

    [Fact]
    public async Task Every_Steam_http_purpose_routes_requests_through_the_supplied_proxy()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var proxy = new RecordingProxy(new Uri($"http://127.0.0.1:{port}"));
        SteamConfiguration configuration = SteamNetworkConfiguration.Create(proxy);

        foreach (HttpClientPurpose purpose in Enum.GetValues<HttpClientPurpose>())
        {
            using HttpClient client = configuration.HttpClientFactory(purpose);
            Task<HttpResponseMessage> request = client.GetAsync($"http://steam.test/{purpose}");
            using TcpClient connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using NetworkStream stream = connection.GetStream();
            string requestLine = await ReadRequestLineAsync(stream);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
            using HttpResponseMessage response = await request.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.StartsWith($"GET http://steam.test/{purpose}", requestLine, StringComparison.Ordinal);
        }

        Assert.Equal(3, proxy.RequestedDestinations.Count);
    }

    private static async Task<string> ReadRequestLineAsync(NetworkStream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        string requestLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) ?? string.Empty;
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))))
        {
        }
        return requestLine;
    }

    private sealed class RecordingProxy(Uri endpoint) : IWebProxy
    {
        public List<Uri> RequestedDestinations { get; } = [];
        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            RequestedDestinations.Add(destination);
            return endpoint;
        }

        public bool IsBypassed(Uri host) => false;
    }
}
