using System.Net;
using Crystalfly.Steam.Authentication;
using Crystalfly.Steam.Security;
using SteamKit2;

namespace Crystalfly.Steam.Tests.Authentication;

public sealed class SteamAuthenticationSessionTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Default_Steam_client_uses_the_supplied_proxy_and_websocket_transport()
    {
        Directory.CreateDirectory(root);
        var proxy = new RecordingProxy();
        await using var session = new SteamAuthenticationSession(
            new DpapiRefreshTokenStore(Path.Combine(root, "steam-token.dat")),
            systemProxy: proxy);

        Assert.Equal(ProtocolTypes.WebSocket, session.Client.Configuration.ProtocolTypes);
        using HttpClient client = session.Client.Configuration.HttpClientFactory(HttpClientPurpose.WebAPI);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.GetAsync("http://steam.test/", cancellation.Token));
        Assert.Contains(proxy.RequestedDestinations, uri => uri.Host == "steam.test");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingProxy : IWebProxy
    {
        public List<Uri> RequestedDestinations { get; } = [];
        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            RequestedDestinations.Add(destination);
            return new Uri("http://127.0.0.1:1");
        }

        public bool IsBypassed(Uri host) => false;
    }
}
