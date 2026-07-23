namespace Crystalfly.Core.Runtime;

public enum ProtocolCommandKind
{
    DownloadMod,
    ReinstallAllMods,
    ResetApplicationSettings,
    UseOfficialModLinks,
    UseCustomModLinks,
    DeleteModSettings,
    DeleteAllModSettings,
    LaunchInstance,
    OpenModLocation,
    ImportPresetShare
}

public sealed record ProtocolCommand
{
    public required ProtocolCommandKind Kind { get; init; }

    public string? InstanceId { get; init; }

    public string? ModId { get; init; }

    public string? ShareCode { get; init; }

    public string? SourceUrl { get; init; }

    public string? BuildId { get; init; }

    public string? LoaderId { get; init; }

    public bool RequiresConfirmation => Kind != ProtocolCommandKind.OpenModLocation;
}

public sealed class ProtocolCommandException(string message) : FormatException(message);
