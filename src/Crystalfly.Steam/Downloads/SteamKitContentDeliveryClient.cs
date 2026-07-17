using System.Globalization;
using System.Net;
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
    private uint _appId;
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

        ulong requestCode = await _content.GetManifestRequestCode(
            depotId,
            appId,
            resolvedManifestId,
            branch);
        (DepotManifest manifest, string? authToken) = await DownloadWithCdnAuthAsync(
            null,
            token => _cdn.DownloadManifestAsync(
                depotId,
                resolvedManifestId,
                requestCode,
                server,
                depotKey.DepotKey,
                proxy,
                token),
            async () =>
            {
                SteamContent.CDNAuthToken token = await _content.GetCDNAuthToken(appId, depotId, server.Host);
                return (token.Result, token.Token);
            });

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
        _cdnAuthToken = authToken;
        _appId = appId;
        _depotId = depotId;
        return new SteamDepotManifest(manifest.ManifestGID, files);
    }

    public async Task<ReadOnlyMemory<byte>> DownloadChunkAsync(
        SteamDepotChunk chunk,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_server is null || string.IsNullOrWhiteSpace(_server.Host) || _depotKey is null
            || !_chunks.TryGetValue(chunk.Id, out DepotManifest.ChunkData? source))
            throw new InvalidOperationException("The requested chunk does not belong to the active depot manifest.");

        Server server = _server;
        string host = server.Host;
        byte[] destination = new byte[checked((int)source.UncompressedLength)];
        (int written, string? authToken) = await DownloadWithCdnAuthAsync(
            _cdnAuthToken,
            token => _cdn.DownloadDepotChunkAsync(
                _depotId,
                source,
                server,
                destination,
                _depotKey,
                _proxy,
                token),
            async () =>
            {
                SteamContent.CDNAuthToken token = await _content.GetCDNAuthToken(_appId, _depotId, host);
                return (token.Result, token.Token);
            });
        _cdnAuthToken = authToken;
        if (written != destination.Length)
            throw new InvalidDataException($"Steam returned {written} bytes for a {destination.Length}-byte chunk.");
        return destination;
    }

    public void Dispose() => _cdn.Dispose();

    internal static async Task<(T Value, string? AuthToken)> DownloadWithCdnAuthAsync<T>(
        string? initialAuthToken,
        Func<string?, Task<T>> download,
        Func<Task<(EResult Result, string? Token)>> requestToken)
    {
        try
        {
            return (await download(initialAuthToken), initialAuthToken);
        }
        catch (SteamKitWebRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            (EResult result, string? token) = await requestToken();
            if (result != EResult.OK)
                throw new InvalidOperationException($"Steam denied the CDN token request: {result}.", exception);
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Steam returned an empty CDN auth token.", exception);

            return (await download(token), token);
        }
    }

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
