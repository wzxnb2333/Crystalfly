using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Thunderstore;

public sealed record ThunderstoreArchiveManifest
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("version_number")] public string Version { get; init; } = "";
    [JsonPropertyName("dependencies")] public IReadOnlyList<string> Dependencies { get; init; } = [];
}
public sealed record ThunderstoreCachedPackage(string Path, string Sha256);

public sealed class ThunderstorePackageStore(HttpClient client, string cacheRoot, INetworkPolicy? networkPolicy = null)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private const long MaxPackageBytes = 16L * 1024 * 1024 * 1024;
    private DateTimeOffset retryAfter;
    public string Root => Path.GetFullPath(cacheRoot);

    public async Task<ThunderstoreCachedPackage> ImportLocalAsync(string source, ThunderstoreResolvedPackage package,
        CancellationToken token = default)
    {
        if (new FileInfo(source).Length > MaxPackageBytes) throw new InvalidDataException("Local archive exceeds size limit.");
        await ValidateAsync(source, package, token);
        InstanceDirectory.RejectReparseAncestors(Root);
        Directory.CreateDirectory(Root);
        var name = ThunderstoreIdentity.SafeName(package.Package.FullName) + "-" + package.Version.VersionNumber;
        _ = ThunderstoreIdentity.Version(package.Version.VersionNumber);
        var target = Path.Combine(Root, name + ".zip");
        var gate = Gates.GetOrAdd(target, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            InstanceDirectory.RejectReparseAncestors(target);
            var hash = await HashAsync(source, token);
            var digestPath = target + ".sha256.json";
            InstanceDirectory.RejectReparseAncestors(digestPath);
            if (File.Exists(digestPath) && await AtomicJsonStore.ReadAsync<string>(digestPath, token) != hash)
                throw new InvalidDataException("This local package version already has different contents.");
            if (!string.Equals(Path.GetFullPath(source), target, StringComparison.OrdinalIgnoreCase))
            {
                var partial = target + "." + Guid.NewGuid().ToString("N") + ".part";
                try
                {
                    File.Copy(source, partial);
                    if (await HashAsync(partial, token) != hash) throw new InvalidDataException("Local package changed during import.");
                    await AtomicJsonStore.WriteAsync(digestPath, hash, token);
                    File.Move(partial, target, overwrite: true);
                }
                finally { File.Delete(partial); }
            }
            return new(target, hash);
        }
        finally { gate.Release(); }
    }

    public async Task<ThunderstoreCachedPackage> GetAsync(ThunderstoreResolvedPackage package, bool offline,
        CancellationToken cancellationToken = default, bool refresh = false)
    {
        var key = ThunderstoreIdentity.SafeName(package.Package.FullName) + "-" + package.Version.VersionNumber;
        _ = ThunderstoreIdentity.Version(package.Version.VersionNumber);
        var path = Path.Combine(Root, key + ".zip");
        InstanceDirectory.RejectReparseAncestors(Root);
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            InstanceDirectory.RejectReparseAncestors(path);
            var digestPath = path + ".sha256.json";
            InstanceDirectory.RejectReparseAncestors(digestPath);
            string? pinnedDigest = File.Exists(digestPath) ? await AtomicJsonStore.ReadAsync<string>(digestPath, cancellationToken) : null;
            if (File.Exists(path) && !refresh)
            {
                if (pinnedDigest is null || !string.Equals(await HashAsync(path, cancellationToken), pinnedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The cached package is corrupted; its contents were not replaced.");
                await ValidateAsync(path, package, cancellationToken);
                return new(path, pinnedDigest);
            }
            if (offline || networkPolicy?.IsOffline == true) throw new IOException("Package is not available in the offline cache.");
            if (package.IsLocal) throw new IOException("Re-import the local ZIP to restore its cache.");
            if (retryAfter > DateTimeOffset.UtcNow) throw new HttpRequestException("Thunderstore requested a retry delay.", null, System.Net.HttpStatusCode.TooManyRequests);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, networkPolicy?.GetOnlineCancellationToken() ?? default);
            Directory.CreateDirectory(Root);
            var partial = Path.Combine(Root, $".{Guid.NewGuid():N}.part");
            try
            {
                var url = ThunderstoreCatalogSource.TrustedUri(package.Version.DownloadUrl);
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linked.Token);
                _ = ThunderstoreCatalogSource.TrustedUri(response.RequestMessage?.RequestUri?.AbsoluteUri ?? url.AbsoluteUri);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    retryAfter = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
                response.EnsureSuccessStatusCode();
                if (package.Version.FileSize is < 1 or > MaxPackageBytes
                    || response.Content.Headers.ContentLength is { } size && size != package.Version.FileSize)
                    throw new InvalidDataException("Package size does not match the directory.");
                await using (var input = await response.Content.ReadAsStreamAsync(linked.Token))
                await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long length = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, linked.Token)) > 0)
                    {
                        length += read;
                        if (length > package.Version.FileSize) throw new InvalidDataException("Package exceeds declared size.");
                        await output.WriteAsync(buffer.AsMemory(0, read), linked.Token);
                    }
                    if (length != package.Version.FileSize) throw new InvalidDataException("Truncated package.");
                    await output.FlushAsync(linked.Token);
                    output.Flush(true);
                }
                var hash = await HashAsync(partial, linked.Token);
                if (pinnedDigest is not null && !string.Equals(hash, pinnedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The publisher changed the contents of this version. The existing cache was preserved.");
                if (SilksongLoaderAdapter.IsLoader(package.Package.FullName)
                    && (package.Version.VersionNumber != SilksongLoaderAdapter.PackageVersion || hash != SilksongLoaderAdapter.Sha256))
                    throw new InvalidDataException("The approved Silksong loader checksum does not match.");
                await ValidateAsync(partial, package, linked.Token);
                await AtomicJsonStore.WriteAsync(digestPath, hash, linked.Token);
                File.Move(partial, path, overwrite: true);
                return new(path, hash);
            }
            finally { File.Delete(partial); }
        }
        finally { gate.Release(); }
    }

    public static async Task<ThunderstoreArchiveManifest> ValidateAsync(string path, ThunderstoreResolvedPackage? expected,
        CancellationToken token = default)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count is < 1 or > 20000) throw new InvalidDataException("Invalid archive entry count.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = ValidateEntry(entry);
            if (!names.Add(name)) throw new InvalidDataException("Duplicate archive path.");
            expanded = checked(expanded + entry.Length);
            if (expanded > 32L * 1024 * 1024 * 1024 || entry.Length > MaxPackageBytes)
                throw new InvalidDataException("Expanded archive exceeds the size limit.");
        }
        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("A standard package requires root manifest.json.");
        if (manifestEntry.Length > 128 * 1024) throw new InvalidDataException("Manifest is too large.");
        await using var stream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<ThunderstoreArchiveManifest>(await ThunderstoreCatalogSource.ReadBoundedAsync(stream, 128 * 1024, token))
            ?? throw new InvalidDataException("Empty package manifest.");
        _ = ThunderstoreIdentity.SafeName(manifest.Name);
        _ = ThunderstoreIdentity.Version(manifest.Version);
        if (manifest.Dependencies is null) throw new InvalidDataException("Missing dependency list.");
        foreach (var dependency in manifest.Dependencies) _ = ThunderstoreIdentity.Dependency(dependency);
        if (expected is not null && (manifest.Name != expected.Package.Name || manifest.Version != expected.Version.VersionNumber
            || !manifest.Dependencies.Order(StringComparer.Ordinal).SequenceEqual(expected.Version.Dependencies.Order(StringComparer.Ordinal))))
            throw new InvalidDataException("Archive identity or dependencies do not match the selected package.");
        return manifest;
    }

    public static string ValidateEntry(ZipArchiveEntry entry)
    {
        var name = entry.FullName.Replace('\\', '/');
        var parts = name.TrimEnd('/').Split('/');
        if (name.StartsWith('/') || parts.Any(part => part.Length == 0 || part is "." or ".."
            || IsDeviceName(part) || part.Contains(':') || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000
            || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Unsafe archive entry.");
        return name;
    }

    private static bool IsDeviceName(string part)
    {
        var stem = part.Split('.')[0];
        return new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase)
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && stem[3] is >= '0' and <= '9';
    }

    public static string? MapPath(string packageName, string entryPath, bool loader)
    {
        var path = entryPath.Replace('\\', '/');
        if (path.EndsWith('/')) return null;
        if (path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)
            || path.Equals("icon.png", StringComparison.OrdinalIgnoreCase)
            || path.Equals("README.md", StringComparison.OrdinalIgnoreCase)
            || path.Equals("CHANGELOG.md", StringComparison.OrdinalIgnoreCase)) return null;
        if (loader)
        {
            if (packageName != SilksongLoaderAdapter.PackageName || !path.StartsWith("BepInExPack/", StringComparison.Ordinal)) return null;
            path = path["BepInExPack/".Length..];
            if (path.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase)
                || path is "winhttp.dll" or "doorstop_config.ini" or ".doorstop_version") return path;
            // Do not replace Steam's app ID or install launch scripts for other platforms.
            return null;
        }
        var relative = path.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase) ? path[8..] : path;
        var parts = relative.Split('/', 2);
        var route = parts[0].ToLowerInvariant();
        if (parts.Length == 2 && route is "plugins" or "patchers" or "monomod" or "core" or "config")
            return route == "config" ? $"BepInEx/config/{parts[1]}" : $"BepInEx/{route}/{packageName}/{parts[1]}";
        route = path.EndsWith(".mm.dll", StringComparison.OrdinalIgnoreCase) ? "monomod" : "plugins";
        return $"BepInEx/{route}/{packageName}/{path}";
    }

    public static void ValidateArchitecture(string path)
    {
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return;
        using var input = File.OpenRead(path);
        using var reader = new PEReader(input);
        if (reader.PEHeaders.CorHeader is { } cor)
        {
            if ((cor.Flags & CorFlags.Requires32Bit) != 0) throw new InvalidDataException("The package contains an x86-only assembly.");
        }
        else if (reader.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("The package contains an unsupported native architecture.");
    }

    public static async Task<string> HashAsync(string path, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }

    public static async Task<string> ReadDocumentAsync(string archivePath, string name, CancellationToken token = default)
    {
        if (name is not ("README.md" or "CHANGELOG.md")) throw new ArgumentException("Unsupported document.");
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.Entries.FirstOrDefault(item => item.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return "";
        await using var stream = entry.Open();
        return System.Text.Encoding.UTF8.GetString(await ThunderstoreCatalogSource.ReadBoundedAsync(stream, 1024 * 1024, token));
    }
}
