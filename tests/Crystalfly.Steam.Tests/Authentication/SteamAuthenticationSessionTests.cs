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

    [Fact]
    public async Task Guard_callback_receives_device_and_email_code_requests()
    {
        Directory.CreateDirectory(root);
        var deviceRequests = 0;
        var emailRequests = 0;
        var callback = new RecordingGuardCallback(
            getDeviceCode: previousIncorrect =>
            {
                deviceRequests++;
                Assert.False(previousIncorrect);
                return Task.FromResult("12345");
            },
            getEmailCode: (email, previousIncorrect) =>
            {
                emailRequests++;
                Assert.Equal("me@example.com", email);
                return Task.FromResult("54321");
            });
        await using var session = new SteamAuthenticationSession(
            new DpapiRefreshTokenStore(Path.Combine(root, "steam-token.dat")),
            guardCallback: callback);

        Assert.Equal("12345", await session.InvokeGuardDeviceCodeAsync(previousCodeWasIncorrect: false));
        Assert.Equal("54321", await session.InvokeGuardEmailCodeAsync("me@example.com", previousCodeWasIncorrect: false));
        Assert.Equal(1, deviceRequests);
        Assert.Equal(1, emailRequests);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingGuardCallback : ISteamGuardCallback
    {
        private readonly Func<bool, Task<string>> getDeviceCode;
        private readonly Func<string, bool, Task<string>> getEmailCode;

        public RecordingGuardCallback(
            Func<bool, Task<string>> getDeviceCode,
            Func<string, bool, Task<string>> getEmailCode)
        {
            this.getDeviceCode = getDeviceCode;
            this.getEmailCode = getEmailCode;
        }

        public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect) =>
            getDeviceCode(previousCodeWasIncorrect);

        public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect) =>
            getEmailCode(email, previousCodeWasIncorrect);

        public Task<bool> AcceptDeviceConfirmationAsync() => Task.FromResult(true);
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
