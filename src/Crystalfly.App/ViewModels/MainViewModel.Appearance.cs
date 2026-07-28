using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.App.Appearance;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.ViewModels;

public enum BackgroundEditScope
{
    Global,
    CurrentInstance
}

public partial class MainViewModel
{
    private readonly BackgroundImageService backgroundImageService = new();
    private readonly object backgroundOpacitySaveLock = new();
    private readonly object backgroundRefreshLock = new();
    private InstanceAppearanceSettings selectedInstanceAppearance = new();
    private InstanceAppearanceSettings persistedSelectedInstanceAppearance = new();
    private string? persistedSelectedInstanceAppearanceId;
    private BackgroundImageSettings? persistedGlobalBackground;
    private Task backgroundOpacitySaveQueue = Task.CompletedTask;
    private Task backgroundRefreshTask = Task.CompletedTask;
    private CancellationTokenSource? backgroundOpacitySaveDelay;
    private CancellationTokenSource? backgroundRefreshCancellation;
    private long backgroundLoadGeneration;
    private bool suppressBackgroundOpacityChange;

    public ObservableCollection<SettingOption<BackgroundEditScope>> BackgroundScopeOptions { get; } = [];

    [ObservableProperty]
    public partial SettingOption<BackgroundEditScope>? SelectedBackgroundScope { get; set; }

    [ObservableProperty]
    public partial Bitmap? ActiveBackgroundImage { get; set; }

    [ObservableProperty]
    public partial Bitmap? BackgroundPreviewImage { get; set; }

    [ObservableProperty]
    public partial double ActiveBackgroundOpacity { get; set; }

    [ObservableProperty]
    public partial double BackgroundOpacityPercent { get; set; }

    [ObservableProperty]
    public partial bool HasInstanceBackgroundOverride { get; set; }

    public bool HasActiveBackgroundImage => ActiveBackgroundImage is not null;

    public bool HasBackgroundPreviewImage => BackgroundPreviewImage is not null;

    public bool CanEditInstanceBackground => SelectedInstance is not null;

    public bool IsEditingInstanceBackground =>
        SelectedBackgroundScope?.Value == BackgroundEditScope.CurrentInstance;

    public bool CanChangeBackgroundOpacity => GetEditableBackgroundSettings() is not null;

    public bool CanRemoveBackgroundImage => CanChangeBackgroundOpacity;

    public double BackgroundPreviewOpacity => BackgroundOpacityPercent / 100d;

    public string BackgroundInstanceName => SelectedInstance?.Name ?? Loc["BackgroundNoInstance"];

    public string BackgroundScopeStatus => IsEditingInstanceBackground
        ? !CanEditInstanceBackground
            ? Loc["BackgroundNoInstance"]
            : HasInstanceBackgroundOverride
                ? Loc["BackgroundInstanceIndependent"]
                : Loc["BackgroundInstanceInherited"]
        : settings.BackgroundImage is null
            ? Loc["BackgroundNotConfigured"]
            : TryGetExistingBackgroundSettings(GlobalAppearanceDirectory, settings.BackgroundImage) is null
                ? Loc["BackgroundNotConfigured"]
                : Loc["BackgroundGlobalConfigured"];

    public string BackgroundRemoveLabel => IsEditingInstanceBackground
        ? Loc["BackgroundRestoreGlobal"]
        : Loc["BackgroundDelete"];

    private string GlobalAppearanceDirectory => Path.Combine(paths.ApplicationDataRoot, "appearance");

    private void RebuildBackgroundScopeOptions()
    {
        var selected = SelectedBackgroundScope?.Value ?? BackgroundEditScope.Global;
        if (selected == BackgroundEditScope.CurrentInstance && SelectedInstance is null)
        {
            selected = BackgroundEditScope.Global;
        }
        SelectedBackgroundScope = null;
        BackgroundScopeOptions.Clear();
        BackgroundScopeOptions.Add(new(BackgroundEditScope.Global, Loc["BackgroundScopeGlobal"]));
        BackgroundScopeOptions.Add(new(BackgroundEditScope.CurrentInstance, Loc["BackgroundScopeInstance"]));
        SelectedBackgroundScope = BackgroundScopeOptions.First(option => option.Value == selected);
    }

