using Crystalfly.Core.Models;

namespace Crystalfly.Core.Speedrun;

[Flags]
public enum RuntimePatchesFeature
{
    None = 0,
    ScreenShakeModifier = 1,
    MiniSaveStates = 2,
    FasterIntroSkip = 4,
    TextMasher = 8
}

public static class RuntimePatchesPolicy
{
    public const string RulesRevision = "runtime-patches-v1.0.2";

    public static RuntimePatchesFeature GetSupportedFeatures(string buildId) => buildId switch
    {
        "1.2.2.1" => RuntimePatchesFeature.ScreenShakeModifier
            | RuntimePatchesFeature.MiniSaveStates
            | RuntimePatchesFeature.TextMasher,
        "1.4.3.2" => RuntimePatchesFeature.ScreenShakeModifier
            | RuntimePatchesFeature.MiniSaveStates
            | RuntimePatchesFeature.FasterIntroSkip
            | RuntimePatchesFeature.TextMasher,
        "1.5.78.11833" => RuntimePatchesFeature.MiniSaveStates
            | RuntimePatchesFeature.FasterIntroSkip
            | RuntimePatchesFeature.TextMasher,
        // MultiSaveStates replaces Assembly-CSharp on the 1.5.12459 build;
        // RuntimePatches JSON switches are intentionally unavailable there.
        "1.5.12459" => RuntimePatchesFeature.None,
        _ => RuntimePatchesFeature.None
    };

    public static RuntimePatchesFeature GetSupportedFeatures(
        string buildId,
        SpeedrunSaveStatesMode saveStatesMode) =>
        saveStatesMode == SpeedrunSaveStatesMode.Multi
            ? RuntimePatchesFeature.None
            : GetSupportedFeatures(buildId);

    public static bool IsSupportedBuild(string buildId) => GetTemplateId(buildId) is not null;

    public static string? GetTemplateId(string buildId) => buildId switch
    {
        "1.2.2.1" => "runtime-patches-1221",
        "1.4.3.2" => "runtime-patches-1432",
        "1.5.78.11833" => "runtime-patches-1578",
        "1.5.12459" => "runtime-patches-12459-multi",
        _ => null
    };

    public static string? GetAssetId(string buildId) => buildId switch
    {
        "1.2.2.1" => "runtime-patches-1221-v1.0.2",
        "1.4.3.2" => "runtime-patches-1432-v1.0.2",
        "1.5.78.11833" => "runtime-patches-1578-v1.0.2",
        "1.5.12459" => "multisavestates-12459",
        _ => null
    };

    public static string? GetAssetId(
        string buildId,
        SpeedrunSaveStatesMode saveStatesMode) =>
        buildId == "1.5.78.11833" && saveStatesMode == SpeedrunSaveStatesMode.Multi
            ? "multisavestates-1578"
            : GetAssetId(buildId);

    public static string GetFileManifestId(
        string buildId,
        SpeedrunSaveStatesMode saveStatesMode,
        string defaultManifestId) =>
        buildId == "1.5.78.11833" && saveStatesMode == SpeedrunSaveStatesMode.Multi
            ? "files-runtime-patches-1578-multi"
            : defaultManifestId;

    public static IReadOnlyList<string> GetRequiredAssetIds(
        string buildId,
        IReadOnlyList<string> templateAssetIds,
        SpeedrunSaveStatesMode saveStatesMode)
    {
        if (buildId != "1.5.78.11833")
        {
            return templateAssetIds.Distinct(StringComparer.Ordinal).ToArray();
        }

        string? assetId = GetAssetId(buildId, saveStatesMode);
        return assetId is null ? [] : [assetId];
    }

    public static string? GetAssetId(string buildId, string? templateId) =>
        IsMultiSaveStatesTemplate(templateId)
            ? buildId switch
            {
                "1.5.78.11833" => "multisavestates-1578",
                "1.5.12459" => "multisavestates-12459",
                _ => null
            }
            : GetAssetId(buildId);

    public static bool IsMultiSaveStatesTemplate(string? templateId) =>
        templateId is "runtime-patches-1578-multi" or "runtime-patches-12459-multi";

    public static bool IsCurrentTemplate(string? templateId) => templateId is
        "runtime-patches-1221" or "runtime-patches-1432" or "runtime-patches-1578"
        or "runtime-patches-1578-multi" or "runtime-patches-12459-multi";

    public static bool IsLegacyTemplate(string? templateId) => templateId is
        "race-1221" or "single-run-1221" or "single-run-1578" or "race-1578";

    public static RuntimePatchesConfiguration Normalize(
        string buildId,
        RuntimePatchesConfiguration configuration)
        => Normalize(buildId, SpeedrunSaveStatesMode.Mini, configuration);

    public static RuntimePatchesConfiguration Normalize(
        string buildId,
        SpeedrunSaveStatesMode saveStatesMode,
        RuntimePatchesConfiguration configuration)
    {
        RuntimePatchesFeature features = GetSupportedFeatures(buildId, saveStatesMode);
        return configuration with
        {
            ScreenShakeModifier = configuration.ScreenShakeModifier
                && features.HasFlag(RuntimePatchesFeature.ScreenShakeModifier),
            MiniSaveStates = (buildId == "1.5.78.11833"
                && saveStatesMode == SpeedrunSaveStatesMode.Mini)
                || (configuration.MiniSaveStates
                    && features.HasFlag(RuntimePatchesFeature.MiniSaveStates)),
            FasterIntroSkip = configuration.FasterIntroSkip
                && features.HasFlag(RuntimePatchesFeature.FasterIntroSkip),
            TextMasher = configuration.TextMasher
                && features.HasFlag(RuntimePatchesFeature.TextMasher)
        };
    }

    public static RuntimePatchesConfiguration Normalize(
        string buildId,
        string? templateId,
        RuntimePatchesConfiguration configuration) =>
        IsMultiSaveStatesTemplate(templateId)
            ? Normalize(buildId, SpeedrunSaveStatesMode.Multi, configuration)
            : Normalize(buildId, SpeedrunSaveStatesMode.Mini, configuration);
}
