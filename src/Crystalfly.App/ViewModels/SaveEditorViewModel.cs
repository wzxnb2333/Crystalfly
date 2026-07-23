using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.App.ViewModels;

public sealed partial class SaveEntryViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Path { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial string Kind { get; set; }

    public SaveEntryViewModel(SaveEntry entry)
    {
        Path = entry.Path;
        Value = entry.Value;
        Kind = entry.Kind;
    }

    public SaveEntry ToEntry() => new(Path, Value, Kind);
}

public sealed partial class SaveEditorViewModel : ViewModelBase
{
    private readonly NamedSnapshotService snapshotService;
    private readonly string instanceId;
    private readonly string? snapshotId;
    private string originalJson = string.Empty;
    private string currentSlot = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<SaveEntryViewModel> Entries { get; set; } = [];
    public ObservableCollection<string> Slots { get; } = [];

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string SourceLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedSlot { get; set; }

    public bool CanSave => IsDirty;

    public SaveEditorViewModel(
        NamedSnapshotService snapshotService,
        string instanceId,
        string? snapshotId,
        string sourceLabel)
    {
        this.snapshotService = snapshotService;
        this.instanceId = instanceId;
        this.snapshotId = snapshotId;
        SourceLabel = sourceLabel;
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
            return;
        }

        await LoadSlotAsync(slot, cancellationToken);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var entries = Entries.Select(entry => entry.ToEntry()).ToArray();
        var json = SaveGameEditor.Rebuild(originalJson, entries);
        await snapshotService.UpdateSaveAsync(instanceId, snapshotId, currentSlot, json, cancellationToken);
        originalJson = json;
        IsDirty = false;
    }

    [RelayCommand]
    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(currentSlot))
        {
            await LoadSlotAsync(currentSlot, cancellationToken);
        }
    }

    private async Task LoadSlotAsync(string slot, CancellationToken cancellationToken)
    {
        currentSlot = slot;
        IsLoaded = false;
        var service = snapshotService;
        var instId = instanceId;
        var snapId = snapshotId;
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
        originalJson = json;
        foreach (var vm in viewModels)
        {
            vm.PropertyChanged += (_, _) => IsDirty = true;
        }

        Entries = viewModels;
        IsDirty = false;
        IsLoaded = true;
    }
}
