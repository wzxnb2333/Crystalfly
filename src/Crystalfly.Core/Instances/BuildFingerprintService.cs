using System.Security.Cryptography;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Instances;

public static class BuildFingerprintService
{
    public static GameBuild? FindBuild(
        IEnumerable<GameBuild> builds,
        BuildFingerprint fingerprint) =>
        builds.FirstOrDefault(build =>
            string.Equals(build.ExecutableSha256, fingerprint.ExecutableSha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                build.GlobalGameManagersSha256,
                fingerprint.GlobalGameManagersSha256,
                StringComparison.OrdinalIgnoreCase)
            && (build.UnityPlayerSha256 is null || string.Equals(
                build.UnityPlayerSha256,
                fingerprint.UnityPlayerSha256,
                StringComparison.OrdinalIgnoreCase)));

    public static async Task<BuildFingerprint> CalculateAsync(
        string instanceRoot,
        CancellationToken cancellationToken = default) =>
        new()
        {
            ExecutableSha256 = await HashFileAsync(
                Path.Combine(instanceRoot, "hollow_knight.exe"),
                cancellationToken),
            UnityPlayerSha256 = await HashOptionalFileAsync(
                Path.Combine(instanceRoot, "UnityPlayer.dll"),
                cancellationToken),
            GlobalGameManagersSha256 = await HashFileAsync(
                Path.Combine(instanceRoot, "hollow_knight_Data", "globalgamemanagers"),
                cancellationToken)
        };

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static async Task<string?> HashOptionalFileAsync(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await HashFileAsync(path, cancellationToken) : null;
}
