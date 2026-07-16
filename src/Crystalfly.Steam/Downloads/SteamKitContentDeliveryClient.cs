using System.Globalization;
using SteamKit2;
using SteamKit2.CDN;

namespace Crystalfly.Steam.Downloads;

public sealed class SteamKitContentDeliveryClient : ISteamContentDeliveryClient, IDisposable
{
    private readonly SteamClient _steamClient;
    private readonly SteamApps _apps;
    private readonly SteamContent _content;
    private readonly Client _cdn;
    private readonly Dictionary<string, DepotManifest.ChunkData> _chunks = new(StringComparer.Ordinal);
    private Server? _server;
    private Server? _proxy;
    private byte[]? _depotKey;
    private string? _cdnAuthToken;
    private uint _depotId;

    public SteamKitContentDeliveryClient(SteamClient steamClient)
    {
        _steamClient = steamClient;
        _apps = steamClient.GetHandler<SteamApps>()
            ?? throw new InvalidOperationException("SteamApps handler is unavailable.");
        _content = steamClient.GetHandler<SteamContent>()
            ?? throw new InvalidOperationException("SteamContent handler is unavailable.");
        _cdn = new Client(steamClient);
    }

    public async Task<SteamDepotManifest> GetManifestAsync(
        uint appId,
        uint depotId,
        string branch,
        ulong? manifestId,
        CancellationToken cancellationToken)
    {
        if (!_steamClient.IsConnected)
            throw new InvalidOperationException("Steam must be connected and logged on before downloading content.");
        if (!string.Equals(branch, "public", StringComparison.Ordinal))
            throw new NotSupportedException("Only the public Steam branch is supported.");

        cancellationToken.ThrowIfCancellationRequested();
        ulong resolvedManifestId = manifestId ?? await ResolvePublicManifestIdAsync(appId, depotId);
        SteamApps.DepotKeyCallback depotKey = await _apps.GetDepotDecryptionKey(depotId, appId);
        if (depotKey.Result != EResult.OK)
            throw new InvalidOperationException($"Steam denied the depot key request: {depotKey.Result}.");

        IReadOnlyCollection<Server> servers = await _content.GetServersForSteamPipe(maxNumServers: 20);
        Server server = servers
            .Where(candidate => !candidate.UseAsProxy &&
                candidate.Protocol == Server.ConnectionProtocol.HTTPS &&
                (candidate.AllowedAppIds.Length == 0 || candidate.AllowedAppIds.Contains(appId)))
            .OrderBy(static candidate => candidate.WeightedLoad)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Steam returned no suitable HTTPS content server.");
        Server? proxy = servers.FirstOrDefault(static candidate => candidate.UseAsProxy);
        if (string.IsNullOrWhiteSpace(server.Host))
            throw new InvalidOperationException("Steam returned a content server without a host name.");

        SteamContent.CDNAuthToken authToken = await _content.GetCDNAuthToken(appId, depotId, server.Host);
        if (authToken.Result != EResult.OK)
            throw new InvalidOperationException($"Steam denied the CDN token request: {authToken.Result}.");
        ulong requestCode = await _content.GetManifestRequestCode(
            depotId,
            appId,
            resolvedManifestId,
            branch);
        DepotManifest manifest = await _cdn.DownloadManifestAsync(
            depotId,
            resolvedManifestId,
            requestCode,
            server,
            depotKey.DepotKey,
            proxy,
            authToken.Token);

        _chunks.Clear();
        List<DepotManifest.FileData> sourceFiles = manifest.Files
            ?? throw new InvalidDataException("Steam returned a manifest without a file list.");
        var files = new List<SteamDepotFile>(sourceFiles.Count);
        foreach (DepotManifest.FileData file in sourceFiles)
        {
            if ((file.Flags & EDepotFileFlag.Directory) != 0)
                continue;
            if ((file.Flags & EDepotFileFlag.Symlink) != 0)
                throw new InvalidDataException($"Symbolic links are not supported in the Windows depot: {file.FileName}");

            List<DepotManifest.ChunkData> sourceChunks = file.Chunks
                ?? throw new InvalidDataException($"Steam returned a file without chunks: {file.FileName}");
            var chunks = new List<SteamDepotChunk>(sourceChunks.Count);
            foreach (DepotManifest.ChunkData chunk in sourceChunks)
            {
                string id = Convert.ToHexString(chunk.ChunkID
                    ?? throw new InvalidDataException($"Steam returned an unnamed chunk for {file.FileName}."));
                _chunks[id] = chunk;
                chunks.Add(new SteamDepotChunk(
                    id,
                    checked((long)chunk.Offset),
                    checked((int)chunk.UncompressedLength)));
            }

            files.Add(new SteamDepotFile(
                file.FileName,
                checked((long)file.TotalSize),
                Convert.ToHexString(file.FileHash
                    ?? throw new InvalidDataException($"Steam returned no file hash for {file.FileName}.")),
                chunks));
        }

        _server = server;
        _proxy = proxy;
        _depotKey = depotKey.DepotKey;
        _cdnAuthToken = authToken.Token;
        _depotId = depotId;
        return new SteamDepotManifest(manifest.ManifestGID, files);
    }

    public async Task<ReadOnlyMemory<byte>> DownloadChunkAsync(
        SteamDepotChunk chunk,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_server is null || _depotKey is null || !_chunks.TryGetValue(chunk.Id, out DepotManifest.ChunkData? source))
            throw new InvalidOperationException("The requested chunk does not belong to the active depot manifest.");

        byte[] destination = new byte[checked((int)source.UncompressedLength)];
        int written = await _cdn.DownloadDepotChunkAsync(
            _depotId,
            source,
            _server,
            destination,
            _depotKey,
            _proxy,
            _cdnAuthToken);
        if (written != destination.Length)
            throw new InvalidDataException($"Steam returned {written} bytes for a {destination.Length}-byte chunk.");
        return destination;
    }

    public void Dispose() => _cdn.Dispose();

    private async Task<ulong> ResolvePublicManifestIdAsync(uint appId, uint depotId)
    {
        SteamApps.PICSTokensCallback tokens = await _apps.PICSGetAccessTokens(appId, package: null);
        if (tokens.AppTokensDenied.Contains(appId))
            throw new InvalidOperationException("Steam denied access to Hollow Knight app information.");
        tokens.AppTokens.TryGetValue(appId, out ulong accessToken);

        var request = new SteamApps.PICSRequest(appId, accessToken);
        AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet responses =
            await _apps.PICSGetProductInfo(request, package: null, metaDataOnly: false);
        SteamApps.PICSProductInfoCallback.PICSProductInfo? appInfo = (responses.Results ?? [])
            .SelectMany(static response => response.Apps)
            .Where(pair => pair.Key == appId)
            .Select(static pair => pair.Value)
            .FirstOrDefault();
        if (appInfo is null)
            throw new InvalidOperationException("Steam returned no Hollow Knight app information.");

        ulong manifestId = appInfo.KeyValues["depots"]
            [depotId.ToString(CultureInfo.InvariantCulture)]
            ["manifests"]
            ["public"]
            ["gid"]
            .AsUnsignedLong();
        return manifestId != 0
            ? manifestId
            : throw new InvalidDataException("Steam app information contains no public Windows manifest.");
    }
}
