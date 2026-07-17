using Crystalfly.Steam.Security;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;

namespace Crystalfly.Steam.Authentication;

public sealed class SteamAuthenticationSession : IAsyncDisposable
{
    private readonly DpapiRefreshTokenStore _tokenStore;
    private readonly SteamClient _client;
    private readonly CallbackManager _callbacks;
    private readonly SteamUser _user;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _loggedOn = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _loginGate = new(1, 1);
    private readonly GuardAuthenticator? _authenticator;
    private Task? _callbackPump;
    private bool _loginStarted;
    private bool _loginCompleted;

    public SteamAuthenticationSession(
        DpapiRefreshTokenStore tokenStore,
        ISteamGuardCallback? guardCallback = null,
        SteamClient? client = null)
    {
        _tokenStore = tokenStore;
        _client = client ?? new SteamClient();
        _callbacks = new CallbackManager(_client);
        _user = _client.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler is unavailable.");
        _authenticator = guardCallback is null ? null : new GuardAuthenticator(guardCallback);
        _callbacks.Subscribe<SteamClient.ConnectedCallback>(_ => _connected.TrySetResult());
        _callbacks.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbacks.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
    }

    public event EventHandler<QrChallengeEventArgs>? QrChallengeChanged;

    public SteamClient Client => _client;
    public bool IsLoggedOn => _loginCompleted && _client.IsConnected;

    public async Task<RefreshTokenCredential> ConnectWithStoredTokenAsync(
        CancellationToken cancellationToken = default)
    {
        RefreshTokenCredential credential = await _tokenStore.LoadAsync(cancellationToken)
            ?? throw new InvalidOperationException("No stored Steam refresh token was found.");
        await ConnectWithRefreshTokenAsync(credential, persist: false, cancellationToken);
        return credential;
    }

    public async Task ConnectWithRefreshTokenAsync(
        RefreshTokenCredential credential,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        await _loginGate.WaitAsync(cancellationToken);
        try
        {
            EnsureLoginNotStarted();
            await ConnectAsync(cancellationToken);
            _user.LogOn(new SteamUser.LogOnDetails
            {
                Username = credential.AccountName,
                AccessToken = credential.RefreshToken,
                ShouldRememberPassword = true
            });
            await _loggedOn.Task.WaitAsync(cancellationToken);
            if (persist)
                await _tokenStore.SaveAsync(credential, cancellationToken);
        }
        finally
        {
            _loginGate.Release();
        }
    }

    public async Task<RefreshTokenCredential> ConnectWithQrAsync(CancellationToken cancellationToken = default)
    {
        await _loginGate.WaitAsync(cancellationToken);
        try
        {
            EnsureLoginNotStarted();
            await ConnectAsync(cancellationToken);
            QrAuthSession authSession = await _client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails
            {
                Authenticator = _authenticator,
                ClientOSType = EOSType.Windows10,
                DeviceFriendlyName = "Crystalfly",
                IsPersistentSession = true,
                PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_SteamClient
            });
            authSession.ChallengeURLChanged = () => RaiseQrChallenge(authSession.ChallengeURL);
            RaiseQrChallenge(authSession.ChallengeURL);
            AuthPollResult result = await authSession.PollingWaitForResultAsync(cancellationToken);
            var credential = new RefreshTokenCredential(result.AccountName, result.RefreshToken);
            _user.LogOn(new SteamUser.LogOnDetails
            {
                Username = credential.AccountName,
                AccessToken = credential.RefreshToken,
                ShouldRememberPassword = true
            });
            await _loggedOn.Task.WaitAsync(cancellationToken);
            await _tokenStore.SaveAsync(credential, cancellationToken);
            return credential;
        }
        finally
        {
            _loginGate.Release();
        }
    }

    public void SignOut()
    {
        Exception? tokenDeleteException = null;
        try
        {
            _tokenStore.Delete();
        }
        catch (Exception exception)
        {
            tokenDeleteException = exception;
        }

        try
        {
            if (_client.IsConnected)
                _user.LogOff();
        }
        finally
        {
            _client.Disconnect();
        }

        if (tokenDeleteException is not null)
            throw new IOException("The Steam refresh token could not be deleted.", tokenDeleteException);
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _client.Disconnect();
        try
        {
            if (_callbackPump is not null)
            {
                try
                {
                    await _callbackPump;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            _loginGate.Dispose();
            _lifetime.Dispose();
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _callbackPump ??= PumpCallbacksAsync();
        _client.Connect();
        await _connected.Task.WaitAsync(cancellationToken);
    }

    private async Task PumpCallbacksAsync()
    {
        while (!_lifetime.IsCancellationRequested)
            await _callbacks.RunWaitCallbackAsync(_lifetime.Token);
    }

    private void EnsureLoginNotStarted()
    {
        if (_loginStarted || _client.IsConnected)
            throw new InvalidOperationException("This Steam session already has an active login.");
        _loginStarted = true;
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        if (callback.UserInitiated)
            return;

        var exception = new IOException("The Steam connection closed unexpectedly.");
        _connected.TrySetException(exception);
        _loggedOn.TrySetException(exception);
        _loginCompleted = false;
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.OK)
        {
            _loginCompleted = true;
            _loggedOn.TrySetResult();
            return;
        }

        _loggedOn.TrySetException(
            new InvalidOperationException($"Steam login failed: {callback.Result} / {callback.ExtendedResult}."));
    }

    private void RaiseQrChallenge(string challengeUrl) =>
        QrChallengeChanged?.Invoke(this, new QrChallengeEventArgs(challengeUrl));

    private sealed class GuardAuthenticator(ISteamGuardCallback callback) : IAuthenticator
    {
        public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect) =>
            callback.GetDeviceCodeAsync(previousCodeWasIncorrect);

        public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect) =>
            callback.GetEmailCodeAsync(email, previousCodeWasIncorrect);

        public Task<bool> AcceptDeviceConfirmationAsync() => callback.AcceptDeviceConfirmationAsync();
    }
}
