using Crystalfly.Core.Models;

namespace Crystalfly.Core.Loaders;

public static class LoaderBuildCompatibility
{
    // BepInEx is injected independently of the game's Assembly-CSharp version.
    // Individual plugins still need their own build and loader compatibility checks.
    public static bool IsBuildIndependent(string? loaderId) =>
        loaderId?.StartsWith("bepinex-", StringComparison.OrdinalIgnoreCase) == true;

    public static bool CanInstall(LoaderManifest manifest, string buildId) =>
        !string.IsNullOrWhiteSpace(buildId)
        && (IsBuildIndependent(manifest.Id)
            || manifest.SupportedBuildIds.Contains(buildId, StringComparer.OrdinalIgnoreCase));
}
