using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    internal static readonly Uri PresetShareServiceUri =
        new("https://crystalflyapi.jwst233.top/");

    private PresetShareClient? presetShareClient;

    // Guards every swap of the mod-pack collections. The mutation-command reload
    // (LoadModPresetsAsync) can overlap an instance-details load, and both replace
    // ModPresets/VisibleModPacks with a Clear+Add sequence; interleaving those
    // sequences duplicates (or corrupts) the entries, so the swaps must be mutually
    // exclusive. The lock is only ever taken around synchronous, await-free blocks.
    private readonly object presetCollectionGate = new();

    public ObservableCollection<ModPreset> ModPresets { get; } = [];

    public ObservableCollection<SettingOption<ModPresetApplyMode>> PresetModeOptions { get; } = [];

    public ObservableCollection<ModPreset> VisibleModPacks { get; } = [];

    public ObservableCollection<ModPresetEntry> VisibleSelectedModPackEntries { get; } = [];

    public ObservableCollection<PresetApplyStepItemViewModel> PresetApplySteps { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPreset))]
    [NotifyPropertyChangedFor(nameof(SelectedPresetEntryCount))]
    public partial ModPreset? SelectedPreset { get; set; }

    [ObservableProperty]
    public partial SettingOption<ModPresetApplyMode>? SelectedPresetModeOption { get; set; }

    [ObservableProperty]
    public partial string PresetName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PresetCopyName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PresetShareCode { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastPresetShare))]
    [NotifyPropertyChangedFor(nameof(LastPresetShareUrl))]
    public partial string LastPresetShareCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastPresetDeleteToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasPresetRestorePoint { get; set; }

    [ObservableProperty]
    private string modPackSearchText = string.Empty;

    [ObservableProperty]
    private string modPackEntrySearchText = string.Empty;

    [ObservableProperty]
    private bool isSelectedPresetEntriesExpanded;

    public bool HasSelectedPreset => SelectedPreset is not null;

    public int SelectedPresetEntryCount => SelectedPreset?.Entries.Count ?? 0;

    public bool HasLastPresetShare => !string.IsNullOrWhiteSpace(LastPresetShareCode);

    public string LastPresetShareUrl => HasLastPresetShare
        ? new Uri(PresetShareServiceUri, $"share/{LastPresetShareCode}").AbsoluteUri
        : string.Empty;

    [RelayCommand]
    private void ToggleSelectedPresetEntries()
    {
        IsSelectedPresetEntriesExpanded = !IsSelectedPresetEntriesExpanded;
    }

    [RelayCommand]
    private async Task CreatePresetAsync()
    {
        if (string.IsNullOrWhiteSpace(PresetName) || SelectedPresetModeOption is null)
        {
            ErrorMessage = Loc["PresetNameRequired"];
            return;
        }
        var record = SelectedInstance?.Record;
        var name = PresetName.Trim();
        var mode = SelectedPresetModeOption.Value;
        string? createdId = null;
        await RunPresetMutationAsync(async (service, cancellationToken) =>
        {
            createdId = (await service.CaptureAsync(
                name,
                mode,
                cancellationToken)).Id;
        });
        if (createdId is not null && SelectedInstance?.Record == record)
        {
            SelectedPreset = ModPresets.FirstOrDefault(preset => preset.Id == createdId);
        }
    }

    [RelayCommand]
    private async Task RecaptureSelectedPresetAsync()
    {
        if (SelectedPreset is null
            || string.IsNullOrWhiteSpace(PresetName)
            || SelectedPresetModeOption is null)
        {
            return;
        }
        var record = SelectedInstance?.Record;
        var id = SelectedPreset.Id;
        var name = PresetName.Trim();
        var mode = SelectedPresetModeOption.Value;
        await RunPresetMutationAsync((service, cancellationToken) => service.RecaptureAsync(
            id,
            name,
            mode,
            cancellationToken));
        if (SelectedInstance?.Record == record)
        {
            SelectedPreset = ModPresets.FirstOrDefault(preset => preset.Id == id);
        }
    }

    [RelayCommand]
    private Task CopySelectedPresetAsync()
    {
        if (SelectedInstance is null || SelectedPreset is null || string.IsNullOrWhiteSpace(PresetCopyName))
        {
            return Task.CompletedTask;
        }
        return CopyPresetAsync(SelectedInstance.Record, SelectedPreset.Id, PresetCopyName.Trim());
    }

    internal async Task CopyPresetAsync(InstanceRecord record, string id, string name)
    {
        string? copiedId = null;
        await RunPresetMutationAsync(record, async (service, cancellationToken) =>
        {
            copiedId = (await service.CopyAsync(
                id,
                name,
                cancellationToken)).Id;
        });
        if (copiedId is not null && SelectedInstance?.Record == record)
        {
            SelectedPreset = ModPresets.FirstOrDefault(preset => preset.Id == copiedId);
        }
    }

    internal Task DeleteSelectedPresetAsync() => SelectedInstance is { } instance && SelectedPreset is { } preset
        ? DeletePresetAsync(instance.Record, preset.Id)
        : Task.CompletedTask;

    internal Task DeletePresetAsync(InstanceRecord record, string id) =>
        RunPresetMutationAsync(record, (service, cancellationToken) => service.DeleteAsync(id, cancellationToken));

    internal async Task ImportPresetFromFileAsync(string path)
    {
        string? importedId = null;
        await RunPresetMutationAsync(async (service, cancellationToken) =>
        {
            importedId = (await service.ImportFileAsync(path, cancellationToken)).Id;
        });
        SelectedPreset = importedId is null
            ? SelectedPreset
            : ModPresets.FirstOrDefault(preset => preset.Id == importedId);
    }

    internal async Task ExportSelectedPresetToFileAsync(string path)
    {
        if (SelectedInstance is null || SelectedPreset is null)
        {
            return;
        }
        var document = await CreateModPresetService(SelectedInstance.Record)
            .ExportAsync(SelectedPreset.Id, lifetimeCancellation.Token);
        var target = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(target), ".json", StringComparison.OrdinalIgnoreCase))
        {
            target += ".json";
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, document, lifetimeCancellation.Token);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
        NotifyToast(Loc["PresetExported"]);
    }

    [RelayCommand]
    private async Task ShareSelectedPresetAsync()
    {
        if (SelectedPreset is null)
        {
            return;
        }
        ErrorMessage = null;
        try
        {
            var result = await GetPresetShareClient().CreateAsync(
                SelectedPreset,
                lifetimeCancellation.Token);
            LastPresetShareCode = result.Code;
            LastPresetDeleteToken = result.DeleteToken;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidDataException
            or InvalidOperationException
            or OperationCanceledException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }

    [RelayCommand]
    private async Task ImportSharedPresetAsync()
    {
        var record = SelectedInstance?.Record;
        var code = PresetShareCode.Trim();
        if (record is null || code.Length == 0)
        {
            return;
        }
        ErrorMessage = null;
        try
        {
            var shared = await GetPresetShareClient().GetAsync(code, lifetimeCancellation.Token);
            string? importedId = null;
            await RunPresetMutationAsync(record, async (service, cancellationToken) =>
            {
                importedId = (await service.ImportAsync(
                    CrystalflyJson.Serialize(shared),
                    cancellationToken)).Id;
            });
            if (importedId is not null && SelectedInstance?.Record == record)
            {
                SelectedPreset = ModPresets.FirstOrDefault(preset => preset.Id == importedId);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidDataException
            or InvalidOperationException
            or ArgumentException
            or OperationCanceledException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }

    internal async Task<PresetApplyPlan?> CreateSelectedPresetPlanAsync()
    {
        if (SelectedInstance is null || SelectedPreset is null)
        {
            return null;
        }
        var record = SelectedInstance.Record;
        var preset = SelectedPreset;
        var plan = await CreateModPresetService(record)
            .CreatePlanAsync(preset, lifetimeCancellation.Token);
        if (SelectedInstance?.Record != record || SelectedPreset != preset)
        {
            return null;
        }
        ProjectPresetApplySteps(plan);
        return plan;
    }

    internal Task EnqueueSelectedPresetAsync()
    {
        if (SelectedInstance is null || SelectedPreset is null)
        {
            return Task.CompletedTask;
        }
        return EnqueuePresetAsync(SelectedInstance.Record, SelectedPreset);
    }

    internal async Task EnqueuePresetAsync(InstanceRecord record, ModPreset preset)
    {
        ErrorMessage = null;
        try
        {
            var sourceCatalog = catalog;
            var service = CreateModPresetService(record);
            var plan = await service.CreatePlanAsync(preset, lifetimeCancellation.Token);
            if (SelectedInstance?.Record == record && SelectedPreset == preset)
            {
                ProjectPresetApplySteps(plan);
            }
            if (plan.IsBlocked)
            {
                throw new InvalidOperationException(
                    plan.Steps.First(step => step.State == PresetApplyStepState.Blocked).Reason);
            }
            var automatic = plan.Steps.Any(step =>
                step.State == PresetApplyStepState.Pending
                && step.Kind != PresetApplyStepKind.Unresolved);
            if (!automatic)
            {
                NotifyToast(Loc["PresetNoChanges"]);
                return;
            }
            await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
            var group = ModPresetQueueGroupFactory.Create(plan, sourceCatalog, record);
            var result = await DownloadCenter.EnqueueAsync(group, lifetimeCancellation.Token);
            NotifyToast(result.Added
                ? Loc["AddedToDownloadQueue"]
                : Loc["QueueTaskAlreadyExists"]);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }

    [RelayCommand]
    private async Task RestorePresetStateAsync()
    {
        await RunInstanceMutationAsync(record => CreateModPresetService(record)
            .RestoreLastAsync(lifetimeCancellation.Token));
        if (SelectedInstance is not null)
        {
            HasPresetRestorePoint = await CreateModPresetService(SelectedInstance.Record)
                .HasRestorePointAsync(lifetimeCancellation.Token);
        }
    }

    private Task RunPresetMutationAsync(Func<ModPresetService, CancellationToken, Task> operation) =>
        SelectedInstance is { } instance
            ? RunPresetMutationAsync(instance.Record, operation)
            : Task.CompletedTask;

    private async Task RunPresetMutationAsync(
        InstanceRecord record,
        Func<ModPresetService, CancellationToken, Task> operation)
    {
        if (IsMutationBlocked())
        {
            return;
        }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await instanceOperationCoordinator.RunAsync(record.Id, async cancellationToken =>
            {
                if (new SystemHollowKnightProcessProbe().IsRunning())
                {
                    throw new InvalidOperationException(Loc["CloseGameFirst"]);
                }
                await EnsureTransactionsHealthyAsync(cancellationToken, Directory.GetParent(record.RootPath)!.FullName);
                await operation(CreateModPresetService(record), cancellationToken);
            }, lifetimeCancellation.Token);
            await LoadModPresetsAsync(record, lifetimeCancellation.Token);
            NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadModPresetsAsync(
        InstanceRecord record,
        CancellationToken cancellationToken)
    {
        var generation = Volatile.Read(ref detailsLoadGeneration);
        var versionRoot = VersionRoot;
        var selectedId = SelectedPreset?.Id;
        var service = CreateModPresetService(record);
        var presets = await service.GetAllAsync(cancellationToken);
        var hasRestorePoint = await service.HasRestorePointAsync(cancellationToken);
        // Swap the collection atomically: the mutation-command reload can overlap
        // an instance-details load that also replaces ModPresets, and interleaving
        // the two Clear+Add sequences would duplicate (or corrupt) the list.
        lock (presetCollectionGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref detailsLoadGeneration)
                || SelectedInstance?.Record != record
                || !string.Equals(versionRoot, VersionRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            ModPresets.Clear();
            foreach (var preset in presets)
            {
                ModPresets.Add(preset);
            }
            SelectedPreset = selectedId is null
                ? ModPresets.FirstOrDefault()
                : ModPresets.FirstOrDefault(preset => preset.Id == selectedId)
                    ?? ModPresets.FirstOrDefault();
            RefreshModPackWorkspace();
            HasPresetRestorePoint = hasRestorePoint;
        }
    }

    private void RebuildPresetModeOptions()
    {
        var selected = SelectedPresetModeOption?.Value
            ?? SelectedPreset?.ApplyMode
            ?? ModPresetApplyMode.Append;
        PresetModeOptions.Clear();
        PresetModeOptions.Add(new(ModPresetApplyMode.Append, Loc["PresetModeAppend"]));
        PresetModeOptions.Add(new(ModPresetApplyMode.Exact, Loc["PresetModeExact"]));
        SelectedPresetModeOption = PresetModeOptions.First(option => option.Value == selected);
    }

    // Re-renders the apply-plan step texts (Action/State) and the copy-name suffix
    // after the application language switches.
    private void RefreshPresetApplySteps()
    {
        if (lastPresetApplyPlan is not null)
        {
            ProjectPresetApplySteps(lastPresetApplyPlan);
        }
    }

    private void RefreshPresetCopyName()
    {
        if (SelectedPreset is not null)
        {
            PresetCopyName = $"{SelectedPreset.Name} - {Loc["CopySuffix"]}";
        }
    }

    private void ProjectPresetApplySteps(PresetApplyPlan plan)
    {
        lastPresetApplyPlan = plan;
        PresetApplySteps.Clear();
        foreach (var step in plan.Steps)
        {
            PresetApplySteps.Add(new PresetApplyStepItemViewModel(
                step,
                Loc[step.Kind switch
                {
                    PresetApplyStepKind.Install => "PresetStepInstall",
                    PresetApplyStepKind.Enable => "PresetStepEnable",
                    PresetApplyStepKind.Disable => "PresetStepDisable",
                    PresetApplyStepKind.Unresolved => "PresetStepUnresolved",
                    _ => "PresetStepBlocked"
                }],
                Loc[step.State switch
                {
                    PresetApplyStepState.Satisfied => "PresetStateSatisfied",
                    PresetApplyStepState.Pending => "PresetStatePending",
                    PresetApplyStepState.Unresolved => "PresetStateUnresolved",
                    _ => "PresetStateBlocked"
                }]));
        }
    }

    private ModPresetService CreateModPresetService(InstanceRecord record) => new(
        record,
        ModCatalogCompatibility.ProjectForBuild(catalog.Mods, record.BuildId),
        CreateLoaderManager(record),
        CreateModManager(record),
        Path.Combine(Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(record.RootPath, record.Id))!, "presets"));

    private PresetShareClient GetPresetShareClient() =>
        presetShareClient ??= new PresetShareClient(
            directMetadataHttpClient,
            networkPolicy,
            PresetShareServiceUri);

    partial void OnSelectedPresetChanged(ModPreset? value)
    {
        RefreshModPackWorkspace();
        PresetName = value?.Name ?? string.Empty;
        PresetCopyName = value is null ? string.Empty : $"{value.Name} - {Loc["CopySuffix"]}";
        if (PresetModeOptions.Count != 0)
        {
            SelectedPresetModeOption = PresetModeOptions.First(option =>
                option.Value == (value?.ApplyMode ?? ModPresetApplyMode.Append));
        }
    }

    partial void OnModPackSearchTextChanged(string value) => RefreshVisibleModPacks();

    partial void OnModPackEntrySearchTextChanged(string value) => RefreshVisibleSelectedModPackEntries();

    private void RefreshModPackWorkspace()
    {
        RefreshVisibleModPacks();
        RefreshVisibleSelectedModPackEntries();
    }

    private void RefreshVisibleModPacks()
    {
        var search = ModPackSearchText.Trim();
        lock (presetCollectionGate)
        {
            VisibleModPacks.Clear();
            foreach (var preset in ModPresets.Where(preset => string.IsNullOrWhiteSpace(search)
                || preset.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                VisibleModPacks.Add(preset);
            }
        }
    }

    private void RefreshVisibleSelectedModPackEntries()
    {
        var search = ModPackEntrySearchText.Trim();
        lock (presetCollectionGate)
        {
            VisibleSelectedModPackEntries.Clear();
            if (SelectedPreset is null)
            {
                return;
            }

            foreach (var entry in SelectedPreset.Entries.Where(entry => string.IsNullOrWhiteSpace(search)
                || entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || (entry.Id?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.Version?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                VisibleSelectedModPackEntries.Add(entry);
            }
        }
    }
}