    partial void OnSelectedBackgroundScopeChanged(SettingOption<BackgroundEditScope>? value)
    {
        if (value?.Value == BackgroundEditScope.CurrentInstance && SelectedInstance is null)
        {
            SelectedBackgroundScope = BackgroundScopeOptions.FirstOrDefault(option =>
                option.Value == BackgroundEditScope.Global);
            return;
        }
        QueueBackgroundAppearanceRefresh();
        NotifyBackgroundState();
    }

    partial void OnActiveBackgroundImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasActiveBackgroundImage));
    }

    partial void OnBackgroundPreviewImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasBackgroundPreviewImage));
    }

    partial void OnBackgroundOpacityPercentChanged(double value)
    {
        if (suppressBackgroundOpacityChange)
        {
            return;
        }

        var normalized = Math.Clamp((int)Math.Round(value), 0, 100);
        if (Math.Abs(value - normalized) > double.Epsilon)
        {
            SetBackgroundOpacityWithoutSaving(normalized);
        }
        var editable = GetEditableBackgroundSettings();
        if (editable is null)
        {
            return;
        }

        var next = editable with { OpacityPercent = normalized };
        if (IsEditingInstanceBackground)
        {
            var instance = SelectedInstance;
            if (instance is null)
            {
                return;
            }
            selectedInstanceAppearance = selectedInstanceAppearance with { BackgroundImage = next };
            ActiveBackgroundOpacity = normalized / 100d;
            var snapshot = selectedInstanceAppearance;
            QueueBackgroundOpacitySave(
                () => SaveInstanceBackgroundOpacityAsync(instance.Id, snapshot));
        }
        else
        {
            settings = settings with { BackgroundImage = next };
            if (!HasInstanceBackgroundOverride)
            {
                ActiveBackgroundOpacity = normalized / 100d;
            }
            QueueBackgroundOpacitySave(() => SaveGlobalBackgroundOpacityAsync(next));
        }
        OnPropertyChanged(nameof(BackgroundPreviewOpacity));
    }

    internal async Task SetBackgroundImageAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsEditingInstanceBackground)
            {
                var instance = SelectedInstance ?? throw new InvalidOperationException(Loc["BackgroundNoInstance"]);
                var directory = GetInstanceAppearanceDirectory(instance.Id);
                var current = selectedInstanceAppearance.BackgroundImage;
                await backgroundImageService.ReplaceAsync(
                    sourcePath,
                    directory,
                    current,
                    async (next, token) =>
                    {
                        var updated = selectedInstanceAppearance with { BackgroundImage = next };
                        await InstanceAppearanceSettingsStore.SaveAsync(
                            GetInstanceAppearanceSettingsPath(instance.Id),
                            updated,
                            token);
                        selectedInstanceAppearance = updated;
                        persistedSelectedInstanceAppearance = updated;
                        persistedSelectedInstanceAppearanceId = instance.Id;
                    },
                    cancellationToken);
            }
            else
            {
                await FlushSettingsSavesAsync();
                var current = settings.BackgroundImage;
                await backgroundImageService.ReplaceAsync(
                    sourcePath,
                    GlobalAppearanceDirectory,
                    current,
                    SaveGlobalBackgroundSettingsAsync,
                    cancellationToken);
            }
            await RefreshBackgroundAppearanceAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException
                                          or InvalidOperationException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    internal async Task RemoveBackgroundImageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsEditingInstanceBackground)
            {
                var instance = SelectedInstance ?? throw new InvalidOperationException(Loc["BackgroundNoInstance"]);
                var current = selectedInstanceAppearance.BackgroundImage;
                await backgroundImageService.RemoveAsync(
                    GetInstanceAppearanceDirectory(instance.Id),
                    current,
                    async (_, token) =>
                    {
                        var updated = selectedInstanceAppearance with { BackgroundImage = null };
                        await InstanceAppearanceSettingsStore.SaveAsync(
                            GetInstanceAppearanceSettingsPath(instance.Id),
                            updated,
                            token);
                        selectedInstanceAppearance = updated;
                        persistedSelectedInstanceAppearance = updated;
                        persistedSelectedInstanceAppearanceId = instance.Id;
                    },
                    cancellationToken);
            }
            else
            {
                await FlushSettingsSavesAsync();
                await backgroundImageService.RemoveAsync(
                    GlobalAppearanceDirectory,
                    settings.BackgroundImage,
                    SaveGlobalBackgroundSettingsAsync,
                    cancellationToken);
            }
            await RefreshBackgroundAppearanceAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    internal async Task RefreshBackgroundAppearanceAsync(CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref backgroundLoadGeneration);
        var selected = SelectedInstance;
        var instanceAppearance = new InstanceAppearanceSettings();
        if (selected is not null && !string.IsNullOrWhiteSpace(VersionRoot))
        {
            try
            {
                instanceAppearance = await InstanceAppearanceSettingsStore.LoadAsync(
                    GetInstanceAppearanceSettingsPath(selected.Id),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or JsonException)
            {
                instanceAppearance = new InstanceAppearanceSettings();
            }
        }

        var instanceDirectory = selected is not null && !string.IsNullOrWhiteSpace(VersionRoot)
            ? GetInstanceAppearanceDirectory(selected.Id)
            : null;
        var instanceBitmap = TryLoadBackgroundBitmap(
            instanceDirectory,
            instanceAppearance.BackgroundImage);
        var globalBitmap = TryLoadBackgroundBitmap(GlobalAppearanceDirectory, settings.BackgroundImage);
        var validInstance = instanceBitmap is not null;
        var activeBitmap = validInstance ? instanceBitmap : globalBitmap;
        var activeSettings = validInstance ? instanceAppearance.BackgroundImage : settings.BackgroundImage;
        if (generation != Interlocked.Read(ref backgroundLoadGeneration))
        {
            instanceBitmap?.Dispose();
            globalBitmap?.Dispose();
            return;
        }

        selectedInstanceAppearance = instanceAppearance;
        persistedSelectedInstanceAppearance = instanceAppearance;
        persistedSelectedInstanceAppearanceId = selected?.Id;
        HasInstanceBackgroundOverride = validInstance;
        ReplaceActiveBackgroundBitmap(activeBitmap);
        if (validInstance)
        {
            globalBitmap?.Dispose();
        }
        ActiveBackgroundOpacity = ActiveBackgroundImage is null
            ? 0
            : (activeSettings?.OpacityPercent ?? BackgroundImageSettings.DefaultOpacityPercent) / 100d;
        RefreshBackgroundPreview();
    }

    private void RefreshBackgroundPreview()
    {
        var settingsForScope = GetEditableBackgroundSettings();
        var directory = IsEditingInstanceBackground && SelectedInstance is { } instance
            ? GetInstanceAppearanceDirectory(instance.Id)
            : GlobalAppearanceDirectory;
        var preview = TryLoadBackgroundBitmap(directory, settingsForScope);
        if (preview is null && IsEditingInstanceBackground)
        {
            settingsForScope = settings.BackgroundImage;
            preview = TryLoadBackgroundBitmap(GlobalAppearanceDirectory, settingsForScope);
        }
        ReplaceBackgroundPreviewBitmap(preview);
        SetBackgroundOpacityWithoutSaving(settingsForScope?.OpacityPercent ?? 0);
        NotifyBackgroundState();
    }

    private BackgroundImageSettings? GetEditableBackgroundSettings() => IsEditingInstanceBackground
        ? HasInstanceBackgroundOverride ? selectedInstanceAppearance.BackgroundImage : null
        : TryGetExistingBackgroundSettings(GlobalAppearanceDirectory, settings.BackgroundImage);

    private static BackgroundImageSettings? TryGetExistingBackgroundSettings(
        string directory,
        BackgroundImageSettings? value) =>
        value is not null && File.Exists(Path.Combine(directory, value.FileName)) ? value : null;

    private static Bitmap? TryLoadBackgroundBitmap(string? directory, BackgroundImageSettings? value)
    {
        if (directory is null || value is null || !BackgroundImageSettings.IsSafeFileName(value.FileName))
        {
            return null;
        }
        try
        {
            var path = Path.Combine(directory, value.FileName);
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or IOException
                                          or InvalidOperationException
                                          or NotSupportedException)
        {
            return null;
        }
    }

    private async Task SaveGlobalBackgroundSettingsAsync(
        BackgroundImageSettings? value,
        CancellationToken cancellationToken)
    {
        var previous = settings;
        settings = settings with { BackgroundImage = value };
        await settingsSaveLock.WaitAsync(cancellationToken);
        try
        {
            await CrystalflySettingsStore.SaveAsync(settingsPath, settings, cancellationToken);
            persistedGlobalBackground = value;
        }
        catch
        {
            settings = previous;
            throw;
        }
        finally
        {
            settingsSaveLock.Release();
        }
    }

    private async Task SaveGlobalBackgroundOpacityAsync(BackgroundImageSettings next)
    {
        await settingsSaveLock.WaitAsync();
        try
        {
            var snapshot = settings with { BackgroundImage = next };
            await CrystalflySettingsStore.SaveAsync(settingsPath, snapshot);
            persistedGlobalBackground = next;
        }
        catch (Exception exception) when (IsExpectedSettingsException(exception))
        {
            if (Equals(settings.BackgroundImage, next))
            {
                settings = settings with { BackgroundImage = persistedGlobalBackground };
                if (!HasInstanceBackgroundOverride)
                {
                    ActiveBackgroundOpacity = persistedGlobalBackground?.OpacityPercent / 100d ?? 0;
                }
                RefreshBackgroundPreview();
            }
            throw;
        }
        finally
        {
            settingsSaveLock.Release();
        }
    }

    private async Task SaveInstanceBackgroundOpacityAsync(
        string instanceId,
        InstanceAppearanceSettings snapshot)
    {
        try
        {
            await InstanceAppearanceSettingsStore.SaveAsync(
                GetInstanceAppearanceSettingsPath(instanceId),
                snapshot);
            if (string.Equals(SelectedInstance?.Id, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                persistedSelectedInstanceAppearance = snapshot;
                persistedSelectedInstanceAppearanceId = instanceId;
            }
        }
        catch (Exception exception) when (IsExpectedSettingsException(exception))
        {
            if (string.Equals(SelectedInstance?.Id, instanceId, StringComparison.OrdinalIgnoreCase)
                && Equals(selectedInstanceAppearance.BackgroundImage, snapshot.BackgroundImage))
            {
                selectedInstanceAppearance = string.Equals(
                    persistedSelectedInstanceAppearanceId,
                    instanceId,
                    StringComparison.OrdinalIgnoreCase)
                    ? persistedSelectedInstanceAppearance
                    : new InstanceAppearanceSettings();
                HasInstanceBackgroundOverride = TryGetExistingBackgroundSettings(
                    GetInstanceAppearanceDirectory(instanceId),
                    selectedInstanceAppearance.BackgroundImage) is not null;
                ActiveBackgroundOpacity = HasInstanceBackgroundOverride
                    ? selectedInstanceAppearance.BackgroundImage!.OpacityPercent / 100d
                    : persistedGlobalBackground?.OpacityPercent / 100d ?? 0;
                RefreshBackgroundPreview();
            }
            throw;
        }
    }

    private string GetInstanceAppearanceDirectory(string instanceId) =>
        Path.Combine(GetInstanceStateRoot(instanceId), "appearance");

    private string GetInstanceAppearanceSettingsPath(string instanceId) =>
        Path.Combine(GetInstanceAppearanceDirectory(instanceId), "appearance.json");

    private void SetBackgroundOpacityWithoutSaving(double value)
    {
        suppressBackgroundOpacityChange = true;
        try
        {
            BackgroundOpacityPercent = value;
        }
        finally
        {
            suppressBackgroundOpacityChange = false;
        }
        OnPropertyChanged(nameof(BackgroundPreviewOpacity));
    }

    private void QueueBackgroundOpacitySave(Func<Task> save)
    {
        lock (backgroundOpacitySaveLock)
        {
            backgroundOpacitySaveDelay?.Cancel();
            backgroundOpacitySaveDelay?.Dispose();
            backgroundOpacitySaveDelay = new CancellationTokenSource();
            backgroundOpacitySaveQueue = SaveBackgroundOpacityAfterAsync(
                backgroundOpacitySaveQueue,
                save,
                backgroundOpacitySaveDelay.Token);
        }
    }

    private async Task SaveBackgroundOpacityAfterAsync(
        Task previous,
        Func<Task> save,
        CancellationToken cancellationToken)
    {
        try
        {
            await previous;
            await Task.Delay(300, cancellationToken);
            await save();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private void QueueBackgroundAppearanceRefresh()
    {
        lock (backgroundRefreshLock)
        {
            backgroundRefreshCancellation?.Cancel();
            backgroundRefreshCancellation?.Dispose();
            backgroundRefreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                lifetimeCancellation.Token);
            backgroundRefreshTask = RefreshBackgroundAppearanceSafelyAsync(
                backgroundRefreshCancellation.Token);
        }
    }

    private async Task RefreshBackgroundAppearanceSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshBackgroundAppearanceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private async Task DisposeBackgroundAppearanceAsync()
    {
        Task pendingRefresh;
        CancellationTokenSource? refreshCancellation;
        lock (backgroundRefreshLock)
        {
            Interlocked.Increment(ref backgroundLoadGeneration);
            refreshCancellation = backgroundRefreshCancellation;
            backgroundRefreshCancellation = null;
            refreshCancellation?.Cancel();
            pendingRefresh = backgroundRefreshTask;
        }

        Task pending;
        lock (backgroundOpacitySaveLock)
        {
            pending = backgroundOpacitySaveQueue;
        }
        await pendingRefresh;
        await pending;
        refreshCancellation?.Dispose();
        backgroundOpacitySaveDelay?.Dispose();
        backgroundOpacitySaveDelay = null;
        ReplaceActiveBackgroundBitmap(null);
        ReplaceBackgroundPreviewBitmap(null);
    }

    private void ReplaceActiveBackgroundBitmap(Bitmap? next)
    {
        var previous = ActiveBackgroundImage;
        ActiveBackgroundImage = next;
        if (!ReferenceEquals(previous, next))
        {
            previous?.Dispose();
        }
    }

    private void ReplaceBackgroundPreviewBitmap(Bitmap? next)
    {
        var previous = BackgroundPreviewImage;
        BackgroundPreviewImage = next;
        if (!ReferenceEquals(previous, next))
        {
            previous?.Dispose();
        }
    }

    private void NotifyBackgroundState()
    {
        OnPropertyChanged(nameof(CanEditInstanceBackground));
        OnPropertyChanged(nameof(IsEditingInstanceBackground));
        OnPropertyChanged(nameof(CanChangeBackgroundOpacity));
        OnPropertyChanged(nameof(CanRemoveBackgroundImage));
        OnPropertyChanged(nameof(BackgroundInstanceName));
        OnPropertyChanged(nameof(BackgroundScopeStatus));
        OnPropertyChanged(nameof(BackgroundRemoveLabel));
    }
}
