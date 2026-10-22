using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Thunderstore;

public sealed class ThunderstoreCatalogSource(HttpClient client, string cachePath, INetworkPolicy? networkPolicy = null)
{
    public const string Community = "hollow-knight-silksong";
    public const string IndexUrl = "https://thunderstore.io/c/" + Community + "/api/v1/package-listing-index/";
    public const string LegacyUrl = "https://thunderstore.io/c/" + Community + "/api/v1/package/";
    private const int MaximumBytes = 64 * 1024 * 1024;
    public bool UsingCache { get; private set; }
    public string? RefreshError { get; private set; }
    public DateTimeOffset? RetryAfter { get; private set; }

    public async Task<ThunderstoreCatalog> LoadAsync(bool offline = false, CancellationToken cancellationToken = default)
    {
        UsingCache = false;
        RefreshError = null;
        if (offline || networkPolicy?.IsOffline == true || RetryAfter > DateTimeOffset.UtcNow)
            return await ReadCacheAsync(cancellationToken);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            networkPolicy?.GetOnlineCancellationToken() ?? default);
        try
        {
            IReadOnlyList<ThunderstorePackage> packages;
            try
            {
                var index = JsonSerializer.Deserialize<string[]>(await ReadJsonAsync(IndexUrl, linked.Token))
                    ?? throw new InvalidDataException("Empty package index.");
                if (index.Length is < 1 or > 512) throw new InvalidDataException("Invalid package index length.");
                var all = new List<ThunderstorePackage>();
                foreach (var url in index)
                {
                    all.AddRange(Parse(await ReadJsonAsync(url, linked.Token)));
                    if (all.Count > 25000) throw new InvalidDataException("Package catalog exceeds limit.");
                }
                packages = all;
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException
                || exception is HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.NotImplemented })
            {
                packages = Parse(await ReadJsonAsync(LegacyUrl, linked.Token));
            }
            var catalog = new ThunderstoreCatalog { UpdatedAt = DateTimeOffset.UtcNow, Packages = packages };
            Validate(catalog);
            await AtomicJsonStore.WriteAsync(cachePath, catalog, linked.Token);
            return catalog;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or IOException or InvalidDataException or JsonException or OperationCanceledException)
        {
            RefreshError = exception.Message;
            return await ReadCacheAsync(cancellationToken);
        }
    }

    private async Task<ThunderstoreCatalog> ReadCacheAsync(CancellationToken token)
    {
        ThunderstoreCatalog catalog;
        try
        {
            catalog = await AtomicJsonStore.ReadAsync<ThunderstoreCatalog>(cachePath, token);
            Validate(catalog);
        }
        catch (Exception error) when (error is InvalidDataException or JsonException && File.Exists(cachePath + ".bak"))
        {
            catalog = await AtomicJsonStore.ReadAsync<ThunderstoreCatalog>(cachePath + ".bak", token);
            Validate(catalog);
        }
        UsingCache = true;
        return catalog;
    }

    private async Task<byte[]> ReadJsonAsync(string url, CancellationToken token)
    {
        var uri = TrustedUri(url);
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        _ = TrustedUri(response.RequestMessage?.RequestUri?.AbsoluteUri ?? url);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            RetryAfter = response.Headers.RetryAfter?.Date
                ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumBytes) throw new InvalidDataException("Catalog response is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = await ReadBoundedAsync(stream, MaximumBytes, token);
        if (bytes.Length > 1 && bytes[0] == 0x1f && bytes[1] == 0x8b)
        {
            using var input = new MemoryStream(bytes);
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            bytes = await ReadBoundedAsync(gzip, MaximumBytes, token);
        }
        return bytes;
    }

    public static Uri TrustedUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)
            || !(uri.Host == "thunderstore.io" || uri.Host.EndsWith(".thunderstore.io", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Thunderstore downloads must use an HTTPS Thunderstore origin.");
        return uri;
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken token)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (memory.Length + read > limit) throw new InvalidDataException("Response exceeds the size limit.");
            await memory.WriteAsync(buffer.AsMemory(0, read), token);
        }
        return memory.ToArray();
    }

    public static IReadOnlyList<ThunderstorePackage> Parse(byte[] json) =>
        JsonSerializer.Deserialize<ThunderstorePackage[]>(json) ?? throw new InvalidDataException("Empty catalog.");

    public static void Validate(ThunderstoreCatalog catalog)
    {
        if (catalog.GameId != GameId.Silksong || catalog.Community != Community || catalog.Packages is null || catalog.Packages.Count is < 1 or > 25000)
            throw new InvalidDataException("Invalid Silksong catalog.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in catalog.Packages)
        {
            if (package is null || package.Categories is null || package.Versions is null || package.FullName != ThunderstoreIdentity.SafeName(package.Owner) + "-" + ThunderstoreIdentity.SafeName(package.Name)
                || !names.Add(package.FullName) || package.Versions.Count is < 1 or > 5000)
                throw new InvalidDataException("Invalid or duplicate package identity.");
            var versions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var version in package.Versions)
            {
                if (version is null || version.Dependencies is null || version.Description is null)
                    throw new InvalidDataException("Incomplete package version.");
                _ = ThunderstoreIdentity.Version(version.VersionNumber);
                if (version.Name != package.Name || version.FullName != package.FullName + "-" + version.VersionNumber
                    || !versions.Add(version.VersionNumber) || version.FileSize is < 1 or > 16L * 1024 * 1024 * 1024)
                    throw new InvalidDataException($"Invalid package version: {package.FullName}, {version.FullName}, {version.Name}, size={version.FileSize}.");
                _ = TrustedUri(version.DownloadUrl);
                foreach (var dependency in version.Dependencies) _ = ThunderstoreIdentity.Dependency(dependency);
            }
        }
    }
}
