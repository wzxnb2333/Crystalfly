using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using Crystalfly.Steam.Authentication;

namespace Crystalfly.App.Services;

public sealed class SteamGuardPromptCallback(
    Func<bool, Task<string?>> getDeviceCode,
    Func<string, bool, Task<string?>> getEmailCode,
    Func<Task<bool?>>? acceptDeviceConfirmation = null) : ISteamGuardCallback
{
    private readonly Func<bool, Task<string?>> getDeviceCode = getDeviceCode;
    private readonly Func<string, bool, Task<string?>> getEmailCode = getEmailCode;
    private readonly Func<Task<bool?>>? acceptDeviceConfirmation = acceptDeviceConfirmation;

    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect) =>
        CompleteWithCodeAsync(() => getDeviceCode(previousCodeWasIncorrect));

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect) =>
        CompleteWithCodeAsync(() => getEmailCode(email, previousCodeWasIncorrect));

    public async Task<bool> AcceptDeviceConfirmationAsync()
    {
        if (acceptDeviceConfirmation is null)
            return false;
        bool? confirmed = await RunOnUiThreadAsync(() => acceptDeviceConfirmation());
        return confirmed ?? throw new OperationCanceledException("Steam device confirmation was cancelled.");
    }

    private static async Task<string> CompleteWithCodeAsync(Func<Task<string?>> request)
    {
        string? code = await RunOnUiThreadAsync(request);
        return code ?? throw new OperationCanceledException("Steam Guard code entry was cancelled.");
    }

    private static async Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        if (!HasActiveUiWindow()
            || !Dispatcher.UIThread.SupportsRunLoops
            || Dispatcher.UIThread.CheckAccess())
        {
            return await action();
        }

        Task<T> inner = await Dispatcher.UIThread.InvokeAsync<Task<T>>(action);
        return await inner;
    }

    private static bool HasActiveUiWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
        {
            MainWindow: not null
        };
}
