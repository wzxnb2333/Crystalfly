namespace Crystalfly.Steam.Authentication;

public sealed class SteamConnectionLostEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
