using System.Security.Cryptography;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Instances;

public static class BuildFingerprintService
{
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
