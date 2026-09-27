using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.App.ViewModels;

public sealed partial class SaveEntryViewModel : ObservableObject
{
    private LocalizationViewModel localization = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(Description))]
    [NotifyPropertyChangedFor(nameof(HasLocalizedName))]
    [NotifyPropertyChangedFor(nameof(FieldToolTip))]
    public partial string Path { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BooleanValue))]
    [NotifyPropertyChangedFor(nameof(BooleanDisplayValue))]
    public partial string Value { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindDisplayName))]
    [NotifyPropertyChangedFor(nameof(IsBoolean))]
    public partial string Kind { get; set; }

    public string DisplayName => IsChinese ? SaveFieldLocalization.Find(Path)?.Name ?? Path : Path;
    public string Description => IsChinese
        ? SaveFieldLocalization.Find(Path)?.Description ?? localization["SaveFieldUntranslated"]
        : localization["SaveFieldRawHint"];
    public bool HasLocalizedName => DisplayName != Path;
    public string FieldToolTip => $"{DisplayName}\n{Description}\n{Path}";
    public string KindDisplayName => localization[Kind switch
    {
        SaveEntry.KindBoolean => "SaveTypeBoolean",
        SaveEntry.KindNumber => "SaveTypeNumber",
        SaveEntry.KindString => "SaveTypeString",
        SaveEntry.KindNull => "SaveTypeNull",
        _ => "SaveTypeUnknown"
    }];
    public bool IsBoolean => Kind == SaveEntry.KindBoolean;
    public bool BooleanValue
    {
        get => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (IsBoolean && BooleanValue != value)
            {
                Value = value ? "true" : "false";
            }
        }
    }
    public string BooleanDisplayValue => localization[BooleanValue ? "SaveBooleanTrue" : "SaveBooleanFalse"];
    private bool IsChinese => localization.Culture.TwoLetterISOLanguageName == "zh";

    public SaveEntryViewModel(SaveEntry entry)
    {
        Path = entry.Path;
        Value = entry.Value;
        Kind = entry.Kind;
    }

    public SaveEntry ToEntry() => new(Path, Value, Kind);

    internal void RefreshLocalization(LocalizationViewModel value)
    {
        localization = value;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(HasLocalizedName));
        OnPropertyChanged(nameof(FieldToolTip));
        OnPropertyChanged(nameof(KindDisplayName));
        OnPropertyChanged(nameof(BooleanDisplayValue));
    }
}

public sealed partial class SaveEditorViewModel : ViewModelBase
{
    private readonly NamedSnapshotService snapshotService;
    private readonly string instanceId;
    private readonly string? snapshotId;
    private string originalJson = string.Empty;
    private string currentSlot = string.Empty;
    private int slotLoadVersion;
    private LocalizationViewModel localization;

