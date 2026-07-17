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
