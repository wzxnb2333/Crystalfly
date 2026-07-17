namespace Crystalfly.Steam.Downloads;

public sealed record SteamDepotChunk(string Id, long Offset, int UncompressedLength);

public sealed record SteamDepotFile(
    string RelativePath,
    long Size,
    string Sha1,
    IReadOnlyList<SteamDepotChunk> Chunks);

public sealed record SteamDepotManifest(ulong Id, IReadOnlyList<SteamDepotFile> Files);

public sealed record SteamDownloadRequest(
    string StagingDirectory,
    ulong? ManifestId = null,
    string Branch = "public");

public sealed record SteamDownloadResult(
    uint AppId,
    uint DepotId,
    ulong ManifestId,
    string StagingDirectory,
    IReadOnlyList<string> Files,
    long TotalBytes);
