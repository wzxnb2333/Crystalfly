using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Json.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class EmbeddedCatalogTests
{
    private static readonly Lazy<JsonSchema> CatalogSchema = new(() =>
        JsonSchema.FromFile(Path.Combine(FindRepositoryRoot(), "catalog", "catalog.v1.schema.json")));

    private static readonly Lazy<JsonSchema> ModTranslationSchema = new(() =>
        JsonSchema.FromFile(Path.Combine(
            FindRepositoryRoot(),
            "catalog",
            "mod-translations.zh-CN.v1.schema.json")));

    [Fact]
    public void Public_mod_translation_catalog_matches_schema_and_is_presentation_only()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "catalog",
            "mod-translations.zh-CN.v1.json");

        Assert.True(File.Exists(path));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        EvaluationResults validation = ModTranslationSchema.Value.Evaluate(document.RootElement);
        Assert.True(validation.IsValid, validation.ToString());

        JsonElement root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("zh-CN", root.GetProperty("language").GetString());
        Assert.Equal(10, root.GetProperty("tagNames").EnumerateObject().Count());

        var mods = root.GetProperty("mods").EnumerateArray().ToArray();
        Assert.Equal(649, mods.Length);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] installFields =
        [
            "version",
            "dependencies",
            "downloadUrl",
            "sha256",
            "sizeBytes",
            "loaderId",
            "supportedBuildIds",
            "flatFiles"
        ];

        foreach (JsonElement mod in mods)
        {
            string id = mod.GetProperty("id").GetString()!;
            Assert.StartsWith("hkmod:", id, StringComparison.OrdinalIgnoreCase);
            Assert.True(ids.Add(id), $"Duplicate translation ID: {id}");
            Assert.DoesNotContain(
                mod.EnumerateObject(),
                property => installFields.Contains(property.Name, StringComparer.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain(ids, id =>
            string.Equals(id, "hkmod:Another Location", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(mods, mod => mod.EnumerateObject().Any(property =>
            property.Value.ValueKind == JsonValueKind.String
            && property.Value.GetString()!.Contains("Another Location", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Embedded_mod_translation_catalog_loads_expected_entries()
    {
        var catalog = EmbeddedModTranslationCatalog.Load();

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal("zh-CN", catalog.Language);
        Assert.Equal(649, catalog.Mods.Count);
        Assert.Equal(10, catalog.TagNames.Count);
        Assert.All(catalog.Mods, mod => Assert.StartsWith(
            "hkmod:",
            mod.Id,
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            catalog.Mods.Count,
            catalog.Mods.Select(mod => mod.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var noskGod = catalog.Mods.Single(mod => mod.Id == "hkmod:Nosk God");
        Assert.Equal("神之诺斯克", noskGod.DisplayName);
        Assert.Null(noskGod.Description);
        Assert.DoesNotContain(
            catalog.Mods,
            mod => string.Equals(mod.Id, "hkmod:Another Location", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_returns_verified_official_fallback_data()
    {
        var catalog = EmbeddedCatalog.Load();

        var build1221 = catalog.Builds.Single(build => build.Id == "1.2.2.1");
        Assert.Equal("648876203478229944", build1221.ManifestId);
        Assert.Equal("1434454FCB5A1F4FFF329EA56182A7C7DA1581DC0F4B6DCEF8585E739F416217", build1221.ExecutableSha256);
        Assert.Null(build1221.UnityPlayerSha256);
        Assert.Equal("58BC88B74D6F05B9E00D7E1F2BC9B3BA6E9FC51F75C6915DF10BF10B90CDD749", build1221.GlobalGameManagersSha256);

        Assert.Equal(
            "3AE7AD0A4658406A056D4D0C75E6787A553ABE3D0ADBCA510BFE8BBEC3111C67",
            catalog.Builds.Single(build => build.Id == "1.4.3.2").UnityPlayerSha256);
        Assert.Equal(
            "D97E92A7640B10580B4E60139EACF01828F74BAAEF53F75E08B9FDD6193FBE5E",
            catalog.Builds.Single(build => build.Id == "1.5.78.11833").UnityPlayerSha256);

        var latest = catalog.Builds.Single(build => build.Id == "1.5.12620.0");
        Assert.Equal("1.5.12620.0", latest.DisplayVersion);
        Assert.Equal("257781644874438846", latest.ManifestId);
        Assert.Equal("8F2D601F8D3C7F4D29D80BA786C0BE873102BB7E6041EB03964A90B99724D90B", latest.UnityPlayerSha256);
        Assert.Equal("1.5.12620.0", catalog.Channels.Single(channel => channel.Name == "latest").BuildId);

        var bepinex = catalog.Loaders.Single(loader => loader.Id == "bepinex-5.4.23.4");
        Assert.Equal(638940, bepinex.SizeBytes);
        Assert.Equal("F881201B79DA03E513BF97CDF39607FFA7F9E0D31A519B1AEECA8EB60F8309E7", bepinex.Sha256);
        Assert.Equal(["1.5.12620.0"], bepinex.SupportedBuildIds);

        var moddingApi37 = catalog.Loaders.Single(loader => loader.Id == "modding-api-37");
        Assert.Equal(932719, moddingApi37.SizeBytes);
        Assert.Equal("ECFF6C73C40194E9D8118C14B9ADE6862DB91E3949D8150027A86FD83BF290F7", moddingApi37.Sha256);

        var moddingApi77 = catalog.Loaders.Single(loader => loader.Id == "modding-api-77");
        Assert.Equal("BC9F0DB3D0916B05CD5A2420BB602FB1B239CE3FF6C289FD84BFFB682FB8F1D6", moddingApi77.Sha256);

        var moddingApi78 = catalog.Loaders.Single(loader => loader.Id == "modding-api-78");
        Assert.Equal(4982043, moddingApi78.SizeBytes);
        Assert.Equal("5B5EBDDA651171E3C5EA6F13FB68FFE2D1B5F8B97A9D6FDE0EED3EA529418748", moddingApi78.Sha256);
        Assert.Equal(["1.5.12620.0"], moddingApi78.SupportedBuildIds);

        var screenShake = catalog.SpeedrunAssets.Single(asset => asset.Id == "screen-shake-modifier-1221");
        Assert.Equal(2832384, screenShake.SizeBytes);
        Assert.Equal("EF25E8E55765230B9D12554355BB069189DD0AA0AEDAB684084DAD297D5391FA", screenShake.Sha256);

        var race1221 = catalog.SpeedrunTemplates.Single(template => template.Id == "race-1221");
        Assert.False(race1221.LoadNormaliserAvailable);
        Assert.Equal(["screen-shake-modifier-1221"], race1221.RequiredAssetIds);

        var single1221 = catalog.SpeedrunTemplates.Single(template => template.Id == "single-run-1221");
        Assert.Equal(["screen-shake-modifier-1221"], single1221.RequiredAssetIds);

        var single1578 = catalog.SpeedrunTemplates.Single(template => template.Id == "single-run-1578");
        Assert.Empty(single1578.RequiredAssetIds);
        Assert.False(single1578.RequiresLoadNormaliserSelection);

        var race1578 = catalog.SpeedrunTemplates.Single(template => template.Id == "race-1578");
        Assert.Equal(["load-normaliser-1.1"], race1578.RequiredAssetIds);
        Assert.True(race1578.RequiresLoadNormaliserSelection);
        Assert.Equal([1, 2, 3, 5], race1578.AllowedLoadNormaliserSeconds);
        Assert.All(catalog.SpeedrunTemplates, template =>
        {
            Assert.False(template.IsOfficial);
            Assert.Equal("unverified-2026-07-17", template.RulesRevision);
            Assert.StartsWith("unverified-", template.FileManifestId, StringComparison.Ordinal);
        });
        Assert.Equal(4, catalog.SpeedrunTemplates.Count);
    }

    [Fact]
    public void Public_catalog_matches_the_embedded_fallback_and_schema_is_present()
    {
        string repositoryRoot = FindRepositoryRoot();
        string publicCatalogPath = Path.Combine(repositoryRoot, "catalog", "catalog.v1.json");
        string schemaPath = Path.Combine(repositoryRoot, "catalog", "catalog.v1.schema.json");
        string embeddedCatalogPath = Path.Combine(
            repositoryRoot,
            "src",
            "Crystalfly.Core",
            "Data",
            "official-catalog.json");

        Assert.True(File.Exists(publicCatalogPath));
        Assert.True(File.Exists(schemaPath));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(File.ReadAllText(publicCatalogPath)),
            JsonNode.Parse(File.ReadAllText(embeddedCatalogPath))));

        using JsonDocument publicCatalog = JsonDocument.Parse(File.ReadAllText(publicCatalogPath));
        EvaluationResults validation = CatalogSchema.Value.Evaluate(publicCatalog.RootElement);
        Assert.True(validation.IsValid, validation.ToString());

        JsonNode schema = JsonNode.Parse(File.ReadAllText(schemaPath))!;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", schema["$schema"]!.GetValue<string>());
        Assert.Equal("https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/catalog.v1.schema.json", schema["$id"]!.GetValue<string>());
        Assert.NotNull(schema["properties"]?["speedrunFileManifests"]);
    }

    [Fact]
    public void Catalog_contains_all_supported_loader_and_debugmod_release_assets()
    {
        var catalog = EmbeddedCatalog.Load();

        Assert.Equal(
            ["modding-api-37", "modding-api-60", "modding-api-77", "modding-api-78"],
            catalog.Loaders
                .Where(loader => loader.Id.StartsWith("modding-api-", StringComparison.Ordinal))
                .Select(loader => loader.Id)
                .Order(StringComparer.Ordinal));
        Assert.All(catalog.Loaders, loader => Assert.True(loader.SizeBytes > 0));

        Assert.Equal(
            [
                "debugmod-legacy-1.2.2.1",
                "debugmod-legacy-1.4.3.2",
                "debugmod-legacy-1.5.78",
                "debugmod-unity6-latest"
            ],
            catalog.Mods
                .Where(mod => mod.Id.StartsWith("debugmod-", StringComparison.Ordinal))
                .Select(mod => mod.Id)
                .Order(StringComparer.Ordinal));
        Assert.All(
            catalog.Mods.Where(mod => mod.Id.StartsWith("debugmod-", StringComparison.Ordinal)),
            mod => Assert.True(mod.SizeBytes > 0));
    }

    [Fact]
    public void Schema_accepts_namespaced_mod_ids_without_relaxing_other_identifiers()
    {
        string repositoryRoot = FindRepositoryRoot();
        JsonSchema schema = CatalogSchema.Value;
        var catalog = new GameCatalog
        {
            Mods =
            [
                new ModManifest
                {
                    Id = "custom:community:feature",
                    Name = "Feature",
                    Version = "1",
                    DownloadUrl = "https://example.invalid/feature.zip",
                    SizeBytes = 1,
                    Sha256 = new string('A', 64),
                    LoaderId = "modding-api-77",
                    SupportedBuildIds = ["1.5.78.11833"],
                    Dependencies = ["custom:community:base", "hkmod:Satchel"]
                }
            ]
        };

        EvaluationResults namespaced = schema.Evaluate(
            JsonSerializer.SerializeToElement(catalog, CrystalflyJson.Options));
        EvaluationResults invalidBuild = schema.Evaluate(JsonSerializer.SerializeToElement(catalog with
        {
            Builds =
            [
                new GameBuild
                {
                    Id = "custom:community:build",
                    DisplayVersion = "Build",
                    DepotId = 367521,
                    ManifestId = "1",
                    ExecutableSha256 = new string('B', 64),
                    GlobalGameManagersSha256 = new string('C', 64)
                }
            ]
        }, CrystalflyJson.Options));

        Assert.True(namespaced.IsValid, namespaced.ToString());
        Assert.False(invalidBuild.IsValid);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Crystalfly repository root was not found.");
    }
}
