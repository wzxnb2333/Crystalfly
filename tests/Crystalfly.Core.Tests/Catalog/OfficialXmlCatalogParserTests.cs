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
