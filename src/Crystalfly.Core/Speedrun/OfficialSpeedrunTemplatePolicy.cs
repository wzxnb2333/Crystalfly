using Crystalfly.Core.Models;

namespace Crystalfly.Core.Speedrun;

public static class OfficialSpeedrunTemplatePolicy
{
    public static SpeedrunIssueCode? GetViolation(SpeedrunTemplate template)
    {
        bool valid = template.Id switch
        {
            "single-run-1221" or "race-1221" =>
                Matches(template, "1.2.2.1", ["screen-shake-modifier-1221"], false, []),
            "single-run-1578" =>
                Matches(template, "1.5.78.11833", [], false, []),
            "race-1578" =>
                Matches(template, "1.5.78.11833", ["load-normaliser-1.1"], true, [1, 2, 3, 5]),
            _ => false
        };
        return valid ? null : SpeedrunIssueCode.UnsupportedOfficialTemplate;
    }

    private static bool Matches(
        SpeedrunTemplate template,
        string buildId,
        IReadOnlyList<string> requiredAssets,
        bool requiresSelection,
        IReadOnlyList<int> allowedSeconds) =>
        template.IsOfficial &&
        template.LoaderId is null &&
        !string.IsNullOrWhiteSpace(template.RulesRevision) &&
        !string.IsNullOrWhiteSpace(template.FileManifestId) &&
        string.Equals(template.BuildId, buildId, StringComparison.Ordinal) &&
        template.LoadNormaliserAvailable == string.Equals(buildId, "1.5.78.11833", StringComparison.Ordinal) &&
        template.RequiredAssetIds.SequenceEqual(requiredAssets, StringComparer.Ordinal) &&
        template.RequiresLoadNormaliserSelection == requiresSelection &&
        template.AllowedLoadNormaliserSeconds.SequenceEqual(allowedSeconds);
}