    [ObservableProperty]
    public partial IReadOnlyList<SaveEntryViewModel> Entries { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SaveEntryViewModel> VisibleEntries { get; set; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public bool HasNoSearchResults => IsLoaded && Entries.Count > 0 && VisibleEntries.Count == 0;
    public ObservableCollection<string> Slots { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string SourceLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedSlot { get; set; }

    public bool CanSave => IsLoaded && IsDirty;

    public event Action<Exception?>? OperationErrorChanged;

    public SaveEditorViewModel(
        NamedSnapshotService snapshotService,
        string instanceId,
        string? snapshotId,
        string sourceLabel,
        LocalizationViewModel? localization = null)
    {
        this.snapshotService = snapshotService;
        this.instanceId = instanceId;
        this.snapshotId = snapshotId;
        SourceLabel = sourceLabel;
        this.localization = localization ?? new LocalizationViewModel();
    }

    public void RefreshLocalization(LocalizationViewModel value)
    {
        localization = value;
        foreach (var entry in Entries)
        {
            entry.RefreshLocalization(value);
        }
        RefreshVisibleEntries();
    }

    partial void OnEntriesChanged(IReadOnlyList<SaveEntryViewModel> value)
    {
        foreach (var entry in value)
        {
            entry.RefreshLocalization(localization);
        }
        RefreshVisibleEntries();
    }

    partial void OnSearchTextChanged(string value) => RefreshVisibleEntries();

    partial void OnIsLoadedChanged(bool value) => OnPropertyChanged(nameof(HasNoSearchResults));

    private void RefreshVisibleEntries()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        VisibleEntries = query.Length == 0
            ? Entries
            : Entries.Where(entry => entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        OnPropertyChanged(nameof(HasNoSearchResults));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoaded = false;
        var service = snapshotService;
        var instId = instanceId;
        var snapId = snapshotId;
        var slots = await Task.Run(
            () => service.ListSaveSlotsAsync(instId, snapId, cancellationToken).GetAwaiter().GetResult(),
            cancellationToken);
        Slots.Clear();
        foreach (var slot in slots)
        {
            Slots.Add(slot);
        }

        if (Slots.Count > 0)
        {
            SelectedSlot = Slots[0];
            await LoadSlotAsync(Slots[0], cancellationToken);
            return;
        }

        SelectedSlot = null;
        currentSlot = string.Empty;
        Entries = [];
        IsDirty = false;
        IsLoaded = true;
    }

    public async Task SelectSlotAsync(string slot, CancellationToken cancellationToken = default)
    {
        if (string.Equals(slot, currentSlot, StringComparison.OrdinalIgnoreCase))
        {
            slotLoadVersion++;
            IsLoaded = true;
            return;
        }

        await LoadSlotAsync(slot, cancellationToken);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanSave)
        {
            return;
        }
        await RunCommandAsync(async () =>
        {
            var version = slotLoadVersion;
            var entries = Entries.Select(entry => entry.ToEntry()).ToArray();
            var json = SaveGameEditor.Rebuild(originalJson, entries);
            await snapshotService.UpdateSaveAsync(instanceId, snapshotId, currentSlot, json, cancellationToken);
            if (version == slotLoadVersion)
            {
                originalJson = json;
                IsDirty = !Entries.Select(entry => entry.ToEntry()).SequenceEqual(entries);
            }
        }, cancellationToken);
    }

    [RelayCommand]
    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(currentSlot))
        {
            await RunCommandAsync(() => LoadSlotAsync(currentSlot, cancellationToken), cancellationToken);
        }
    }

    private async Task RunCommandAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        OperationErrorChanged?.Invoke(null);
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or FormatException
            or InvalidOperationException
            or System.Security.Cryptography.CryptographicException)
        {
            OperationErrorChanged?.Invoke(exception);
        }
    }

    private async Task LoadSlotAsync(string slot, CancellationToken cancellationToken)
    {
        var version = ++slotLoadVersion;
        IsLoaded = false;
        var service = snapshotService;
        var instId = instanceId;
        var snapId = snapshotId;
        try
        {
            var (json, viewModels) = await Task.Run(() =>
            {
                var decrypted = service.DecryptSaveAsync(instId, snapId, slot, cancellationToken)
                    .GetAwaiter().GetResult();
                var flattened = SaveGameEditor.Flatten(decrypted);
                var vms = new SaveEntryViewModel[flattened.Count];
                for (var i = 0; i < flattened.Count; i++)
                {
                    vms[i] = new SaveEntryViewModel(flattened[i]);
                }

                return (decrypted, vms);
            }, cancellationToken);
            if (version != slotLoadVersion)
            {
                return;
            }
            foreach (var vm in Entries)
            {
                vm.PropertyChanged -= OnEntryPropertyChanged;
            }
            currentSlot = slot;
            originalJson = json;
            foreach (var vm in viewModels)
            {
                vm.PropertyChanged += OnEntryPropertyChanged;
            }
            Entries = viewModels;
            SelectedSlot = slot;
            IsDirty = false;
        }
        catch
        {
            if (version == slotLoadVersion)
            {
                SelectedSlot = string.IsNullOrEmpty(currentSlot) ? null : currentSlot;
            }
            throw;
        }
        finally
        {
            if (version == slotLoadVersion)
            {
                IsLoaded = !string.IsNullOrEmpty(currentSlot);
            }
        }
    }

    private void OnEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SaveEntryViewModel.Value))
        {
            IsDirty = true;
        }
    }
}
