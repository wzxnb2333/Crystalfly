using Crystalfly.App.ViewModels;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class MarketModItemViewModelTests
{
    [Theory]
    [InlineData("ModCommon", "ModCommon", "ModCommon 通用开发库")]
    [InlineData("ModConsole", "ModConsole", "模组交互控制台")]
    [InlineData("ModScript", "ModScript", "JavaScript 模组脚本")]
    [InlineData("ModTerminal", "ModTerminal", "模组命令终端")]
    [InlineData("MoreLocations", "MoreLocations", "更多随机化地点")]
    [InlineData("MoreMasks", "More Masks", "更多生命面具显示")]
    [InlineData("MoreStags", "MoreStags", "更多鹿角虫车站")]
    public void Embedded_names_display_in_Chinese_and_preserve_official_name_search(
        string modName, string officialDisplayName, string expectedName)
    {
        var catalog = EmbeddedModTranslationCatalog.Load();
        var manifest = Manifest() with
        {
            Id = $"hkmod:{modName}",
            Name = modName,
            DisplayName = officialDisplayName
        };
        var translation = Assert.Single(catalog.Mods, entry => entry.Id == manifest.Id);
        var item = new MarketModItemViewModel(manifest, translation, catalog.TagNames, chinese: true);

        Assert.Same(manifest, item.Manifest);
        Assert.Equal(expectedName, item.PrimaryName);
        Assert.Equal(officialDisplayName, item.SecondaryName);
        Assert.True(item.MatchesSearch(expectedName));
        Assert.True(item.MatchesSearch(officialDisplayName));
        Assert.True(item.MatchesSearch(modName));

        var english = new MarketModItemViewModel(manifest, translation, catalog.TagNames, chinese: false);
        Assert.Equal(officialDisplayName, english.PrimaryName);
        Assert.Empty(english.SecondaryName);
    }

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
    public void Official_1578_projection_lists_latest_build_and_api78_compatibility()
    {
        var item = new MarketModItemViewModel(
            Manifest() with { SourceName = "HK ModLinks" },
            null,
            new Dictionary<string, string>(),
            chinese: false);

        Assert.Equal(["1.5.78.11833", "1.5.12620.0"], item.SupportedBuildIds);
        Assert.Equal(["modding-api-77", "modding-api-78"], item.CompatibleLoaderIds);
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
