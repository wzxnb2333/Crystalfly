using System.Reflection;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class ModMarketParityViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.MarketParity", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Clearing_market_filters_restores_results()
    {
        await using var model = new MainViewModel(root);
        typeof(MainViewModel).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(model, new GameCatalog { Mods = [Mod("one"), Mod("two")] });
        typeof(MainViewModel).GetMethod("RebuildMarketCatalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(model, null);
        model.MarketSearchText = "one";
        Assert.Equal("one", Assert.Single(model.VisibleMarketDisplayMods).Id);
        model.ClearMarketFiltersCommand.Execute(null);
        Assert.Equal(2, model.VisibleMarketDisplayMods.Count);
        Assert.Equal("", model.MarketSearchText);
        Assert.False(model.IsBusy);
    }

    private static ModManifest Mod(string id) => new()
    {
        Id = id, Name = id, Version = "1.0.0", DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('0', 64), LoaderId = "loader"
    };
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
