using System.Net;
using System.Text;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class CustomCatalogSourceTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-custom-catalog-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadWithCache_writes_valid_remote_source_and_namespaces_result()
    {
        var source = new GameCatalog { Mods = [Mod("helper")] };
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                CrystalflyJson.Serialize(source),
                Encoding.UTF8,
                "application/json")
        });
        var cachePath = Path.Combine(directory, "community.json");

        var result = await CustomCatalogSource.LoadWithCacheAsync(
            "community",
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.Equal(CustomCatalogLoadStatus.Remote, result.Status);
        Assert.Equal("custom:community:helper", Assert.Single(result.Catalog.Mods).Id);
        Assert.True(File.Exists(cachePath));
        var cachedSource = await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath);
        Assert.Equal("helper", Assert.Single(cachedSource.Mods).Id);
    }

    [Fact]
    public async Task LoadWithCache_uses_last_valid_cache_when_remote_fails()
    {
        var cachePath = Path.Combine(directory, "fallback.json");
        await AtomicJsonStore.WriteAsync(cachePath, new GameCatalog { Mods = [Mod("cached")] });
        using var client = Client(_ => throw new HttpRequestException("offline"));

        var result = await CustomCatalogSource.LoadWithCacheAsync(
            "community",
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.Equal(CustomCatalogLoadStatus.Cached, result.Status);
        Assert.Equal("custom:community:cached", Assert.Single(result.Catalog.Mods).Id);
        Assert.Contains("offline", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadWithCache_invalid_remote_does_not_replace_valid_cache()
    {
        var cachePath = Path.Combine(directory, "preserved.json");
        var cached = new GameCatalog { Mods = [Mod("cached")] };
        await AtomicJsonStore.WriteAsync(cachePath, cached);
        var before = await File.ReadAllBytesAsync(cachePath);
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ bad json", Encoding.UTF8, "application/json")
        });

        var result = await CustomCatalogSource.LoadWithCacheAsync(
            "community",
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.Equal(CustomCatalogLoadStatus.Cached, result.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(cachePath));
    }

    [Fact]
    public void Namespace_allows_mods_without_granting_official_catalog_authority()
    {
        var source = new GameCatalog
        {
            Builds = [Build("override")],
            Mods =
            [
                Mod("base"),
                Mod("feature", "base", "hkmod:Satchel")
            ],
            SpeedrunTemplates =
            [
                new SpeedrunTemplate
                {
                    Id = "official-looking",
                    Name = "Untrusted",
                    BuildId = "1.5.78.11833"
                }
            ]
        };

        var namespaced = CustomCatalogSource.Namespace("community", source);

        Assert.Empty(namespaced.Builds);
        Assert.Empty(namespaced.SpeedrunTemplates);
        Assert.Equal(["custom:community:base", "custom:community:feature"], namespaced.Mods.Select(mod => mod.Id));
        Assert.All(namespaced.Mods, mod => Assert.Equal("community", mod.SourceName));
        Assert.Equal(
            ["custom:community:base", "hkmod:Satchel"],
            namespaced.Mods.Single(mod => mod.Id.EndsWith(":feature", StringComparison.Ordinal)).Dependencies);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad namespace")]
    [InlineData("../escape")]
    public void Namespace_rejects_invalid_names(string value) =>
        Assert.Throws<ArgumentException>(() => CustomCatalogSource.Namespace(value, new GameCatalog()));

    [Theory]
    [InlineData("null-dependencies")]
    [InlineData("null-authors")]
    [InlineData("null-tags")]
    [InlineData("null-integrations")]
    [InlineData("duplicate-dependencies")]
    [InlineData("duplicate-authors")]
    [InlineData("duplicate-tags")]
    [InlineData("duplicate-integrations")]
    [InlineData("insecure-repository")]
    [InlineData("insecure-issues")]
    public void Namespace_rejects_invalid_mod_metadata_before_rewriting_ids(string invalidKind)
    {
        var mod = Mod("broken");
        mod = invalidKind switch
        {
            "null-dependencies" => mod with { Dependencies = null! },
            "null-authors" => mod with { Authors = null! },
            "null-tags" => mod with { Tags = null! },
            "null-integrations" => mod with { Integrations = null! },
            "duplicate-dependencies" => mod with { Dependencies = ["shared", "SHARED"] },
            "duplicate-authors" => mod with { Authors = ["Author", "author"] },
            "duplicate-tags" => mod with { Tags = ["Utility", "utility"] },
            "duplicate-integrations" => mod with { Integrations = ["Menu", "menu"] },
            "insecure-repository" => mod with { RepositoryUrl = "http://example.invalid/repository" },
            "insecure-issues" => mod with { IssuesUrl = "ftp://example.invalid/issues" },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidKind))
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            CustomCatalogSource.Namespace("community", new GameCatalog { Mods = [mod] }));

        Assert.Contains("broken", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ModManifest Mod(string id, params string[] dependencies) => new()
    {
        Id = id,
        Name = id,
        Version = "1",
        DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api-77",
        SupportedBuildIds = ["1.5.78.11833"],
        Dependencies = dependencies
    };

    private static GameBuild Build(string id) => new()
    {
        Id = id,
        DisplayVersion = id,
        ManifestId = "1",
        ExecutableSha256 = new string('A', 64),
        GlobalGameManagersSha256 = new string('B', 64)
    };

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new StubHandler(respond));

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
