using Crystalfly.Core.Models;

namespace Crystalfly.Core.Speedrun;

public static class OfficialSpeedrunTemplatePolicy
{
    public static SpeedrunIssueCode? GetViolation(SpeedrunTemplate template)
    {
        SpeedrunSaveStatesMode mode = RuntimePatchesPolicy.IsMultiSaveStatesTemplate(template.Id)
            ? SpeedrunSaveStatesMode.Multi
            : SpeedrunSaveStatesMode.Mini;
        return GetViolation(template, mode);
    }

    public static SpeedrunIssueCode? GetViolation(
        SpeedrunTemplate template,
        SpeedrunSaveStatesMode saveStatesMode)
    {
        string? templateId = RuntimePatchesPolicy.IsMultiSaveStatesTemplate(template.Id)
            ? template.Id
            : RuntimePatchesPolicy.GetTemplateId(template.BuildId);
        string? assetId = RuntimePatchesPolicy.GetAssetId(template.BuildId, saveStatesMode);
        bool valid = templateId is not null
            && assetId is not null
            && string.Equals(template.Id, templateId, StringComparison.Ordinal)
            && Matches(template, assetId, saveStatesMode);
        return valid ? null : SpeedrunIssueCode.UnsupportedOfficialTemplate;
    }

    private static bool Matches(
        SpeedrunTemplate template,
        string assetId,
        SpeedrunSaveStatesMode saveStatesMode) =>
        template.IsOfficial &&
        template.LoaderId is null &&
        string.Equals(template.RulesRevision, RuntimePatchesPolicy.RulesRevision, StringComparison.Ordinal) &&
        string.Equals(template.FileManifestId, $"files-{template.Id}", StringComparison.Ordinal) &&
        (template.BuildId == "1.5.78.11833"
            ? template.RequiredAssetIds.Contains(assetId, StringComparer.Ordinal)
            : template.RequiredAssetIds.SequenceEqual([assetId], StringComparer.Ordinal)) &&
        !template.LoadNormaliserAvailable &&
        !template.RequiresLoadNormaliserSelection &&
        template.AllowedLoadNormaliserSeconds.Count == 0;
}
