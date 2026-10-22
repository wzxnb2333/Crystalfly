using System.Text.Json.Serialization;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Thunderstore;

public sealed record ThunderstorePackage
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("owner")] public string Owner { get; init; } = "";
    [JsonPropertyName("full_name")] public string FullName { get; init; } = "";
    [JsonPropertyName("package_url")] public string PackageUrl { get; init; } = "";
    [JsonPropertyName("is_deprecated")] public bool IsDeprecated { get; init; }
    [JsonPropertyName("categories")] public IReadOnlyList<string> Categories { get; init; } = [];
    [JsonPropertyName("rating_score")] public int RatingScore { get; init; }
    [JsonPropertyName("date_updated")] public DateTimeOffset UpdatedAt { get; init; }
    [JsonPropertyName("versions")] public IReadOnlyList<ThunderstoreVersion> Versions { get; init; } = [];
    [JsonIgnore] public bool IsModpack => Categories.Contains("Modpacks", StringComparer.OrdinalIgnoreCase);
    [JsonIgnore] public ThunderstoreVersion? Latest => Versions.Where(version => version.IsActive)
        .OrderByDescending(version => ThunderstoreIdentity.Version(version.VersionNumber)).FirstOrDefault();
    public override string ToString() => FullName;
}

public sealed record ThunderstoreVersion
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("full_name")] public string FullName { get; init; } = "";
    [JsonPropertyName("version_number")] public string VersionNumber { get; init; } = "";
    [JsonPropertyName("description")] public string Description { get; init; } = "";
    [JsonPropertyName("dependencies")] public IReadOnlyList<string> Dependencies { get; init; } = [];
    [JsonPropertyName("download_url")] public string DownloadUrl { get; init; } = "";
    [JsonPropertyName("icon")] public string IconUrl { get; init; } = "";
    [JsonPropertyName("website_url")] public string WebsiteUrl { get; init; } = "";
    [JsonPropertyName("file_size")] public long FileSize { get; init; }
    [JsonPropertyName("downloads")] public long Downloads { get; init; }
    [JsonPropertyName("is_active")] public bool IsActive { get; init; } = true;
    public override string ToString() => VersionNumber;
}

public sealed record ThunderstoreCatalog
{
    public GameId GameId { get; init; } = GameId.Silksong;
    public string Community { get; init; } = ThunderstoreCatalogSource.Community;
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<ThunderstorePackage> Packages { get; init; } = [];
}

public enum GameCompatibility { Verified, Unverified, Incompatible }
public sealed record ThunderstoreResolvedPackage(ThunderstorePackage Package, ThunderstoreVersion Version,
    string? PreviousVersion = null, bool Pinned = false)
{
    public bool IsLocal { get; init; }
    public bool? DesiredEnabled { get; init; }
    public bool? DesiredPinned { get; init; }
}
public sealed record ThunderstoreInstallPlan(GameId GameId, string BuildId,
    IReadOnlyList<ThunderstoreResolvedPackage> Packages, IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string> RequiredEnabled { get; init; } = [];
}
public sealed record ThunderstoreFileReceipt(string Path, string Sha256, bool IsConfiguration = false);
public sealed record ThunderstoreInstalledPackage
{
    public string FullName { get; init; } = "";
    public string Version { get; init; } = "";
    public string ArchiveSha256 { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public bool Pinned { get; init; }
    public bool IsLocal { get; init; }
    public ThunderstorePackage? LocalPackage { get; init; }
    public bool IsLoader { get; init; }
    public string? RuntimeVersion { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public IReadOnlyList<ThunderstoreFileReceipt> Files { get; init; } = [];
    public IReadOnlyList<ThunderstoreLoaderBackupFile> LoaderBackups { get; init; } = [];
}
public sealed record ThunderstoreLoaderBackupFile(string Path, string BackupPath, string Sha256);
public sealed record ThunderstoreInstallState
{
    public int SchemaVersion { get; init; } = 1;
    public GameId GameId { get; init; } = GameId.Silksong;
    public IReadOnlyList<ThunderstoreInstalledPackage> Packages { get; init; } = [];
}
public sealed record ThunderstorePreset(string Name, GameId GameId, string BuildId,
    IReadOnlyList<ThunderstorePresetEntry> Packages);
public sealed record ThunderstorePresetEntry(string FullName, string Version, bool Enabled, bool Pinned);

public static class ThunderstoreIdentity
{
    public static Version Version(string value) => System.Version.TryParse(value, out var parsed)
        && parsed.Major >= 0 && parsed.Minor >= 0 && parsed.Build >= 0 && parsed.Revision < 0
        ? parsed : throw new InvalidDataException($"Invalid Thunderstore version: {value}");
    public static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
            throw new InvalidDataException("Invalid Thunderstore package identity.");
        return value;
    }
    public static (string Name, string Version) Dependency(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Empty dependency.");
        var separator = value.LastIndexOf('-');
        if (separator < 1) throw new InvalidDataException($"Invalid dependency: {value}");
        var name = SafeName(value[..separator]);
        var version = value[(separator + 1)..];
        _ = Version(version);
        return (name, version);
    }
    public static (string Name, string? Version) ParseLink(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "thunderstore.io" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Use an HTTPS Thunderstore Silksong package link.");
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length is not (5 or 6) || parts[0] != "c" || parts[1] != ThunderstoreCatalogSource.Community || parts[2] != "p")
            throw new InvalidDataException("This link does not belong to the Silksong community.");
        var name = SafeName(parts[3]) + "-" + SafeName(parts[4]);
        var version = parts.Length == 6 ? parts[5] : null;
        if (version is not null) _ = Version(version);
        return (name, version);
    }
}
