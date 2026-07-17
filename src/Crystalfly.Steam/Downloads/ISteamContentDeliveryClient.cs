namespace Crystalfly.Steam.Downloads;

public interface ISteamContentDeliveryClient
{
    // Complete manifest initialization before concurrent chunk downloads. Do not switch
    // manifests or dispose the client until every chunk request has completed.
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
