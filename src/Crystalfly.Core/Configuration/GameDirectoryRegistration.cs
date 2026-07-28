namespace Crystalfly.Core.Configuration;

public enum GameDirectorySourceKind
{
    Managed,
    Steam,
    Custom
}

public sealed record GameDirectoryRegistration
{
    public required string Path { get; init; }

    public required string DisplayName { get; init; }

    public GameDirectorySourceKind Source { get; init; }

    public bool SteamRiskAccepted { get; init; }
}
