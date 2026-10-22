using Crystalfly.Core.Models;

namespace Crystalfly.Core.Configuration;

public sealed record GameWorkspaceSettings
{
    public GameId GameId { get; init; }
    public string? VersionRoot { get; init; }
    public string? CurrentInstanceId { get; init; }
    public string InstanceSearch { get; init; } = string.Empty;
    public string MarketSearch { get; init; } = string.Empty;
    public string? MarketBuild { get; init; }
    public string? MarketLoader { get; init; }
    public string? MarketSource { get; init; }
    public string? MarketTag { get; init; }
    public string? MarketActivity { get; init; }
    public string? ThunderstoreCategory { get; init; }
    public int ThunderstoreSort { get; init; }
    public bool ThunderstoreShowDeprecated { get; init; }
    public string DownloadSearch { get; init; } = string.Empty;
}
