namespace Crystalfly.Core.Configuration;

public enum SpeedrunCommunityGroup
{
    HollowKnight,
    Silksong,
    Other
}

public sealed record SpeedrunCommunityLinkDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public SpeedrunCommunityGroup Group { get; init; }
    public required string Url { get; init; }
    public string IconKey { get; init; } = "link";

    public static bool TryNormalize(SpeedrunCommunityLinkDefinition? value, out SpeedrunCommunityLinkDefinition normalized)
    {
        normalized = null!;
        if (value is null)
        {
            return false;
        }

        var id = value.Id?.Trim() ?? string.Empty;
        var name = value.Name?.Trim() ?? string.Empty;
        var icon = value.IconKey?.Trim() ?? "link";
        var url = value.Url?.Trim() ?? string.Empty;
        if (id.Length == 0 || id.Length > 80 || name.Length == 0 || name.Length > 120 || icon.Length > 40
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalized = value with
        {
            Id = id,
            Name = name,
            Url = uri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped),
            IconKey = icon.Length == 0 ? "link" : icon
        };
        return true;
    }

    public static IReadOnlyList<SpeedrunCommunityLinkDefinition> NormalizeStoredLinks(IEnumerable<SpeedrunCommunityLinkDefinition>? links)
    {
        if (links is null || !links.Any()) return Array.Empty<SpeedrunCommunityLinkDefinition>();
        var result = new List<SpeedrunCommunityLinkDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in links ?? [])
        {
            if (!TryNormalize(link, out var normalized)
                || !ids.Add(normalized.Id)
                || !urls.Add(normalized.Url))
            {
                continue;
            }
            result.Add(normalized);
        }
        return result.ToArray();
    }
}
