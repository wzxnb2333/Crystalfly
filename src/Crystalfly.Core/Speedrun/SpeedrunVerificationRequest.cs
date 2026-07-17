using Crystalfly.Core.Models;

namespace Crystalfly.Core.Speedrun;

public sealed record SpeedrunVerificationRequest
{
    public required InstanceRecord Instance { get; init; }

    public required SpeedrunTemplate Template { get; init; }

    public SpeedrunTemplateSource TemplateSource { get; init; }

    public required GameBuild ExpectedBuild { get; init; }

    public required string CurrentRulesRevision { get; init; }

    public required SpeedrunFileManifest FileManifest { get; init; }

    public int? LoadNormaliserSeconds { get; init; }

    public required string ReportsDirectory { get; init; }
}

public sealed record SpeedrunVerificationResult(
    SpeedrunVerificationReport Report,
    string ReportPath);
