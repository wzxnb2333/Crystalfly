using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class InstalledModItemViewModelTests
{
    [Fact]
    public void Reports_catalog_update_and_selection_changes()
    {
        var changes = 0;
        var item = new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            Manifest("debugmod", "1.1.0"),
            () => changes++);

        Assert.True(item.HasUpdate);
        Assert.True(item.IsEnabled);
        Assert.False(item.IsLocal);

        item.IsSelected = true;

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Local_mod_never_claims_catalog_update()
    {
        var item = new InstalledModItemViewModel(
            Receipt("local-test", "1.0.0", enabled: false, isLocal: true),
            Manifest("local-test", "2.0.0"),
            static () => { });

        Assert.False(item.HasUpdate);
        Assert.False(item.IsEnabled);
        Assert.True(item.IsLocal);
    }

    [Fact]
    public void Chinese_market_projection_drives_installed_name_tags_and_search()
    {
        var manifest = Manifest("debugmod", "1.1.0") with
        {
            Description = "Official debug tools",
            Tags = ["Utility"]
        };
        var display = new MarketModItemViewModel(
            manifest,
            new ModTranslationEntry
            {
                Id = "hkmod:Debug Mod",
                DisplayName = "调试模组",
                Description = "用于调试与练习"
            },
            new Dictionary<string, string> { ["Utility"] = "工具" },
            chinese: true);
        var item = new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            manifest,
            static () => { },
            display);

        Assert.Equal("调试模组", item.PrimaryName);
        Assert.Equal("Debug Mod", item.SecondaryName);
        Assert.True(item.HasSecondaryName);
        Assert.Equal("用于调试与练习", item.Description);
        Assert.Equal("工具", Assert.Single(item.Tags).Name);
        Assert.True(item.Matches("调试", ModStatusFilter.All));
        Assert.True(item.Matches("Official debug", ModStatusFilter.All));
        Assert.True(item.Matches("工具", ModStatusFilter.All));
        Assert.True(item.Matches("debugmod", ModStatusFilter.All));
    }

    [Fact]
    public void Unknown_local_mod_falls_back_to_receipt_name()
    {
        var item = new InstalledModItemViewModel(
            Receipt("local-test", "local", enabled: false, isLocal: true) with
            {
                Name = "Local Helper"
            },
            null,
            static () => { });

        Assert.Equal("Local Helper", item.PrimaryName);
        Assert.False(item.HasSecondaryName);
        Assert.True(item.Matches("Local Helper", ModStatusFilter.Local));
    }

    [Theory]
    [InlineData("debug", ModStatusFilter.All, true)]
    [InlineData("missing", ModStatusFilter.All, false)]
    [InlineData("", ModStatusFilter.Enabled, true)]
    [InlineData("", ModStatusFilter.Disabled, false)]
    [InlineData("", ModStatusFilter.Updates, true)]
    [InlineData("", ModStatusFilter.Local, false)]
    public void Matches_search_and_status_filter(string search, ModStatusFilter filter, bool expected)
    {
        var item = new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            Manifest("debugmod", "1.1.0"),
            static () => { });

        Assert.Equal(expected, item.Matches(search, filter));
    }

    private static InstalledModReceipt Receipt(
        string id,
        string version,
        bool enabled,
        bool isLocal = false) => new()
    {
        Id = id,
        Name = "Debug Mod",
        Version = version,
        LoaderId = "modding-api",
        InstallRoot = "Mods/DebugMod",
        Enabled = enabled,
        IsLocal = isLocal
    };

    private static ModManifest Manifest(string id, string version) => new()
    {
        Id = id,
        Name = "Debug Mod",
        Version = version,
        DownloadUrl = "https://example.invalid/debugmod.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api"
    };
}
