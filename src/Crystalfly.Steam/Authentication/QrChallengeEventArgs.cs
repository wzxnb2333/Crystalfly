namespace Crystalfly.Steam.Authentication;

public sealed class QrChallengeEventArgs(string challengeUrl) : EventArgs
{
    public string ChallengeUrl { get; } = challengeUrl;
}
