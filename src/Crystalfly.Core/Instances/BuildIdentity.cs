using System.Globalization;

namespace Crystalfly.Core.Instances;

public static class BuildIdentity
{
    private const string PublicPrefix = "steam-public-";
    private const string ManifestPrefix = "steam-manifest-";

    public static bool IsKnown(string? buildId) =>
        !string.IsNullOrWhiteSpace(buildId)
        && !string.Equals(buildId, "unknown", StringComparison.OrdinalIgnoreCase)
        && !buildId.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase)
        && !buildId.StartsWith(ManifestPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool TryGetSteamManifestId(string? buildId, out ulong manifestId)
    {
        manifestId = 0;
        if (string.IsNullOrWhiteSpace(buildId))
        {
            return false;
        }
        string? value = buildId.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase)
            ? buildId[PublicPrefix.Length..]
            : buildId.StartsWith(ManifestPrefix, StringComparison.OrdinalIgnoreCase)
                ? buildId[ManifestPrefix.Length..]
                : null;
        return value is not null
            && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out manifestId)
            && manifestId != 0;
    }
}