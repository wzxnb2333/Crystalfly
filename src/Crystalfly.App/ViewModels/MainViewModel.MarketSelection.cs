using CommunityToolkit.Mvvm.Input;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private readonly HashSet<string> selectedMarketMods = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> marketInstallModIds = [];
    public int SelectedMarketModCount => selectedMarketMods.Count;
    public bool HasSelectedMarketMods => SelectedMarketModCount > 0;
    public string SelectedMarketModsText => string.Format(Loc.Culture, Loc["SelectedModCount"], SelectedMarketModCount);
    internal IReadOnlyList<string> GetMarketInstallModIds() => SelectedMarketMod is { } selected
        ? [selected.Id] : selectedMarketMods.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    internal string MarketInstallName => string.Join(" + ", marketInstallModIds.Select(id =>
        MarketDisplayMods.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.PrimaryName ?? id));

    private void SetMarketModSelected(string id, bool selected)
    {
        if (selected ? !selectedMarketMods.Add(id) : !selectedMarketMods.Remove(id)) return;
        foreach (var item in MarketDisplayMods.Where(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            item.IsSelected = selected;
        OnPropertyChanged(nameof(SelectedMarketModCount));
        OnPropertyChanged(nameof(HasSelectedMarketMods));
        OnPropertyChanged(nameof(SelectedMarketModsText));
    }

    [RelayCommand]
    private void ClearMarketSelection()
    {
        foreach (var id in selectedMarketMods.ToArray()) SetMarketModSelected(id, false);
    }
}
