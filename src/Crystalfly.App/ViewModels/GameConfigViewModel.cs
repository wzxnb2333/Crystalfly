using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.ViewModels;

/// <summary>
/// A single editable row in the advanced key/value view of the configuration editor.
/// </summary>
public sealed partial class ConfigEntryViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Section { get; set; }

    [ObservableProperty]
    public partial string Key { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }

    public ConfigEntryViewModel(string section, string key, string value)
    {
        Section = section;
        Key = key;
        Value = value;
    }
}

/// <summary>
/// Edits an instance's <c>AppConfig.ini</c>. Known accessibility settings are exposed as typed
/// sliders while every section/key/value is also available in an advanced editable list. The
/// underlying <see cref="IniDocument"/> is the source of truth: slider changes write through to
/// the document immediately and value edits in the advanced list write through as well, so
/// unknown fields are preserved and the two editors never conflict.
/// </summary>
public sealed partial class GameConfigViewModel : ViewModelBase
{
    private readonly string configPath;
    private IniDocument document = new();
    private bool suppressDirty;

    /// <summary>Absolute path of the <c>AppConfig.ini</c> being edited.</summary>
    public string ConfigPath => configPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    /// <summary>Raised after a successful save so the host can show a confirmation.</summary>
    public event Action? Saved;

    public ObservableCollection<ConfigEntryViewModel> Entries { get; } = [];

    public bool CanSave => IsDirty;

    public GameConfigViewModel(string configPath)
    {
        this.configPath = configPath;
    }

    public double ReducedCameraShake
    {
        get => AppConfigService.GetDouble(
            document,
            AppConfigService.AccessibilitySection,
            AppConfigService.ReducedCameraShakeKey,
            0);
        set => SetAccessibility(
            AppConfigService.ReducedCameraShakeKey,
            nameof(ReducedCameraShake),
            value);
    }

    public double ReducedControllerRumble
    {
        get => AppConfigService.GetDouble(
            document,
            AppConfigService.AccessibilitySection,
            AppConfigService.ReducedControllerRumbleKey,
            0);
        set => SetAccessibility(
            AppConfigService.ReducedControllerRumbleKey,
            nameof(ReducedControllerRumble),
            value);
    }

    /// <summary>Loads (or reloads) the document from disk and rebuilds the editors.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        document = await AppConfigService.LoadAsync(configPath, cancellationToken);
        suppressDirty = true;
        try
        {
            OnPropertyChanged(nameof(ReducedCameraShake));
            OnPropertyChanged(nameof(ReducedControllerRumble));
            RebuildEntries();
            IsDirty = false;
            IsLoaded = true;
        }
        finally
        {
            suppressDirty = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        RebuildDocumentFromEntries();
        await AppConfigService.SaveAsync(configPath, document, cancellationToken);
        IsDirty = false;
        Saved?.Invoke();
    }

    [RelayCommand]
    private async Task ResetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    [RelayCommand]
    private void AddEntry()
    {
        var entry = new ConfigEntryViewModel("Section", "Key", "Value");
        entry.PropertyChanged += OnEntryPropertyChanged;
        Entries.Add(entry);
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveEntry(ConfigEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.PropertyChanged -= OnEntryPropertyChanged;
        Entries.Remove(entry);
        MarkDirty();
    }

    private void SetAccessibility(string key, string propertyName, double value)
    {
        var clamped = Math.Clamp(value, 0, 1);
        AppConfigService.SetDouble(document, AppConfigService.AccessibilitySection, key, clamped);
        OnPropertyChanged(propertyName);
        RefreshEntryValue(AppConfigService.AccessibilitySection, key);
        MarkDirty();
    }

    private void RebuildEntries()
    {
        foreach (var entry in Entries)
        {
            entry.PropertyChanged -= OnEntryPropertyChanged;
        }

        Entries.Clear();
        foreach (var section in document.Sections)
        {
            foreach (var keyValue in section.KeyValues)
            {
                var entry = new ConfigEntryViewModel(
                    section.Name,
                    keyValue.Key ?? string.Empty,
                    keyValue.Value);
                entry.PropertyChanged += OnEntryPropertyChanged;
                Entries.Add(entry);
            }
        }
    }

    private void OnEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ConfigEntryViewModel entry)
        {
            return;
        }

        if (e.PropertyName == nameof(ConfigEntryViewModel.Value))
        {
            // Write the value through to the document immediately so it stays the source of truth.
            if (!string.IsNullOrWhiteSpace(entry.Key))
            {
                document.SetValue(entry.Section, entry.Key, entry.Value);
                RefreshAccessibilityProperties();
            }
        }

        MarkDirty();
    }

    private void RefreshEntryValue(string section, string key)
    {
        var value = document.GetValue(section, key) ?? string.Empty;
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Section, section, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                entry.Value = value;
                return;
            }
        }

        // The key was not yet visible (e.g. first edit of a default value); surface it.
        var added = new ConfigEntryViewModel(section, key, value);
        added.PropertyChanged += OnEntryPropertyChanged;
        Entries.Add(added);
    }

    private void RebuildDocumentFromEntries()
    {
        document = new IniDocument();
        foreach (var entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            document.SetValue(entry.Section, entry.Key, entry.Value);
        }

        RefreshAccessibilityProperties();
    }

    private void RefreshAccessibilityProperties()
    {
        OnPropertyChanged(nameof(ReducedCameraShake));
        OnPropertyChanged(nameof(ReducedControllerRumble));
    }

    private void MarkDirty()
    {
        if (!suppressDirty)
        {
            IsDirty = true;
        }
    }
}
