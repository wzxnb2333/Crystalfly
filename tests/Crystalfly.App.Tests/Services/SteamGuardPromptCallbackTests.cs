using Crystalfly.App.Services;
using Crystalfly.Steam.Authentication;

namespace Crystalfly.App.Tests.Services;

public sealed class SteamGuardPromptCallbackTests
{
    [Fact]
    public async Task AcceptDeviceConfirmation_returns_true_when_user_confirmed()
    {
        var callback = new SteamGuardPromptCallback(
            getDeviceCode: _ => Task.FromResult<string?>("12345"),
            getEmailCode: (_, _) => Task.FromResult<string?>("54321"),
            acceptDeviceConfirmation: () => Task.FromResult<bool?>(true));

        bool accepted = await callback.AcceptDeviceConfirmationAsync();

        Assert.True(accepted);
    }

    [Fact]
    public async Task AcceptDeviceConfirmation_returns_false_when_user_switches_to_code()
    {
        var callback = new SteamGuardPromptCallback(
            getDeviceCode: _ => Task.FromResult<string?>("12345"),
            getEmailCode: (_, _) => Task.FromResult<string?>("54321"),
            acceptDeviceConfirmation: () => Task.FromResult<bool?>(false));

        bool accepted = await callback.AcceptDeviceConfirmationAsync();

        Assert.False(accepted);
    }

    [Fact]
    public async Task AcceptDeviceConfirmation_throws_cancelled_when_user_closes_dialog()
    {
        var callback = new SteamGuardPromptCallback(
            getDeviceCode: _ => Task.FromResult<string?>("12345"),
            getEmailCode: (_, _) => Task.FromResult<string?>("54321"),
            acceptDeviceConfirmation: () => Task.FromResult<bool?>(null));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => callback.AcceptDeviceConfirmationAsync());
    }

    [Fact]
    public async Task AcceptDeviceConfirmation_returns_false_without_a_prompt_delegate()
    {
        var callback = new SteamGuardPromptCallback(
            getDeviceCode: _ => Task.FromResult<string?>("12345"),
            getEmailCode: (_, _) => Task.FromResult<string?>("54321"));

        bool accepted = await callback.AcceptDeviceConfirmationAsync();

        Assert.False(accepted);
    }

    [Fact]
    public async Task GuardCodeRequests_forward_to_the_supplied_delegates()
    {
        var deviceCodePrompt = new List<string?>();
        var emailCodePrompt = new List<string?>();
        var callback = new SteamGuardPromptCallback(
            getDeviceCode: previous => { deviceCodePrompt.Add(previous.ToString()); return Task.FromResult<string?>("11111"); },
            getEmailCode: (email, previous) => { emailCodePrompt.Add($"{email}:{previous}"); return Task.FromResult<string?>("22222"); });

        string deviceCode = await callback.GetDeviceCodeAsync(previousCodeWasIncorrect: false);
        string emailCode = await callback.GetEmailCodeAsync("me@example.com", previousCodeWasIncorrect: true);

        Assert.Equal("11111", deviceCode);
        Assert.Equal("22222", emailCode);
        Assert.Equal(["False"], deviceCodePrompt);
        Assert.Equal(["me@example.com:True"], emailCodePrompt);
    }
}
