using System.Security.Cryptography;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Instances;

public static class BuildFingerprintService
{
    public static string? ReadDeclaredVersion(string instanceRoot, GameId gameId)
    {
        if (gameId != GameId.Silksong) return null;
        var path = Path.Combine(instanceRoot, GameDefinition.Get(gameId).DataDirectoryName, "Managed", "Assembly-CSharp.dll");
        try
        {
            InstanceDirectory.RejectReparseAncestors(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 32 * 1024 * 1024) return null;
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
            if (!pe.HasMetadata) return null;
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                if (reader.GetString(type.Name) != "Constants" || reader.GetString(type.Namespace).Length != 0) continue;
                foreach (var fieldHandle in type.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    if (reader.GetString(field.Name) != "GAME_VERSION" || field.GetDefaultValue().IsNil) continue;
                    var constant = reader.GetConstant(field.GetDefaultValue());
                    if (constant.TypeCode != ConstantTypeCode.String) return null;
                    var bytes = reader.GetBlobBytes(constant.Value);
                    if (bytes.Length > 64) return null;
                    var value = Encoding.Unicode.GetString(bytes);
                    return value.All(character => char.IsAsciiDigit(character) || character == '.')
                        && Version.TryParse(value, out var version)
                        && version is { Major: 1, Minor: 0, Build: >= 0, Revision: -1 } ? value : null;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return null;
        }
        return null;
    }

    public static async Task<string> CalculateLaunchFingerprintAsync(
        string instanceRoot, BuildFingerprint game, CancellationToken cancellationToken = default,
        IReadOnlyList<InstalledFileReceipt>? managedLoaderFiles = null)
    {
        var paths = new List<string>
        {
            $"{GameDefinition.Get(game.GameId).DataDirectoryName}/Managed/Assembly-CSharp.dll",
            "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "BepInEx/config/BepInEx.cfg"
        };
        var core = Path.Combine(instanceRoot, "BepInEx", "core");
        InstanceDirectory.RejectReparseAncestors(core);
        if (Directory.Exists(core))
            paths.AddRange(Directory.EnumerateFiles(core, "*", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetRelativePath(instanceRoot, path).Replace('\\', '/')));
        var values = new List<string> { game.GameId.ToString(), game.ExecutableSha256, game.UnityPlayerSha256 ?? "", game.GlobalGameManagersSha256 };
        // Inspection has already verified these hashes, including any nested managed loader files.
        foreach (var file in (managedLoaderFiles ?? []).OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
            values.Add(file.RelativePath.Replace('\\', '/').ToUpperInvariant() + ":" + file.Sha256.ToUpperInvariant());
        foreach (var relative in paths.Order(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(instanceRoot, relative);
            InstanceDirectory.RejectReparseAncestors(path);
            values.Add(relative.ToUpperInvariant() + ":" + (await HashOptionalFileAsync(path, cancellationToken) ?? "missing"));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
    }

    public static GameBuild? FindBuild(
        IEnumerable<GameBuild> builds,
        BuildFingerprint fingerprint) =>
        builds.FirstOrDefault(build =>
            build.GameId == fingerprint.GameId &&
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
        await CalculateAsync(instanceRoot, GameDefinition.Detect(instanceRoot)?.Id ?? GameId.HollowKnight, cancellationToken);

    public static async Task<BuildFingerprint> CalculateAsync(
        string instanceRoot, GameId gameId, CancellationToken cancellationToken = default) =>
        new()

        {
            GameId = gameId,
            ExecutableSha256 = await HashFileAsync(
                Path.Combine(instanceRoot, GameDefinition.Get(gameId).ExecutableName),
                cancellationToken),
            UnityPlayerSha256 = await HashOptionalFileAsync(
                Path.Combine(instanceRoot, "UnityPlayer.dll"),
                cancellationToken),
            GlobalGameManagersSha256 = await HashFileAsync(
                Path.Combine(instanceRoot, GameDefinition.Get(gameId).DataDirectoryName, "globalgamemanagers"),
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
