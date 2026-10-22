namespace Crystalfly.App.ViewModels;

internal static class SilksongManifestHistory
{
    // Windows Depot 1030301 version mapping: https://github.com/hk-speedrunning/Silksong-Resources/blob/main/manifests.md
    // First-seen UTC dates come from the user's SteamDB snapshot. These entries are display metadata, not file fingerprints.
    internal static readonly (ulong ManifestId, string DisplayVersion, string FirstSeenDate, string? UnityVariantKey)[] Entries =
    [
        (4421626056705534276UL, "1.0.30000", "2026-03-24", null),
        (468692862190470536UL, "1.0.29980", "2026-03-20", null),
        (3703686001292550981UL, "1.0.29926", "2026-03-17", null),
        (3853982342899707391UL, "1.0.29909", "2026-03-13", null),
        (3545882420322545098UL, "1.0.29315", "2025-11-12", null),
        (426651197780377263UL, "1.0.29242", "2025-11-06", null),
        (3690203822520536668UL, "1.0.28891", "2025-10-07", null),
        (5977483240701257214UL, "1.0.28714", "2025-10-03", "DownloadBuildNewUnity"),
        (6773297648406051922UL, "1.0.28714", "2025-09-24", "DownloadBuildOldUnity"),
        (3900764848237536293UL, "1.0.28650", "2025-09-17", null),
        (8642535143474926050UL, "1.0.28561", "2025-09-12", null),
        (539129767115354441UL, "1.0.28497", "2025-09-11", null),
        (3229726349000518284UL, "1.0.28324", "2025-09-04", null)
    ];
}
