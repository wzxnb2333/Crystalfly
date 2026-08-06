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

    [AvaloniaTheory]
    [MemberData(nameof(IndexerNotificationCandidates))]
    public void Indexer_binding_refreshes_when_loc_raises_indexer_notification(string? propertyName)
    {
        // Documents the Avalonia INPC contract the fix relies on: indexer bindings
        // refresh when the bound instance raises PropertyChanged with the CLR indexer
        // name ("Item") or an empty property name, NOT the WPF-style "Item[]".
        var holder = new LocHolder();
        var textBlock = new TextBlock
        {
            DataContext = holder,
            [!TextBlock.TextProperty] = new Binding("Loc[StatusReady]")
        };

        holder.Loc.Apply(UiLanguage.English);
        Assert.Equal("Ready", textBlock.Text);

        holder.Loc.RaiseTestNotification(propertyName);

        Assert.Equal("Ready", textBlock.Text);
    }

    [AvaloniaFact]
    public void Wpf_style_item_array_notification_does_not_refresh_indexer_bindings()
    {
        // Regression guard for the original bug: "Item[]" (WPF convention) is ignored by
        // Avalonia's INPC accessor, which is why the old Apply never refreshed anything.
        var holder = new LocHolder();
        var textBlock = new TextBlock
        {
            DataContext = holder,
            [!TextBlock.TextProperty] = new Binding("Loc[StatusReady]")
        };

        holder.Loc.Apply(UiLanguage.English);
        Assert.Equal("Ready", textBlock.Text);

        holder.Loc.RaiseTestNotification("Item[]");

        Assert.Equal("Ready", textBlock.Text);
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
