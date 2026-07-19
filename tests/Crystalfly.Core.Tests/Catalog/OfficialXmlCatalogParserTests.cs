using Crystalfly.Core.Catalog;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class OfficialXmlCatalogParserTests
{
    private const string Namespace = "https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager";

    [Fact]
    public void ParseMods_maps_official_manifest_and_dependencies_to_protected_ids()
    {
        var xml = $$"""
            <ModLinks xmlns="{{Namespace}}">
              <Manifest>
                <Name>Example Mod</Name>
                <Description>Example</Description>
                <Version>1.2.3.4</Version>
                <Link SHA256="{{new string('A', 64)}}">https://example.invalid/example.zip</Link>
                <Dependencies><Dependency>Satchel</Dependency></Dependencies>
              </Manifest>
            </ModLinks>
            """;

        var mod = Assert.Single(OfficialXmlCatalogParser.ParseMods(xml, "modding-api-77", "1.5.78.11833"));

        Assert.Equal("hkmod:Example Mod", mod.Id);
        Assert.Equal("Example Mod", mod.Name);
        Assert.Equal("1.2.3.4", mod.Version);
        Assert.Equal("https://example.invalid/example.zip", mod.DownloadUrl);
        Assert.Equal(new string('A', 64), mod.Sha256);
        Assert.Equal("modding-api-77", mod.LoaderId);
        Assert.Equal(["1.5.78.11833"], mod.SupportedBuildIds);
        Assert.Equal(["hkmod:Satchel"], mod.Dependencies);
    }

    [Fact]
    public void ParseMods_selects_windows_asset_and_maps_market_metadata()
    {
        var xml = $$"""
            <ModLinks xmlns="{{Namespace}}">
              <Manifest>
                <Name>Source Name</Name>
                <DisplayName>Friendly Name</DisplayName>
                <Description>Example description</Description>
                <Version>2.0.0.0</Version>
                <Links>
                  <Linux SHA256="{{new string('B', 64)}}">https://example.invalid/linux.zip</Linux>
                  <Windows SHA256="{{new string('A', 64)}}">https://example.invalid/windows.zip</Windows>
                </Links>
                <Dependencies />
                <Repository>https://example.invalid/repository</Repository>
                <Issues>https://example.invalid/issues</Issues>
                <Authors><Author>Alice</Author><Author>Bob</Author></Authors>
                <Tags><Tag>Gameplay</Tag><Tag>Utility</Tag></Tags>
                <Integrations><Integration>Menu</Integration></Integrations>
              </Manifest>
            </ModLinks>
            """;

        var mod = Assert.Single(OfficialXmlCatalogParser.ParseMods(xml, "modding-api-78", "build"));

        Assert.Equal("hkmod:Source Name", mod.Id);
        Assert.Equal("Source Name", mod.Name);
        Assert.Equal("Friendly Name", mod.DisplayName);
        Assert.Equal("HK ModLinks", mod.SourceName);
        Assert.Equal("Example description", mod.Description);
        Assert.Equal("https://example.invalid/windows.zip", mod.DownloadUrl);
        Assert.Equal(["Alice", "Bob"], mod.Authors);
        Assert.Equal(["Gameplay", "Utility"], mod.Tags);
        Assert.Equal(["Menu"], mod.Integrations);
        Assert.Equal("https://example.invalid/repository", mod.RepositoryUrl);
        Assert.Equal("https://example.invalid/issues", mod.IssuesUrl);
    }

    [Fact]
    public void ParseMods_reports_mod_name_when_windows_asset_is_missing()
    {
        var xml = $$"""
            <ModLinks xmlns="{{Namespace}}">
              <Manifest>
                <Name>Broken Mod</Name>
                <Version>1.0.0.0</Version>
                <Links>
                  <Linux SHA256="{{new string('A', 64)}}">https://example.invalid/linux.zip</Linux>
                </Links>
              </Manifest>
            </ModLinks>
            """;

        var exception = Assert.Throws<InvalidDataException>(() =>
            OfficialXmlCatalogParser.ParseMods(xml, "modding-api-78", "build"));

        Assert.Contains("Broken Mod", exception.Message);
        Assert.Contains("Windows", exception.Message);
    }

    [Fact]
    public void ParseMods_rejects_insecure_metadata_url_with_mod_name()
    {
        var xml = $$"""
            <ModLinks xmlns="{{Namespace}}">
              <Manifest>
                <Name>Broken Metadata</Name>
                <Version>1.0.0.0</Version>
                <Link SHA256="{{new string('A', 64)}}">https://example.invalid/mod.zip</Link>
                <Repository>http://example.invalid/repository</Repository>
              </Manifest>
            </ModLinks>
            """;

        var exception = Assert.Throws<InvalidDataException>(() =>
            OfficialXmlCatalogParser.ParseMods(xml, "modding-api-78", "build"));

        Assert.Contains("Broken Metadata", exception.Message);
        Assert.Contains("HTTPS", exception.Message);
    }

    [Fact]
    public void ParseApi_selects_windows_asset_and_file_inventory()
    {
        var xml = $$"""
            <ApiLinks xmlns="{{Namespace}}">
              <Manifest>
                <Version>77</Version>
                <Links>
                  <Windows SHA256="{{new string('B', 64)}}">https://example.invalid/api.zip</Windows>
                </Links>
                <Files><File>Assembly-CSharp.dll</File><File>Mods/</File></Files>
              </Manifest>
            </ApiLinks>
            """;

        var loader = OfficialXmlCatalogParser.ParseApi(xml, "1.5.78.11833");

        Assert.Equal("modding-api-77", loader.Id);
        Assert.Equal("77", loader.Version);
        Assert.Equal("https://example.invalid/api.zip", loader.DownloadUrl);
        Assert.Equal(new string('B', 64), loader.Sha256);
        Assert.Equal(["1.5.78.11833"], loader.SupportedBuildIds);
        Assert.Equal(["Assembly-CSharp.dll", "Mods/"], loader.ManagedFiles);
    }
}
