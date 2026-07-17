using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class CatalogMergerTests
{
    [Fact]
    public void Merge_uses_remote_speedrun_file_manifest_for_same_id()
    {
        var embedded = new GameCatalog
        {
            SpeedrunFileManifests = [CreateManifest("official-files", "old")]
        };
        var remote = new GameCatalog
        {
            SpeedrunFileManifests = [CreateManifest("official-files", "new")]
        };

        var merged = CatalogMerger.Merge(embedded, null, remote);

        Assert.Equal("new", Assert.Single(merged.SpeedrunFileManifests).RulesRevision);
    }

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

    [Fact]
    public void Merge_only_uses_non_executable_cache_metadata()
    {
        var embedded = new GameCatalog
        {
            Builds = [CreateBuild("official", "embedded")],
            Channels = [new GameChannel { Name = "stable", BuildId = "official" }],
            SpeedrunTemplates =
            [
                new SpeedrunTemplate
                {
                    Id = "official-template",
                    Name = "Official",
                    BuildId = "official",
                    IsOfficial = true,
                    RulesRevision = "embedded",
                    FileManifestId = "official-files"
                }
            ],
            SpeedrunAssets =
            [
                new SpeedrunAsset
                {
                    Id = "official-asset",
                    Name = "Official",
                    Version = "embedded",
                    DownloadUrl = "https://example.invalid/embedded.zip",
                    SizeBytes = 1,
                    Sha256 = new string('A', 64),
                    SupportedBuildIds = ["official"]
                }
            ],
            SpeedrunFileManifests = [CreateManifest("official-files", "embedded", "official")]
        };
        var cache = new GameCatalog
        {
            Builds =
            [
                embedded.Builds[0] with
                {
                    ManifestId = "forged",
                    ExecutableSha256 = new string('C', 64)
                },
                CreateBuild("cached-build", "cached")
            ],
            Channels =
            [
                new GameChannel { Name = "stable", BuildId = "cached-build" },
                new GameChannel { Name = "cached-channel", BuildId = "official" },
                new GameChannel { Name = "invalid-channel", BuildId = "cached-build" }
            ],
            Loaders =
            [
                new LoaderManifest
                {
                    Id = "cached-loader",
                    Name = "Cached",
                    Version = "1",
                    DownloadUrl = "https://example.invalid/cached-loader.zip",
                    SizeBytes = 1,
                    Sha256 = new string('C', 64),
                    SupportedBuildIds = ["official"]
                }
            ],
            Mods =
            [
                new ModManifest
                {
                    Id = "cached-mod",
                    Name = "Cached",
                    Version = "1",
                    DownloadUrl = "https://example.invalid/cached-mod.zip",
                    SizeBytes = 1,
                    Sha256 = new string('C', 64),
                    LoaderId = "cached-loader",
                    SupportedBuildIds = ["official"]
                }
            ],
            SpeedrunTemplates =
            [
                embedded.SpeedrunTemplates[0] with { RulesRevision = "forged" },
                new SpeedrunTemplate
                {
                    Id = "cached-template",
                    Name = "Cached",
                    BuildId = "official",
                    IsOfficial = true,
                    RulesRevision = "cached",
                    FileManifestId = "cached-files",
                    LoaderId = "cached-loader",
                    RequiredAssetIds = ["cached-asset"]
                }
            ],
            SpeedrunAssets =
            [
                embedded.SpeedrunAssets[0] with
                {
                    Version = "forged",
                    Sha256 = new string('C', 64)
                },
                new SpeedrunAsset
                {
                    Id = "cached-asset",
                    Name = "Cached",
                    Version = "1",
                    DownloadUrl = "https://example.invalid/cached-asset.zip",
                    SizeBytes = 1,
                    Sha256 = new string('C', 64),
                    SupportedBuildIds = ["official"]
                }
            ],
            SpeedrunFileManifests =
            [
                CreateManifest("official-files", "forged", "official", 'C'),
                CreateManifest("cached-files", "cached", "official", 'C')
            ]
        };

        var merged = CatalogMerger.Merge(embedded, cache, null);

        Assert.Equal("embedded", merged.Builds.Single(build => build.Id == "official").ManifestId);
        Assert.Equal(new string('A', 64), merged.Builds.Single(build => build.Id == "official").ExecutableSha256);
        Assert.DoesNotContain(merged.Builds, build => build.Id == "cached-build");
        Assert.Equal("official", merged.Channels.Single(channel => channel.Name == "stable").BuildId);
        Assert.Contains(merged.Channels, channel => channel.Name == "cached-channel");
        Assert.DoesNotContain(merged.Channels, channel => channel.Name == "invalid-channel");
        Assert.Empty(merged.Loaders);
        Assert.Empty(merged.Mods);
        Assert.Equal("embedded", merged.SpeedrunTemplates.Single(template => template.Id == "official-template").RulesRevision);
        Assert.DoesNotContain(merged.SpeedrunTemplates, template => template.Id == "cached-template");
        Assert.Equal("embedded", merged.SpeedrunAssets.Single(asset => asset.Id == "official-asset").Version);
        Assert.Equal(new string('A', 64), merged.SpeedrunAssets.Single(asset => asset.Id == "official-asset").Sha256);
        Assert.DoesNotContain(merged.SpeedrunAssets, asset => asset.Id == "cached-asset");
        Assert.Equal("embedded", merged.SpeedrunFileManifests.Single(manifest => manifest.Id == "official-files").RulesRevision);
        Assert.Equal(
            new string('A', 64),
            Assert.Single(merged.SpeedrunFileManifests.Single(manifest => manifest.Id == "official-files").Files).Sha256);
        Assert.DoesNotContain(merged.SpeedrunFileManifests, manifest => manifest.Id == "cached-files");
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

    private static SpeedrunFileManifest CreateManifest(
        string id,
        string revision,
        string buildId = "1.2.2.1",
        char hash = 'A') => new()
    {
        Id = id,
        BuildId = buildId,
        RulesRevision = revision,
        Files =
        [
            new SpeedrunFileRule
            {
                RelativePath = "hollow_knight.exe",
                Sha256 = new string(hash, 64)
            }
        ]
    };
}
