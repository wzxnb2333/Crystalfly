using Crystalfly.Core.Models;

namespace Crystalfly.Core.Speedrun;

public static class OfficialSpeedrunTemplatePolicy
{
    public static SpeedrunIssueCode? GetViolation(SpeedrunTemplate template)
    {
        string? templateId = RuntimePatchesPolicy.IsMultiSaveStatesTemplate(template.Id)
            ? template.Id
            : RuntimePatchesPolicy.GetTemplateId(template.BuildId);
        string? assetId = RuntimePatchesPolicy.GetAssetId(template.BuildId, template.Id);
        bool valid = templateId is not null
            && assetId is not null
            && string.Equals(template.Id, templateId, StringComparison.Ordinal)
            && Matches(template, assetId);
        return valid ? null : SpeedrunIssueCode.UnsupportedOfficialTemplate;
    }

    private static bool Matches(SpeedrunTemplate template, string assetId) =>
        template.IsOfficial &&
        template.LoaderId is null &&
        string.Equals(template.RulesRevision, RuntimePatchesPolicy.RulesRevision, StringComparison.Ordinal) &&
        string.Equals(template.FileManifestId, $"files-{template.Id}", StringComparison.Ordinal) &&
        template.RequiredAssetIds.SequenceEqual([assetId], StringComparer.Ordinal) &&
        !template.LoadNormaliserAvailable &&
        !template.RequiresLoadNormaliserSelection &&
        template.AllowedLoadNormaliserSeconds.Count == 0;
}
