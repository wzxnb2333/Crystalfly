using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class MarketModItemViewModelTests
{
    [Fact]
    public void Chinese_projection_keeps_manifest_and_localizes_display_fields()
    {
        var manifest = Manifest();
        var item = new MarketModItemViewModel(
            manifest,
            new ModTranslationEntry
            {
                Id = manifest.Id,
                DisplayName = "中文名称",
                Description = "中文说明"
            },
            new Dictionary<string, string> { ["Gameplay"] = "玩法" },
            chinese: true);

        Assert.Same(manifest, item.Manifest);
        Assert.Equal("中文名称", item.PrimaryName);
        Assert.Equal("Official Name", item.SecondaryName);
        Assert.Equal("中文说明", item.PrimaryDescription);
        Assert.Equal("English description", item.SecondaryDescription);
        Assert.Equal("玩法", Assert.Single(item.Tags).Name);
        Assert.Equal("Gameplay", Assert.Single(item.Tags).Value);
        Assert.True(item.MatchesSearch("中文"));
        Assert.True(item.MatchesSearch("Official Name"));
        Assert.True(item.HasRepositoryUrl);
        Assert.True(item.HasIssuesUrl);
    }

    [Fact]
    public void English_projection_falls_back_to_official_fields_without_translation()
    {
        var manifest = Manifest() with
        {
            RepositoryUrl = "http://example.test/repository",
            IssuesUrl = "javascript:alert(1)"
        };
        var item = new MarketModItemViewModel(
            manifest,
            new ModTranslationEntry { Id = manifest.Id, DisplayName = "中文名称" },
            new Dictionary<string, string> { ["Gameplay"] = "玩法" },
            chinese: false);

        Assert.Equal("Official Name", item.PrimaryName);
        Assert.Empty(item.SecondaryName);
        Assert.Equal("English description", item.PrimaryDescription);
        Assert.Empty(item.SecondaryDescription);
        Assert.Equal("Gameplay", Assert.Single(item.Tags).Name);
        Assert.False(item.MatchesSearch("中文"));
        Assert.False(item.HasRepositoryUrl);
        Assert.False(item.HasIssuesUrl);
    }

    [Fact]
    public void Activity_projection_marks_recent_additions_and_updates_against_catalog_cutoff()
    {
        var item = new MarketModItemViewModel(
            Manifest(),
            null,
            new Dictionary<string, string>(),
            chinese: false,
            activity: new ModActivityEntry
            {
                Id = "hkmod:Example",
                AddedAt = DateTimeOffset.Parse("2026-07-20T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-07-21T00:00:00Z")
            },
            recentCutoff: DateTimeOffset.Parse("2026-06-22T00:00:00Z"));

        Assert.True(item.IsRecentlyAdded);
        Assert.True(item.IsRecentlyUpdated);
        Assert.Equal(DateTimeOffset.Parse("2026-07-21T00:00:00Z"), item.UpdatedAt);
    }

    private static ModManifest Manifest() => new()
    {
        Id = "hkmod:Example",
        Name = "Official Name",
        DisplayName = "Official Name",
        Description = "English description",
        Version = "1.0.0",
        DownloadUrl = "https://example.test/mod.zip",
        Sha256 = new string('a', 64),
        LoaderId = "modding-api-77",
        SupportedBuildIds = ["1.5.78.11833"],
        Tags = ["Gameplay"],
        RepositoryUrl = "https://example.test/repository",
        IssuesUrl = "https://example.test/issues"
    };
}
