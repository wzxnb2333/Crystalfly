using System.Security.Cryptography;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Loaders;

public static class LoaderStateDetector
{
    public static async Task<LoaderState> DetectAsync(
        string instanceRoot,
        InstalledPackageReceipt? receipt,
        CancellationToken cancellationToken = default)
    {
        instanceRoot = Path.GetFullPath(instanceRoot);
        var managed = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed");
        var hasBepInEx = Directory.Exists(Path.Combine(instanceRoot, "BepInEx"))
            || File.Exists(Path.Combine(instanceRoot, "doorstop_config.ini"))
            || File.Exists(Path.Combine(instanceRoot, "winhttp.dll"));
        var hasModdingApi = Directory.Exists(Path.Combine(managed, "Mods"))
            || File.Exists(Path.Combine(managed, "MMHOOK_Assembly-CSharp.dll"))
            || (Directory.Exists(managed) && Directory.EnumerateFiles(
                managed,
                "MMHOOK_TeamCherry*.dll",
                SearchOption.TopDirectoryOnly).Any());

        if (hasBepInEx && hasModdingApi)
        {
            return LoaderState.Conflict;
        }
        if (receipt is null)
        {
            return hasBepInEx || hasModdingApi ? LoaderState.Drifted : LoaderState.Vanilla;
        }
        if (receipt.LoaderState is not (LoaderState.BepInEx or LoaderState.ModdingApi)
            || receipt.LoaderState == LoaderState.BepInEx != hasBepInEx
            || receipt.LoaderState == LoaderState.ModdingApi != hasModdingApi)
        {
            return LoaderState.Drifted;
        }

        foreach (var file in receipt.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = ResolveUnderRoot(instanceRoot, file.RelativePath);
            if (!File.Exists(path)
                || !string.Equals(
                    file.Sha256,
                    await HashFileAsync(path, cancellationToken),
                    StringComparison.OrdinalIgnoreCase))
            {
                return LoaderState.Drifted;
            }
        }
        return receipt.LoaderState;
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        var fullRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(
            fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Receipt path escapes the instance root: '{relativePath}'.");
        }
        return path;
    }

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
}
