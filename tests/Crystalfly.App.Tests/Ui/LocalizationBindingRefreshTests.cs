using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed class LocalizationBindingRefreshTests
{
    private sealed class LocHolder
    {
        public LocalizationViewModel Loc { get; } = new();
    }

    public static TheoryData<string?> IndexerNotificationCandidates => new()
    {
        "Item",
        string.Empty
    };

    [AvaloniaFact]
    public void Loc_apply_switches_language_refreshes_indexer_bindings()
    {
        var holder = new LocHolder();
        var textBlock = new TextBlock
        {
            DataContext = holder,
            [!TextBlock.TextProperty] = new Binding("Loc[StatusReady]")
        };

        // Default language is Simplified Chinese.
        Assert.Equal("就绪", textBlock.Text);

        holder.Loc.Apply(UiLanguage.English);

        Assert.Equal("Ready", textBlock.Text);

        holder.Loc.Apply(UiLanguage.SimplifiedChinese);

        Assert.Equal("就绪", textBlock.Text);
    }

    private sealed class SilentProbe : INotifyPropertyChanged
    {
        private string value = "A";

        public string this[string key]
        {
            get => value;
            set => this.value = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [AvaloniaFact]
    public void Wpf_style_item_array_notification_ignored_by_avalonia_accessor()
    {
        // Direct contract check: with the indexer value swapped behind the accessor's
        // back (no notification), the CLR indexer name "Item" refreshes the binding
        // while the WPF-style "Item[]" is ignored.
        var probe = new SilentProbe();
        var textBlock = new TextBlock
        {
            DataContext = probe,
            [!TextBlock.TextProperty] = new Binding("[key]")
        };
        Assert.Equal("A", textBlock.Text);

        probe["key"] = "B";
        probe.Raise("Item[]");
        Assert.Equal("A", textBlock.Text);

        probe.Raise("Item");
        Assert.Equal("B", textBlock.Text);
    }
}
