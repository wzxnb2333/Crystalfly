namespace Crystalfly.Steam.Authentication;

public interface ISteamGuardCallback
{
    Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect);
    Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect);
    Task<bool> AcceptDeviceConfirmationAsync();
}
