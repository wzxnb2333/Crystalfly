using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModCatalogMatcherTests
{
    [Fact]
    public void Match_returns_single_candidate_when_name_and_loader_agree()
    {
        var external = External("DebugMod", "modding-api-77");
        var catalog = new[]
        {
            Manifest("debugmod-1", "DebugMod", "modding-api-77", ["1.5.78.11833"]),
            Manifest("other-1", "OtherMod", "modding-api-77", ["1.5.78.11833"])
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.78.11833", catalog);

        Assert.Single(candidates);
        Assert.Equal("debugmod-1", candidates[0].Id);
    }

    [Fact]
    public void Match_returns_multiple_candidates_when_several_names_agree()
    {
        var external = External("DebugMod", "modding-api-77");
        var catalog = new[]
        {
            Manifest("debugmod-v1", "DebugMod", "modding-api-77", ["1.5.78.11833"]),
            Manifest("debugmod-v2", "DebugMod", "modding-api-77", ["1.5.78.11833"])
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.78.11833", catalog);

        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void Match_filters_candidates_by_loader_family()
    {
        var external = External("DebugMod", "bepinex-1.0.0.0");
        var catalog = new[]
        {
            Manifest("debugmod-api", "DebugMod", "modding-api-77", ["1.5.78.11833"]),
            Manifest("debugmod-bep", "DebugMod", "bepinex-1.0.0.0", ["1.5.78.11833"])
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.78.11833", catalog);

        Assert.Single(candidates);
        Assert.Equal("debugmod-bep", candidates[0].Id);
    }

    [Fact]
    public void Match_filters_candidates_by_supported_build()
    {
        var external = External("DebugMod", "modding-api-77");
        var catalog = new[]
        {
            Manifest("debugmod-old", "DebugMod", "modding-api-77", ["1.2.2.1"]),
            Manifest("debugmod-ok", "DebugMod", "modding-api-77", ["1.5.78.11833"])
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.78.11833", catalog);

        Assert.Single(candidates);
        Assert.Equal("debugmod-ok", candidates[0].Id);
    }

    [Fact]
    public void Match_accepts_legacy_official_mod_for_the_latest_build()
    {
        var external = External("DebugMod", "modding-api-78");
        var catalog = new[]
        {
            LegacyOfficialManifest("hkmod:DebugMod")
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.12620.0", catalog);

        Assert.Single(candidates);
        Assert.Equal("hkmod:DebugMod", candidates[0].Id);
    }

    [Fact]
    public void Match_returns_empty_when_nothing_agrees()
    {
        var external = External("UnknownMod", "modding-api-77");
        var catalog = new[]
        {
            Manifest("debugmod-1", "DebugMod", "modding-api-77", ["1.5.78.11833"])
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.78.11833", catalog);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Match_matches_when_a_flat_file_name_agrees()
    {
        var external = new ModDiscoveryEntry
        {
            Id = "external-abc",
            Name = "SomeDir",
            LoaderId = "modding-api-77",
            InstallRoot = "hollow_knight_Data/Managed/Mods/SomeDir",
            Enabled = true,
            Ownership = ModOwnership.External,
            Files = ["hollow_knight_Data/Managed/Mods/SomeDir/DebugMod.dll"],
            EntryFiles = ["hollow_knight_Data/Managed/Mods/SomeDir/DebugMod.dll"]
        };
        var catalog = new[]
        {
            Manifest("debugmod-1", "DebugMod", "modding-api-77", ["1.5.78.11833"],
                flatFiles: ["DebugMod.dll"])
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.78.11833", catalog);

        Assert.Single(candidates);
        Assert.Equal("debugmod-1", candidates[0].Id);
    }

    [Fact]
    public void Match_accepts_the_external_family_loader_id()
    {
        var external = External("DebugMod", "modding-api-external");
        var catalog = new[]
        {
            Manifest("debugmod-1", "DebugMod", "modding-api-77", ["1.5.78.11833"])
        };

        var candidates = ModCatalogMatcher.Match(external, "1.5.78.11833", catalog);

        Assert.Single(candidates);
        Assert.Equal("debugmod-1", candidates[0].Id);
    }

    private static ModDiscoveryEntry External(string name, string loaderId) => new()
    {
        Id = $"external-{name}",
        Name = name,
        LoaderId = loaderId,
        InstallRoot = "hollow_knight_Data/Managed/Mods",
        Enabled = true,
        Ownership = ModOwnership.External,
        Files = [$"hollow_knight_Data/Managed/Mods/{name}.dll"],
        EntryFiles = [$"hollow_knight_Data/Managed/Mods/{name}.dll"]
    };

    private static ModManifest Manifest(
        string id,
        string name,
        string loaderId,
        IReadOnlyList<string> builds,
        IReadOnlyList<string>? flatFiles = null) => new()
    {
        Id = id,
        Name = name,
        Version = "1.0.0",
        DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('A', 64),
        LoaderId = loaderId,
        SupportedBuildIds = builds,
        FlatFiles = flatFiles ?? []
    };

    private static ModManifest LegacyOfficialManifest(string id) => new()
    {
        Id = id,
        Name = id["hkmod:".Length..],
        Version = "1.0.0",
        DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('B', 64),
        LoaderId = ModCatalogCompatibility.LegacyLoaderId,
        SupportedBuildIds = [ModCatalogCompatibility.LegacyBuildId],
        SourceName = "HK ModLinks"
    };
}
