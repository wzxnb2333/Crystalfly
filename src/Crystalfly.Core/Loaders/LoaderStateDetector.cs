using System.Security.Cryptography;
using System.Reflection;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Loaders;

public static class LoaderStateDetector
{
    public static async Task<LoaderState> DetectAsync(
        string instanceRoot,
        InstalledPackageReceipt? receipt,
        CancellationToken cancellationToken = default) =>
        (await InspectAsync(instanceRoot, receipt, cancellationToken)).State;

    public static async Task<LoaderInspection> InspectAsync(
        string instanceRoot,
        InstalledPackageReceipt? receipt,
        CancellationToken cancellationToken = default)
    {
        instanceRoot = Path.GetFullPath(instanceRoot);
        var managed = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed");
        var bepInExRoot = Path.Combine(instanceRoot, "BepInEx");
        var hasBepInEx = File.Exists(Path.Combine(bepInExRoot, "core", "BepInEx.dll"))
            || File.Exists(Path.Combine(instanceRoot, "doorstop_config.ini"))
            || File.Exists(Path.Combine(instanceRoot, "winhttp.dll"));
        var hasBepInExArtifacts = hasBepInEx
            || HasFiles(Path.Combine(bepInExRoot, "core"))
            || HasFiles(Path.Combine(bepInExRoot, "plugins"))
            || HasFiles(Path.Combine(bepInExRoot, "patchers"));
        var hasModdingApi = File.Exists(Path.Combine(managed, "MMHOOK_Assembly-CSharp.dll"))
            || (Directory.Exists(managed) && Directory.EnumerateFiles(
                managed,
                "MMHOOK_TeamCherry*.dll",
                SearchOption.TopDirectoryOnly).Any());
        var hasModdingApiArtifacts = hasModdingApi || HasFiles(Path.Combine(managed, "Mods"));

        if (receipt is not null)
        {
            if (receipt.LoaderState is not (LoaderState.BepInEx or LoaderState.ModdingApi))
            {
                return Inspection(LoaderState.Drifted, receipt);
            }
            if (receipt.LoaderState == LoaderState.BepInEx && hasModdingApiArtifacts
                || receipt.LoaderState == LoaderState.ModdingApi && hasBepInExArtifacts)
            {
                return Inspection(LoaderState.Conflict, receipt);
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
                    return Inspection(LoaderState.Drifted, receipt);
                }
            }
            return Inspection(receipt.LoaderState, receipt);
        }

        if (hasBepInExArtifacts && hasModdingApiArtifacts)
        {
            return External(LoaderState.Conflict);
        }
        if (!hasBepInExArtifacts && !hasModdingApiArtifacts)
        {
            return new LoaderInspection
            {
                State = LoaderState.Vanilla,
                Ownership = LoaderOwnership.None
            };
        }
        if (hasModdingApi || !hasBepInEx)
        {
            return External(LoaderState.Drifted);
        }

        var version = ReadAssemblyVersion(Path.Combine(instanceRoot, "BepInEx", "core", "BepInEx.dll"));
        return version is null
            ? External(LoaderState.Drifted)
            : new LoaderInspection
            {
                State = LoaderState.BepInEx,
                PackageId = $"bepinex-{version}",
                Version = version,
                Ownership = LoaderOwnership.External
            };
    }

    private static LoaderInspection Inspection(LoaderState state, InstalledPackageReceipt receipt) => new()
    {
        State = state,
        PackageId = receipt.PackageId,
        Version = PackageVersion(receipt.PackageId),
        IsVerified = receipt.IsVerified,
        Ownership = LoaderOwnership.Managed
    };

    private static LoaderInspection External(LoaderState state) => new()
    {
        State = state,
        Ownership = LoaderOwnership.External
    };

    private static string? PackageVersion(string packageId)
    {
        const string moddingApiPrefix = "modding-api-";
        const string bepinExPrefix = "bepinex-";
        return packageId.StartsWith(moddingApiPrefix, StringComparison.OrdinalIgnoreCase)
            ? packageId[moddingApiPrefix.Length..]
            : packageId.StartsWith(bepinExPrefix, StringComparison.OrdinalIgnoreCase)
                ? packageId[bepinExPrefix.Length..]
                : null;
    }

    private static bool HasFiles(string path) =>
        Directory.Exists(path) && Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any();

    private static string? ReadAssemblyVersion(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return AssemblyName.GetAssemblyName(path).Version?.ToString();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return null;
        }
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
