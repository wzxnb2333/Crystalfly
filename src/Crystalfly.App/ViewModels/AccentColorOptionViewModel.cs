using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Crystalfly.App.ViewModels;

public sealed partial class AccentColorOptionViewModel(
    string name,
    string hex,
    bool isCustom,
    bool isSelected) : ViewModelBase
{
    public string Name { get; } = name;

    public string Hex { get; } = hex;

    public bool IsCustom { get; } = isCustom;

    public IBrush SwatchBrush { get; } = new SolidColorBrush(Color.Parse(hex));

    internal void UpdateCustomColor(string accentColor)
    {
        if (IsCustom && SwatchBrush is SolidColorBrush brush)
        {
            brush.Color = Color.Parse(accentColor);
        }
    }

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
