using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    internal static readonly Uri PresetShareServiceUri =
        new("https://crystalfly-preset-share.vercel.app/");

    private PresetShareClient? presetShareClient;

    public ObservableCollection<ModPreset> ModPresets { get; } = [];

    public ObservableCollection<SettingOption<ModPresetApplyMode>> PresetModeOptions { get; } = [];

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

    public bool HasSelectedPreset => SelectedPreset is not null;

    public int SelectedPresetEntryCount => SelectedPreset?.Entries.Count ?? 0;

    public bool HasLastPresetShare => !string.IsNullOrWhiteSpace(LastPresetShareCode);

    public string LastPresetShareUrl => HasLastPresetShare
        ? new Uri(PresetShareServiceUri, $"share/{LastPresetShareCode}").AbsoluteUri
        : string.Empty;

    [RelayCommand]
    private async Task CreatePresetAsync()
    {
        if (string.IsNullOrWhiteSpace(PresetName) || SelectedPresetModeOption is null)
        {
            ErrorMessage = Loc["PresetNameRequired"];
            return;
        }
        string? createdId = null;
        await RunPresetMutationAsync(async (service, cancellationToken) =>
        {
            createdId = (await service.CaptureAsync(
                PresetName.Trim(),
                SelectedPresetModeOption.Value,
                cancellationToken)).Id;
        });
        SelectedPreset = createdId is null
            ? SelectedPreset
            : ModPresets.FirstOrDefault(preset => preset.Id == createdId);
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
        var id = SelectedPreset.Id;
        await RunPresetMutationAsync((service, cancellationToken) => service.RecaptureAsync(
            id,
            PresetName.Trim(),
            SelectedPresetModeOption.Value,
            cancellationToken));
        SelectedPreset = ModPresets.FirstOrDefault(preset => preset.Id == id);
    }

    [RelayCommand]
    private async Task CopySelectedPresetAsync()
    {
        if (SelectedPreset is null || string.IsNullOrWhiteSpace(PresetCopyName))
        {
            return;
        }
        string? copiedId = null;
        await RunPresetMutationAsync(async (service, cancellationToken) =>
        {
            copiedId = (await service.CopyAsync(
                SelectedPreset.Id,
                PresetCopyName.Trim(),
                cancellationToken)).Id;
        });
        SelectedPreset = copiedId is null
            ? SelectedPreset
            : ModPresets.FirstOrDefault(preset => preset.Id == copiedId);
    }

    internal async Task DeleteSelectedPresetAsync()
    {
        if (SelectedPreset is null)
        {
            return;
        }
        var id = SelectedPreset.Id;
        await RunPresetMutationAsync((service, cancellationToken) =>
            service.DeleteAsync(id, cancellationToken));
        SelectedPreset = ModPresets.FirstOrDefault();
    }

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
        ToastRequested?.Invoke(Loc["PresetExported"]);
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
            ToastRequested?.Invoke(Loc["PresetShared"]);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidDataException
            or InvalidOperationException
            or OperationCanceledException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportSharedPresetAsync()
    {
        var code = PresetShareCode.Trim();
        if (code.Length == 0)
        {
            return;
        }
        ErrorMessage = null;
        try
        {
            var shared = await GetPresetShareClient().GetAsync(code, lifetimeCancellation.Token);
            string? importedId = null;
            await RunPresetMutationAsync(async (service, cancellationToken) =>
            {
                importedId = (await service.ImportAsync(
                    CrystalflyJson.Serialize(shared),
                    cancellationToken)).Id;
            });
            SelectedPreset = importedId is null
                ? SelectedPreset
                : ModPresets.FirstOrDefault(preset => preset.Id == importedId);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidDataException
            or InvalidOperationException
            or ArgumentException
            or OperationCanceledException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    internal async Task<PresetApplyPlan?> CreateSelectedPresetPlanAsync()
    {
        if (SelectedInstance is null || SelectedPreset is null)
        {
            return null;
        }
        var plan = await CreateModPresetService(SelectedInstance.Record)
            .CreatePlanAsync(SelectedPreset, lifetimeCancellation.Token);
        ProjectPresetApplySteps(plan);
        return plan;
    }

    internal async Task EnqueueSelectedPresetAsync()
    {
        if (SelectedInstance is null || SelectedPreset is null)
        {
            return;
        }
        ErrorMessage = null;
        try
        {
            var service = CreateModPresetService(SelectedInstance.Record);
            var plan = await service.CreatePlanAsync(SelectedPreset, lifetimeCancellation.Token);
            ProjectPresetApplySteps(plan);
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
                ToastRequested?.Invoke(Loc["PresetNoChanges"]);
                return;
            }
            await downloadQueue.InitializeAsync(lifetimeCancellation.Token);
            var group = ModPresetQueueGroupFactory.Create(plan, catalog, SelectedInstance.Record);
            var result = await downloadQueue.EnqueueAsync(group, lifetimeCancellation.Token);
            ToastRequested?.Invoke(result.Added
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
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
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

    private async Task RunPresetMutationAsync(
        Func<ModPresetService, CancellationToken, Task> operation)
    {
        if (SelectedInstance is null || IsMutationBlocked())
        {
            return;
        }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var record = SelectedInstance.Record;
            await instanceOperationCoordinator.RunAsync(record.Id, async cancellationToken =>
            {
                if (new SystemHollowKnightProcessProbe().IsRunning())
                {
                    throw new InvalidOperationException(Loc["CloseGameFirst"]);
                }
                await EnsureTransactionsHealthyAsync(cancellationToken);
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
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
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
        var selectedId = SelectedPreset?.Id;
        var service = CreateModPresetService(record);
        var presets = await service.GetAllAsync(cancellationToken);
        ModPresets.Clear();
        foreach (var preset in presets)
        {
            ModPresets.Add(preset);
        }
        SelectedPreset = selectedId is null
            ? ModPresets.FirstOrDefault()
            : ModPresets.FirstOrDefault(preset => preset.Id == selectedId)
                ?? ModPresets.FirstOrDefault();
        HasPresetRestorePoint = await service.HasRestorePointAsync(cancellationToken);
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

    private void ProjectPresetApplySteps(PresetApplyPlan plan)
    {
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
        catalog.Mods,
        CreateLoaderManager(record),
        CreateModManager(record),
        Path.Combine(GetInstanceStateRoot(record.Id), "presets"));

    private PresetShareClient GetPresetShareClient() =>
        presetShareClient ??= new PresetShareClient(
            directMetadataHttpClient,
            networkPolicy,
            PresetShareServiceUri);

    partial void OnSelectedPresetChanged(ModPreset? value)
    {
        PresetName = value?.Name ?? string.Empty;
        PresetCopyName = value is null ? string.Empty : $"{value.Name} - {Loc["CopySuffix"]}";
        if (PresetModeOptions.Count != 0)
        {
            SelectedPresetModeOption = PresetModeOptions.First(option =>
                option.Value == (value?.ApplyMode ?? ModPresetApplyMode.Append));
        }
    }
}
