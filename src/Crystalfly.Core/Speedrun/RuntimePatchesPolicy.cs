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
        _ => RuntimePatchesFeature.None
    };

    public static bool IsSupportedBuild(string buildId) =>
        GetSupportedFeatures(buildId) != RuntimePatchesFeature.None;

    public static string? GetTemplateId(string buildId) => buildId switch
    {
        "1.2.2.1" => "runtime-patches-1221",
        "1.4.3.2" => "runtime-patches-1432",
        "1.5.78.11833" => "runtime-patches-1578",
        _ => null
    };

    public static string? GetAssetId(string buildId) => buildId switch
    {
        "1.2.2.1" => "runtime-patches-1221-v1.0.2",
        "1.4.3.2" => "runtime-patches-1432-v1.0.2",
        "1.5.78.11833" => "runtime-patches-1578-v1.0.2",
        _ => null
    };

    public static bool IsCurrentTemplate(string? templateId) => templateId is
        "runtime-patches-1221" or "runtime-patches-1432" or "runtime-patches-1578";

    public static bool IsLegacyTemplate(string? templateId) => templateId is
        "race-1221" or "single-run-1221" or "single-run-1578" or "race-1578";

    public static RuntimePatchesConfiguration Normalize(
        string buildId,
        RuntimePatchesConfiguration configuration)
    {
        RuntimePatchesFeature features = GetSupportedFeatures(buildId);
        return configuration with
        {
            ScreenShakeModifier = configuration.ScreenShakeModifier
                && features.HasFlag(RuntimePatchesFeature.ScreenShakeModifier),
            MiniSaveStates = configuration.MiniSaveStates
                && features.HasFlag(RuntimePatchesFeature.MiniSaveStates),
            FasterIntroSkip = configuration.FasterIntroSkip
                && features.HasFlag(RuntimePatchesFeature.FasterIntroSkip),
            TextMasher = configuration.TextMasher
                && features.HasFlag(RuntimePatchesFeature.TextMasher)
        };
    }
}
