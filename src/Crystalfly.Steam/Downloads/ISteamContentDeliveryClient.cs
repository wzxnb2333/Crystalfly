namespace Crystalfly.Steam.Downloads;

public interface ISteamContentDeliveryClient
{
    Task<SteamDepotManifest> GetManifestAsync(
        uint appId,
        uint depotId,
        string branch,
        ulong? manifestId,
        CancellationToken cancellationToken);

    Task<ReadOnlyMemory<byte>> DownloadChunkAsync(
        SteamDepotChunk chunk,
        CancellationToken cancellationToken);
}
