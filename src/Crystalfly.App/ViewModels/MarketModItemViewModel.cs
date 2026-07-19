using Crystalfly.Core.Models;

namespace Crystalfly.App.ViewModels;

public sealed record MarketTagViewModel(string Value, string Name);

public sealed class MarketModItemViewModel
{
    public MarketModItemViewModel(
        ModManifest manifest,
        ModTranslationEntry? translation,
        IReadOnlyDictionary<string, string> tagNames,
        bool chinese)
    {
        Manifest = manifest;
        Id = manifest.Id;
        Version = manifest.Version;
        LoaderId = manifest.LoaderId;
        SourceName = manifest.SourceName;
        Authors = manifest.Authors;
        Integrations = manifest.Integrations;
        SupportedBuildIds = manifest.SupportedBuildIds;
        Dependencies = manifest.Dependencies;
        RepositoryUrl = manifest.RepositoryUrl;
        IssuesUrl = manifest.IssuesUrl;
        CanonicalTags = manifest.Tags;

        var officialName = manifest.DisplayName ?? manifest.Name;
        PrimaryName = chinese && !string.IsNullOrWhiteSpace(translation?.DisplayName)
            ? translation.DisplayName!
            : officialName;
        SecondaryName = chinese && !string.Equals(PrimaryName, officialName, StringComparison.Ordinal)
            ? officialName
            : string.Empty;

        var officialDescription = manifest.Description;
        PrimaryDescription = chinese && !string.IsNullOrWhiteSpace(translation?.Description)
            ? translation.Description
            : officialDescription;
        SecondaryDescription = chinese
            && !string.IsNullOrWhiteSpace(translation?.Description)
            && !string.Equals(translation.Description, officialDescription, StringComparison.Ordinal)
            ? officialDescription ?? string.Empty
            : string.Empty;

        Tags = manifest.Tags
            .Select(tag => new MarketTagViewModel(
                tag,
                chinese && tagNames.TryGetValue(tag, out var translatedTag)
                    ? translatedTag
                    : tag))
            .ToArray();
        var searchValues = new List<string>();
        Add(PrimaryName);
        Add(SecondaryName);
        Add(PrimaryDescription);
        Add(SecondaryDescription);
        Add(manifest.Name);
        Add(manifest.DisplayName);
        Add(manifest.Id);
        Add(manifest.Version);
        AddValues(manifest.Authors);
        AddValues(manifest.Integrations);
        AddValues(manifest.Tags);
        AddValues(Tags.Select(tag => tag.Name));
        SearchText = string.Join('\n', searchValues);

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                searchValues.Add(value);
            }
        }

        void AddValues(IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }
    }

    public ModManifest Manifest { get; }

    public string Id { get; }

    public string PrimaryName { get; }

    public string SecondaryName { get; }

    public string Name => PrimaryName;

    public string Version { get; }

    public string LoaderId { get; }

    public string? SourceName { get; }

    public string? PrimaryDescription { get; }

    public string SecondaryDescription { get; }

    public string? Description => PrimaryDescription;

    public IReadOnlyList<string> Authors { get; }

    public IReadOnlyList<string> Integrations { get; }

    public IReadOnlyList<string> SupportedBuildIds { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public IReadOnlyList<string> CanonicalTags { get; }

    public IReadOnlyList<MarketTagViewModel> Tags { get; }

    public string? RepositoryUrl { get; }

    public string? IssuesUrl { get; }

    public bool HasRepositoryUrl => IsHttpsUrl(RepositoryUrl);

    public bool HasIssuesUrl => IsHttpsUrl(IssuesUrl);

    public string SearchText { get; }

    public bool HasSecondaryName => !string.IsNullOrWhiteSpace(SecondaryName);

    public bool HasPrimaryDescription => !string.IsNullOrWhiteSpace(PrimaryDescription);

    public bool HasSecondaryDescription => !string.IsNullOrWhiteSpace(SecondaryDescription);

    public bool MatchesSearch(string? query) => string.IsNullOrWhiteSpace(query)
        || SearchText.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}
