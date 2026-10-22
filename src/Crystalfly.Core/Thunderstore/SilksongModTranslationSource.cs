using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Thunderstore;

public sealed class SilksongModTranslationSource
{
    private const string ResourcePrefix = "Crystalfly.Core.SilksongTranslations.";
    private const int MaximumCatalogBytes = 16 * 1024 * 1024;
    private const int MaximumDocumentBytes = 1024 * 1024;
    private static readonly Uri RemoteRoot = new("https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/");
    private readonly HttpClient? client;
    private readonly string? cacheRoot;
    private readonly INetworkPolicy? networkPolicy;
    private readonly SilksongModTranslationCatalog embedded;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    public SilksongModTranslationCatalog Catalog { get; private set; }

    public SilksongModTranslationSource(HttpClient? client = null, string? cacheRoot = null,
        INetworkPolicy? networkPolicy = null, SilksongModTranslationCatalog? baseline = null)
    {
        this.client = client;
        this.cacheRoot = cacheRoot is null ? null : Path.GetFullPath(cacheRoot);
        this.networkPolicy = networkPolicy;
        embedded = Validate(baseline ?? LoadEmbedded());
        Catalog = embedded;
    }

    public SilksongModTranslationEntry? Find(string fullName) =>
        Catalog.Mods.FirstOrDefault(item => item.FullName.Equals(fullName, StringComparison.Ordinal));

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task RefreshAsync(bool offline, CancellationToken token = default)
    {
        await refreshGate.WaitAsync(token);
        try
        {
            if (cacheRoot is not null)
            {
                try
                {
                    var path = SafeCachePath("index.json");
                    if (File.Exists(path))
                    {
                        await using var input = File.OpenRead(path);
                        var bytes = await ThunderstoreCatalogSource.ReadBoundedAsync(input, MaximumCatalogBytes, token);
                        var cached = Parse(bytes);
                        if (cached.UpdatedAt >= embedded.UpdatedAt) Catalog = Merge(embedded, cached);
                    }
                }
                catch (Exception error) when (Recoverable(error, token)) { }
            }
            if (offline || client is null || networkPolicy?.IsOffline == true) return;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, networkPolicy?.GetOnlineCancellationToken() ?? default);
            linked.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var bytes = await DownloadAsync(new(RemoteRoot, "silksong-mod-translations.zh-CN.v1.json"), MaximumCatalogBytes, linked.Token);
                var update = Parse(bytes);
                if (update.UpdatedAt < Catalog.UpdatedAt) return;
                var merged = Merge(Catalog, update);
                if (cacheRoot is not null) await AtomicJsonStore.WriteAsync(SafeCachePath("index.json"), merged, linked.Token);
                Catalog = merged;
            }
            catch (Exception error) when (Recoverable(error, token)) { }
        }
        finally { refreshGate.Release(); }
    }

    public async Task<SilksongTranslatedDocument?> ReadDocumentAsync(string fullName, string version, string name,
        bool offline, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ValidateDocumentName(name);
        var entry = Find(fullName)?.Versions.FirstOrDefault(item => item.Version == version);
        if (entry is null || !entry.Documents.TryGetValue(name, out var document)) return null;
        if (document.Missing) return new("", null, true);
        var stem = name[..^3];
        var relative = $"{fullName}/{version}/{stem}";
        var original = await ReadTextAsync(relative + ".en.md", document.SourceSha256!, offline, token);
        if (original is null) return null;
        if (string.IsNullOrWhiteSpace(original)) return new("", null, true);
        var chinese = document.TranslationSha256 is null ? null
            : await ReadTextAsync(relative + ".zh-CN.md", document.TranslationSha256, offline, token);
        // A failed translation-only update must not hide a verified baseline of the same original.
        var baselineDocument = embedded.Mods.FirstOrDefault(item => item.FullName == fullName)?.Versions
            .FirstOrDefault(item => item.Version == version)?.Documents.GetValueOrDefault(name);
        if (chinese is null && baselineDocument?.TranslationSha256 is { } baselineHash
            && baselineDocument.SourceSha256?.Equals(document.SourceSha256, StringComparison.OrdinalIgnoreCase) == true
            && !baselineHash.Equals(document.TranslationSha256, StringComparison.OrdinalIgnoreCase))
            chinese = await ReadTextAsync(relative + ".zh-CN.md", baselineHash, offline: true, token);
        return new(original, chinese, false);
    }

    private async Task<string?> ReadTextAsync(string relative, string expectedHash, bool offline, CancellationToken token)
    {
        if (cacheRoot is not null)
        {
            try
            {
                var path = SafeCachePath("documents/" + relative);
                if (File.Exists(path))
                {
                    await using var stream = File.OpenRead(path);
                    var bytes = await ThunderstoreCatalogSource.ReadBoundedAsync(stream, MaximumDocumentBytes, token);
                    if (Matches(bytes, expectedHash)) return Decode(bytes);
                }
            }
            catch (Exception error) when (Recoverable(error, token)) { }
        }
        await using var resource = typeof(SilksongModTranslationSource).Assembly.GetManifestResourceStream(ResourcePrefix + relative);
        if (resource is not null)
        {
            var bytes = await ThunderstoreCatalogSource.ReadBoundedAsync(resource, MaximumDocumentBytes, token);
            if (Matches(bytes, expectedHash)) return Decode(bytes);
        }
        if (offline || client is null || networkPolicy?.IsOffline == true) return null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, networkPolicy?.GetOnlineCancellationToken() ?? default);
        linked.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var bytes = await DownloadAsync(new(RemoteRoot, "silksong-mod-documents/" + relative), MaximumDocumentBytes, linked.Token);
            if (!Matches(bytes, expectedHash)) return null;
            var text = Decode(bytes);
            if (cacheRoot is not null)
            {
                var path = SafeCachePath("documents/" + relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".part";
                try
                {
                    await File.WriteAllBytesAsync(temporary, bytes, linked.Token);
                    InstanceDirectory.RejectReparseAncestors(path);
                    File.Move(temporary, path, overwrite: true);
                }
                finally { File.Delete(temporary); }
            }
            return text;
        }
        catch (Exception error) when (Recoverable(error, token)) { return null; }
    }

    private async Task<byte[]> DownloadAsync(Uri uri, int limit, CancellationToken token)
    {
        using var response = await client!.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Translation response is too large.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        return await ThunderstoreCatalogSource.ReadBoundedAsync(input, limit, token);
    }

    private string SafeCachePath(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(cacheRoot!, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(cacheRoot! + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Translation cache path escapes its root.");
        InstanceDirectory.RejectReparseAncestors(path);
        return path;
    }

    private static SilksongModTranslationCatalog LoadEmbedded()
    {
        using var stream = typeof(SilksongModTranslationSource).Assembly.GetManifestResourceStream(ResourcePrefix + "index.json")
            ?? throw new InvalidOperationException("Silksong translation resource is missing.");
        return JsonSerializer.Deserialize<SilksongModTranslationCatalog>(stream, CrystalflyJson.Options)
            ?? throw new InvalidDataException("Silksong translation resource is empty.");
    }

    private static SilksongModTranslationCatalog Parse(byte[] bytes) => Validate(
        JsonSerializer.Deserialize<SilksongModTranslationCatalog>(bytes, CrystalflyJson.Options)
        ?? throw new InvalidDataException("Translation catalog is empty."));

    public static SilksongModTranslationCatalog Validate(SilksongModTranslationCatalog value)
    {
        if (value.SchemaVersion != 1 || value.Language != "zh-CN" || value.Community != ThunderstoreCatalogSource.Community
            || value.Mods is null || value.Mods.Count > 25000 || value.CategoryNames is null || value.CategoryNames.Count > 256)
            throw new InvalidDataException("Invalid Silksong translation catalog.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in value.CategoryNames)
        {
            Text(category.Key, 128);
            Text(category.Value, 128);
        }
        foreach (var mod in value.Mods)
        {
            if (mod is null) throw new InvalidDataException("Invalid translation identity.");
            Text(mod.FullName, 200);
            if (!ids.Add(mod.FullName)) throw new InvalidDataException("Duplicate translation identity.");
            _ = ThunderstoreIdentity.SafeName(mod.FullName);
            Text(mod.DisplayName, 200);
            var uri = ThunderstoreCatalogSource.TrustedUri(mod.SourceUrl);
            if (uri.Host != "thunderstore.io" || !uri.AbsolutePath.StartsWith("/c/" + ThunderstoreCatalogSource.Community + "/p/", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid translation source.");
            if (mod.Versions is null || mod.Versions.Count > 5000) throw new InvalidDataException("Too many translated versions.");
            var versions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var version in mod.Versions)
            {
                if (version is null) throw new InvalidDataException("Invalid translated version.");
                Text(version.Version, 100);
                if (!versions.Add(version.Version)) throw new InvalidDataException("Duplicate translated version.");
                _ = ThunderstoreIdentity.Version(version.Version);
                if (version.Description is not null) { Text(version.Description, 16384); HashText(version.DescriptionSha256); }
                if (version.Documents is null || version.Documents.Count > 2) throw new InvalidDataException("Invalid document index.");
                foreach (var pair in version.Documents)
                {
                    ValidateDocumentName(pair.Key);
                    var document = pair.Value ?? throw new InvalidDataException("Invalid document entry.");
                    if (document.Missing)
                    {
                        if (document.SourceSha256 is not null || document.TranslationSha256 is not null) throw new InvalidDataException("Missing document has content.");
                    }
                    else { HashText(document.SourceSha256); if (document.TranslationSha256 is not null) HashText(document.TranslationSha256); }
                }
            }
        }
        return value;
    }

    private static SilksongModTranslationCatalog Merge(SilksongModTranslationCatalog baseline, SilksongModTranslationCatalog overlay)
    {
        var mods = baseline.Mods.ToDictionary(item => item.FullName, StringComparer.Ordinal);
        foreach (var item in overlay.Mods)
        {
            if (mods.TryGetValue(item.FullName, out var previous))
                mods[item.FullName] = item with { Versions = previous.Versions.Where(v => !item.Versions.Any(u => u.Version == v.Version)).Concat(item.Versions).ToArray() };
            else mods[item.FullName] = item;
        }
        var categories = new Dictionary<string, string>(baseline.CategoryNames, StringComparer.Ordinal);
        foreach (var item in overlay.CategoryNames) categories[item.Key] = item.Value;
        return overlay with { Mods = mods.Values.OrderBy(item => item.FullName, StringComparer.Ordinal).ToArray(), CategoryNames = categories };
    }

    private static void Text(string? text, int limit)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > limit) throw new InvalidDataException("Invalid translation text.");
    }
    private static void HashText(string? hash)
    {
        if (hash is null || hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid document digest.");
    }
    private static void ValidateDocumentName(string name)
    {
        if (name is not ("README.md" or "CHANGELOG.md")) throw new InvalidDataException("Unsupported document name.");
    }
    private static bool Matches(byte[] bytes, string hash) => Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase);
    private static string Decode(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
    private static bool Recoverable(Exception error, CancellationToken token) => error is IOException or InvalidDataException or JsonException
        or HttpRequestException or UnauthorizedAccessException or DecoderFallbackException
        || error is OperationCanceledException && !token.IsCancellationRequested;
}
