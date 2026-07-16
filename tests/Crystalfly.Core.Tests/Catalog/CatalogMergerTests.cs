using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class CatalogMergerTests
{
    [Fact]
    public void Merge_prefers_remote_trusted_data_and_custom_sources_only_add_entries()
    {
        var embedded = CreateCatalog(CreateBuild("official", "embedded"));
        var cache = CreateCatalog(CreateBuild("official", "cache"));
        var remote = CreateCatalog(CreateBuild("official", "remote"));
        var custom = CreateCatalog(
            CreateBuild("official", "custom-override"),
            CreateBuild("community", "custom-addition"));

        var merged = CatalogMerger.Merge(embedded, cache, remote, [custom]);

        Assert.Equal("remote", merged.Builds.Single(build => build.Id == "official").ManifestId);
        Assert.Equal("custom-addition", merged.Builds.Single(build => build.Id == "community").ManifestId);
    }

    private static GameCatalog CreateCatalog(params GameBuild[] builds) => new()
    {
        Builds = builds
    };

    private static GameBuild CreateBuild(string id, string manifestId) => new()
    {
        Id = id,
        DisplayVersion = id,
        DepotId = 367521,
        ManifestId = manifestId,
        ExecutableSha256 = new string('A', 64),
        GlobalGameManagersSha256 = new string('B', 64)
    };
}
