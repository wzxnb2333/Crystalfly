using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Tests.Configuration;

public sealed class SpeedrunCommunityLinkDefinitionTests
{
    [Fact]
    public void TryNormalize_accepts_https_and_trims_metadata()
    {
        var input = new SpeedrunCommunityLinkDefinition
        {
            Id = " hk-speedrun ", Name = " HK Speedrunning ", Group = SpeedrunCommunityGroup.HollowKnight,
            Url = " https://github.com/hk-speedrunning/ ", IconKey = " github "
        };

        Assert.True(SpeedrunCommunityLinkDefinition.TryNormalize(input, out var normalized));
        Assert.Equal("hk-speedrun", normalized.Id);
        Assert.Equal("HK Speedrunning", normalized.Name);
        Assert.Equal("https://github.com/hk-speedrunning/", normalized.Url);
        Assert.Equal("github", normalized.IconKey);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void TryNormalize_rejects_non_https(string url)
    {
        var input = new SpeedrunCommunityLinkDefinition { Id = "id", Name = "name", Group = SpeedrunCommunityGroup.Other, Url = url, IconKey = "link" };
        Assert.False(SpeedrunCommunityLinkDefinition.TryNormalize(input, out _));
    }

    [Fact]
    public void NormalizeStoredLinks_deduplicates_ids_and_urls()
    {
        var links = new[]
        {
            new SpeedrunCommunityLinkDefinition { Id = "one", Name = "One", Group = SpeedrunCommunityGroup.Other, Url = "https://example.com/a", IconKey = "link" },
            new SpeedrunCommunityLinkDefinition { Id = "one", Name = "Duplicate", Group = SpeedrunCommunityGroup.Other, Url = "https://example.com/b", IconKey = "link" },
            new SpeedrunCommunityLinkDefinition { Id = "two", Name = "Duplicate url", Group = SpeedrunCommunityGroup.Other, Url = "https://example.com/a", IconKey = "link" }
        };

        var normalized = SpeedrunCommunityLinkDefinition.NormalizeStoredLinks(links);
        var only = Assert.Single(normalized);
        Assert.Equal("one", only.Id);
    }
}
