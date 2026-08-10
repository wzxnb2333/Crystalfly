using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Appearance;
using Crystalfly.App.Theming;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;

namespace Crystalfly.App.ViewModels;

public enum BackgroundEditScope
{
    Global,
    CurrentInstance
}

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsDependencies dependencies;
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

    internal SettingsViewModel(SettingsDependencies dependencies)
    {
        this.dependencies = dependencies;
    }

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

    public bool CanEditInstanceBackground => dependencies.GetSelectedInstance() is not null;

    public bool IsEditingInstanceBackground =>
        SelectedBackgroundScope?.Value == BackgroundEditScope.CurrentInstance;

    public bool CanChangeBackgroundOpacity => GetEditableBackgroundSettings() is not null;

    public bool CanRemoveBackgroundImage => CanChangeBackgroundOpacity;

    public double BackgroundPreviewOpacity => BackgroundOpacityPercent / 100d;

    public string BackgroundInstanceName =>
        dependencies.GetSelectedInstance()?.Name ?? dependencies.Loc()["BackgroundNoInstance"];

    public string BackgroundScopeStatus => IsEditingInstanceBackground
        ? !CanEditInstanceBackground
            ? dependencies.Loc()["BackgroundNoInstance"]
            : HasInstanceBackgroundOverride
                ? dependencies.Loc()["BackgroundInstanceIndependent"]
                : dependencies.Loc()["BackgroundInstanceInherited"]
        : dependencies.GetSettings().BackgroundImage is null
            ? dependencies.Loc()["BackgroundNotConfigured"]
            : TryGetExistingBackgroundSettings(GlobalAppearanceDirectory, dependencies.GetSettings().BackgroundImage) is null
                ? dependencies.Loc()["BackgroundNotConfigured"]
                : dependencies.Loc()["BackgroundGlobalConfigured"];

    public string BackgroundRemoveLabel => IsEditingInstanceBackground
        ? dependencies.Loc()["BackgroundRestoreGlobal"]
        : dependencies.Loc()["BackgroundDelete"];

    private string GlobalAppearanceDirectory => Path.Combine(dependencies.ApplicationDataRoot, "appearance");

    // Rebuilds every Loc-derived option label and computed text of this view model
    // after the application language switches.
    internal void RefreshLocalization()
    {
        RebuildSettingOptions();
        RebuildCustomModLinksOptions();
        RebuildBackgroundScopeOptions();
        OnPropertyChanged(nameof(BackgroundScopeStatus));
        OnPropertyChanged(nameof(BackgroundRemoveLabel));
        OnPropertyChanged(nameof(BackgroundInstanceName));
    }

    internal void InitializeBackgroundState(BackgroundImageSettings? backgroundImage)
    {
        persistedGlobalBackground = backgroundImage;
    }

    internal void RebuildBackgroundScopeOptions()
    {
        var selected = SelectedBackgroundScope?.Value ?? BackgroundEditScope.Global;
        if (selected == BackgroundEditScope.CurrentInstance && dependencies.GetSelectedInstance() is null)
        {
            selected = BackgroundEditScope.Global;
        }
        SelectedBackgroundScope = null;
        BackgroundScopeOptions.Clear();
        BackgroundScopeOptions.Add(new(BackgroundEditScope.Global, dependencies.Loc()["BackgroundScopeGlobal"]));
        BackgroundScopeOptions.Add(new(BackgroundEditScope.CurrentInstance, dependencies.Loc()["BackgroundScopeInstance"]));
        SelectedBackgroundScope = BackgroundScopeOptions.First(option => option.Value == selected);
    }

    partial void OnSelectedBackgroundScopeChanged(SettingOption<BackgroundEditScope>? value)
    {
        if (value?.Value == BackgroundEditScope.CurrentInstance && dependencies.GetSelectedInstance() is null)
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
            var instance = dependencies.GetSelectedInstance();
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
            dependencies.SetSettings(dependencies.GetSettings() with { BackgroundImage = next });
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
                var instance = dependencies.GetSelectedInstance()
                    ?? throw new InvalidOperationException(dependencies.Loc()["BackgroundNoInstance"]);
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
                await dependencies.FlushSettingsSavesAsync();
                var current = dependencies.GetSettings().BackgroundImage;
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
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
    }

    internal async Task RemoveBackgroundImageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsEditingInstanceBackground)
            {
                var instance = dependencies.GetSelectedInstance()
                    ?? throw new InvalidOperationException(dependencies.Loc()["BackgroundNoInstance"]);
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
                await dependencies.FlushSettingsSavesAsync();
                await backgroundImageService.RemoveAsync(
                    GlobalAppearanceDirectory,
                    dependencies.GetSettings().BackgroundImage,
                    SaveGlobalBackgroundSettingsAsync,
                    cancellationToken);
            }
            await RefreshBackgroundAppearanceAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException)
        {
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
    }

    internal async Task RefreshBackgroundAppearanceAsync(CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref backgroundLoadGeneration);
        var selected = dependencies.GetSelectedInstance();
        var instanceAppearance = new InstanceAppearanceSettings();
        if (selected is not null && !string.IsNullOrWhiteSpace(dependencies.GetVersionRoot()))
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

        var instanceDirectory = selected is not null && !string.IsNullOrWhiteSpace(dependencies.GetVersionRoot())
            ? GetInstanceAppearanceDirectory(selected.Id)
            : null;
        var instanceBitmap = TryLoadBackgroundBitmap(
            instanceDirectory,
            instanceAppearance.BackgroundImage);
        var globalBitmap = TryLoadBackgroundBitmap(GlobalAppearanceDirectory, dependencies.GetSettings().BackgroundImage);
        var validInstance = instanceBitmap is not null;
        var activeBitmap = validInstance ? instanceBitmap : globalBitmap;
        var activeSettings = validInstance ? instanceAppearance.BackgroundImage : dependencies.GetSettings().BackgroundImage;
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
        var directory = IsEditingInstanceBackground && dependencies.GetSelectedInstance() is { } instance
            ? GetInstanceAppearanceDirectory(instance.Id)
            : GlobalAppearanceDirectory;
        var preview = TryLoadBackgroundBitmap(directory, settingsForScope);
        if (preview is null && IsEditingInstanceBackground)
        {
            settingsForScope = dependencies.GetSettings().BackgroundImage;
            preview = TryLoadBackgroundBitmap(GlobalAppearanceDirectory, settingsForScope);
        }
        ReplaceBackgroundPreviewBitmap(preview);
        SetBackgroundOpacityWithoutSaving(settingsForScope?.OpacityPercent ?? 0);
        NotifyBackgroundState();
    }

    private BackgroundImageSettings? GetEditableBackgroundSettings() => IsEditingInstanceBackground
        ? HasInstanceBackgroundOverride ? selectedInstanceAppearance.BackgroundImage : null
        : TryGetExistingBackgroundSettings(GlobalAppearanceDirectory, dependencies.GetSettings().BackgroundImage);

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
        var previous = dependencies.GetSettings();
        var next = previous with { BackgroundImage = value };
        dependencies.SetSettings(next);
        try
        {
            await dependencies.SaveSettingsImmediately(next, cancellationToken);
            persistedGlobalBackground = value;
        }
        catch
        {
            dependencies.SetSettings(previous);
            throw;
        }
    }

    private async Task SaveGlobalBackgroundOpacityAsync(BackgroundImageSettings next)
    {
        dependencies.SetSettings(dependencies.GetSettings() with { BackgroundImage = next });
        try
        {
            var snapshot = dependencies.GetSettings();
            await dependencies.SaveSettingsImmediately(snapshot, CancellationToken.None);
            persistedGlobalBackground = next;
        }
        catch (Exception exception) when (IsExpectedSettingsException(exception))
        {
            if (Equals(dependencies.GetSettings().BackgroundImage, next))
            {
                dependencies.SetSettings(
                    dependencies.GetSettings() with { BackgroundImage = persistedGlobalBackground });
                if (!HasInstanceBackgroundOverride)
                {
                    ActiveBackgroundOpacity = persistedGlobalBackground?.OpacityPercent / 100d ?? 0;
                }
                RefreshBackgroundPreview();
            }
            throw;
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
            if (string.Equals(dependencies.GetSelectedInstance()?.Id, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                persistedSelectedInstanceAppearance = snapshot;
                persistedSelectedInstanceAppearanceId = instanceId;
            }
        }
        catch (Exception exception) when (IsExpectedSettingsException(exception))
        {
            if (string.Equals(dependencies.GetSelectedInstance()?.Id, instanceId, StringComparison.OrdinalIgnoreCase)
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
        Path.Combine(dependencies.GetInstanceStateRoot(instanceId), "appearance");

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
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
    }

    internal void QueueBackgroundAppearanceRefresh()
    {
        lock (backgroundRefreshLock)
        {
            backgroundRefreshCancellation?.Cancel();
            backgroundRefreshCancellation?.Dispose();
            backgroundRefreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                dependencies.LifetimeCancellation);
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
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
    }

    internal async Task DisposeBackgroundAppearanceAsync()
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

    public ObservableCollection<SettingOption<UiLanguage>> LanguageOptions { get; } = [];

    public ObservableCollection<SettingOption<UiTheme>> ThemeOptions { get; } = [];

    public ObservableCollection<SettingOption<UiMotionPreference>> MotionOptions { get; } = [];

    public ObservableCollection<AccentColorOptionViewModel> AccentColorOptions { get; } = [];

    public ObservableCollection<SettingOption<GitHubDownloadRoute>> GitHubRouteOptions { get; } = [];

    public ObservableCollection<SettingOption<string>> CustomModLinksBuildOptions { get; } = [];

    public ObservableCollection<SettingOption<string>> CustomModLinksLoaderOptions { get; } = [];

    public bool IsGeneralSettingsSection => CurrentSettingsSection == "General";

    public bool IsNetworkSettingsSection => CurrentSettingsSection == "Network";

    public bool IsCatalogSettingsSection => CurrentSettingsSection == "Catalog";

    public bool IsUpdatesSettingsSection => CurrentSettingsSection == "Updates";

    public bool IsAboutSettingsSection => CurrentSettingsSection == "About";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralSettingsSection))]
    [NotifyPropertyChangedFor(nameof(IsNetworkSettingsSection))]
    [NotifyPropertyChangedFor(nameof(IsCatalogSettingsSection))]
    [NotifyPropertyChangedFor(nameof(IsUpdatesSettingsSection))]
    [NotifyPropertyChangedFor(nameof(IsAboutSettingsSection))]
    public partial string CurrentSettingsSection { get; set; } = "General";

    [ObservableProperty]
    public partial SettingOption<UiLanguage>? SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial SettingOption<UiTheme>? SelectedTheme { get; set; }

    [ObservableProperty]
    public partial SettingOption<UiMotionPreference>? SelectedMotionPreference { get; set; }

    [ObservableProperty]
    public partial SettingOption<GitHubDownloadRoute>? SelectedGitHubRoute { get; set; }

    [ObservableProperty]
    public partial bool IsTestingGitHubLatency { get; set; }

    [ObservableProperty]
    public partial string GitHubDirectLatency { get; set; } = "-";

    [ObservableProperty]
    public partial string GitHubMirrorLatency { get; set; } = "-";

    [ObservableProperty]
    public partial string GitHubGhProxyOrgLatency { get; set; } = "-";

    [ObservableProperty]
    public partial string GitHubGhProxyNetLatency { get; set; } = "-";

    [ObservableProperty]
    public partial string GitHubGhFastTopLatency { get; set; } = "-";

    [ObservableProperty]
    public partial string CustomModLinksUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SettingOption<string>? SelectedCustomModLinksBuild { get; set; }

    [ObservableProperty]
    public partial SettingOption<string>? SelectedCustomModLinksLoader { get; set; }

    [ObservableProperty]
    public partial string CustomSourcesText { get; set; } = string.Empty;

    public UiMotionPreference EffectiveMotionPreference => dependencies.GetSettings().MotionPreference;

    public string AccentColor => dependencies.GetSettings().AccentColor;

    [RelayCommand]
    private void SelectSettingsSection(string? section)
    {
        if (!dependencies.GetCanNavigate() || section is not ("General" or "Network" or "Catalog" or "Updates" or "About"))
        {
            return;
        }

        CurrentSettingsSection = section;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task TestGitHubLatencyAsync(CancellationToken cancellationToken)
    {
        IsTestingGitHubLatency = true;
        GitHubDirectLatency = dependencies.Loc()["LatencyTesting"];
        GitHubMirrorLatency = dependencies.Loc()["LatencyTesting"];
        GitHubGhProxyOrgLatency = dependencies.Loc()["LatencyTesting"];
        GitHubGhProxyNetLatency = dependencies.Loc()["LatencyTesting"];
        GitHubGhFastTopLatency = dependencies.Loc()["LatencyTesting"];
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            dependencies.LifetimeCancellation);
        try
        {
            var result = await dependencies.TestGitHubLatency(linkedCancellation.Token);
            IReadOnlyDictionary<GitHubDownloadRoute, GitHubRouteLatencyResult> results =
                result.Routes.ToDictionary(item => item.Route);
            GitHubDirectLatency = FormatGitHubLatency(results, GitHubDownloadRoute.Direct);
            GitHubMirrorLatency = FormatGitHubLatency(results, GitHubDownloadRoute.Mirror);
            GitHubGhProxyOrgLatency = FormatGitHubLatency(results, GitHubDownloadRoute.GhProxyOrg);
            GitHubGhProxyNetLatency = FormatGitHubLatency(results, GitHubDownloadRoute.GhProxyNet);
            GitHubGhFastTopLatency = FormatGitHubLatency(results, GitHubDownloadRoute.GhFastTop);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            GitHubDirectLatency = dependencies.Loc()["LatencyCanceled"];
            GitHubMirrorLatency = dependencies.Loc()["LatencyCanceled"];
            GitHubGhProxyOrgLatency = dependencies.Loc()["LatencyCanceled"];
            GitHubGhProxyNetLatency = dependencies.Loc()["LatencyCanceled"];
            GitHubGhFastTopLatency = dependencies.Loc()["LatencyCanceled"];
        }
        finally
        {
            IsTestingGitHubLatency = false;
        }
    }

    private string FormatGitHubLatency(
        IReadOnlyDictionary<GitHubDownloadRoute, GitHubRouteLatencyResult> results,
        GitHubDownloadRoute route) => results.TryGetValue(route, out GitHubRouteLatencyResult? result)
            ? FormatGitHubLatency(result)
            : dependencies.Loc()["LatencyUnavailable"];

    private string FormatGitHubLatency(GitHubRouteLatencyResult result) => result.Status switch
    {
        GitHubRouteLatencyStatus.Success when result.Latency is { } latency =>
            $"{Math.Max(0, Math.Round(latency.TotalMilliseconds))} ms",
        GitHubRouteLatencyStatus.Timeout => dependencies.Loc()["LatencyTimeout"],
        _ => dependencies.Loc()["LatencyUnavailable"]
    };

    [RelayCommand]
    private async Task SaveCustomSourcesAsync()
    {
        var definitions = new List<CustomCatalogDefinition>();
        try
        {
            foreach (var line in CustomSourcesText.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0
                    || !Uri.TryCreate(line[(separator + 1)..], UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new FormatException($"Invalid custom catalog entry: {line}");
                }
                var sourceNamespace = line[..separator].Trim();
                _ = CustomCatalogSource.Namespace(sourceNamespace, new GameCatalog());
                definitions.Add(new CustomCatalogDefinition
                {
                    Namespace = sourceNamespace,
                    Url = uri.AbsoluteUri
                });
            }

            dependencies.SetSettings(dependencies.GetSettings() with { CustomCatalogs = definitions });
            dependencies.QueueSettingsSave();
            await dependencies.LoadCatalog(CancellationToken.None);
            await dependencies.RefreshAfterCatalogChange();
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            dependencies.SetErrorMessage(exception.Message);
        }
    }

    [RelayCommand]
    private async Task SaveCustomModLinksAsync()
    {
        try
        {
            CustomModLinksDefinition? definition = null;
            if (!string.IsNullOrWhiteSpace(CustomModLinksUrl))
            {
                if (!Uri.TryCreate(CustomModLinksUrl.Trim(), UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps
                    || SelectedCustomModLinksBuild is null
                    || SelectedCustomModLinksLoader is null)
                {
                    throw new FormatException(dependencies.Loc()["CustomModLinksInvalid"]);
                }
                definition = new CustomModLinksDefinition
                {
                    Url = uri.AbsoluteUri,
                    BuildId = SelectedCustomModLinksBuild.Value,
                    LoaderId = SelectedCustomModLinksLoader.Value
                };
            }

            dependencies.SetSettings(dependencies.GetSettings() with { CustomModLinks = definition });
            CustomModLinksUrl = definition?.Url ?? string.Empty;
            dependencies.QueueSettingsSave();
            await dependencies.LoadCatalog(dependencies.LifetimeCancellation);
            RebuildCustomModLinksOptions();
            await dependencies.RefreshAfterCatalogChange();
            dependencies.NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is FormatException
            or ArgumentException
            or IOException
            or InvalidDataException)
        {
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
    }

    internal void RebuildSettingOptions()
    {
        var settings = dependencies.GetSettings();
        SelectedLanguage = null;
        LanguageOptions.Clear();
        LanguageOptions.Add(new(UiLanguage.FollowSystem, dependencies.Loc()["FollowSystem"]));
        LanguageOptions.Add(new(UiLanguage.SimplifiedChinese, dependencies.Loc()["SimplifiedChinese"]));
        LanguageOptions.Add(new(UiLanguage.English, dependencies.Loc()["English"]));
        ThemeOptions.Clear();
        ThemeOptions.Add(new(UiTheme.System, dependencies.Loc()["System"]));
        ThemeOptions.Add(new(UiTheme.Light, dependencies.Loc()["Light"]));
        ThemeOptions.Add(new(UiTheme.Dark, dependencies.Loc()["Dark"]));
        MotionOptions.Clear();
        MotionOptions.Add(new(UiMotionPreference.FollowSystem, dependencies.Loc()["MotionFollowSystem"]));
        MotionOptions.Add(new(UiMotionPreference.Reduced, dependencies.Loc()["MotionReduced"]));
        MotionOptions.Add(new(UiMotionPreference.Off, dependencies.Loc()["MotionOff"]));
        SelectedLanguage = LanguageOptions.First(option => option.Value == settings.Language);
        SelectedTheme = ThemeOptions.First(option => option.Value == settings.Theme);
        SelectedMotionPreference = MotionOptions.First(option => option.Value == settings.MotionPreference);
        RebuildAccentColorOptions();
        RebuildBackgroundScopeOptions();
        GitHubRouteOptions.Clear();
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.Auto, dependencies.Loc()["GitHubAuto"]));
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.Direct, dependencies.Loc()["GitHubDirect"]));
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.GhProxyOrg, dependencies.Loc()["GitHubGhProxyOrg"]));
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.Mirror, dependencies.Loc()["GitHubMirror"]));
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.GhProxyNet, dependencies.Loc()["GitHubGhProxyNet"]));
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.GhFastTop, dependencies.Loc()["GitHubGhFastTop"]));
        SelectedGitHubRoute = GitHubRouteOptions.First(option => option.Value == settings.GitHubDownloadRoute);
        dependencies.RebuildPresetModeOptions();
    }

    internal void RebuildCustomModLinksOptions()
    {
        var settings = dependencies.GetSettings();
        var selectedBuild = settings.CustomModLinks?.BuildId;
        var selectedLoader = settings.CustomModLinks?.LoaderId;
        CustomModLinksBuildOptions.Clear();
        foreach (var build in dependencies.GetCatalog().Builds
                     .OrderBy(build => build.DisplayVersion, StringComparer.OrdinalIgnoreCase))
        {
            CustomModLinksBuildOptions.Add(new(build.Id, build.DisplayVersion));
        }
        CustomModLinksLoaderOptions.Clear();
        foreach (var loader in dependencies.GetCatalog().Loaders
                     .Where(loader => loader.Id.StartsWith("modding-api-", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(loader => loader.Name, StringComparer.OrdinalIgnoreCase))
        {
            CustomModLinksLoaderOptions.Add(new(loader.Id, $"{loader.Name} {loader.Version}"));
        }
        SelectedCustomModLinksBuild = CustomModLinksBuildOptions.FirstOrDefault(option =>
            string.Equals(option.Value, selectedBuild, StringComparison.OrdinalIgnoreCase));
        SelectedCustomModLinksLoader = CustomModLinksLoaderOptions.FirstOrDefault(option =>
            string.Equals(option.Value, selectedLoader, StringComparison.OrdinalIgnoreCase));
    }

    private void RebuildAccentColorOptions()
    {
        var names = new[]
        {
            dependencies.Loc()["AccentBlue"],
            dependencies.Loc()["AccentIndigo"],
            dependencies.Loc()["AccentCrystalPurple"],
            dependencies.Loc()["AccentRose"],
            dependencies.Loc()["AccentOrange"],
            dependencies.Loc()["AccentGreen"],
            dependencies.Loc()["AccentCyan"]
        };
        var selected = AccentColorPalette.Normalize(dependencies.GetSettings().AccentColor);
        AccentColorOptions.Clear();
        for (var index = 0; index < AccentColorPalette.Presets.Count; index++)
        {
            var hex = AccentColorPalette.Presets[index];
            AccentColorOptions.Add(new(
                names[index],
                hex,
                isCustom: false,
                string.Equals(hex, selected, StringComparison.Ordinal)));
        }

        AccentColorOptions.Add(new(
            dependencies.Loc()["AccentCustom"],
            selected,
            isCustom: true,
            !AccentColorPalette.Presets.Contains(selected, StringComparer.Ordinal)));
    }

    private void UpdateAccentColorSelection(string accentColor)
    {
        var isPreset = AccentColorPalette.Presets.Contains(accentColor, StringComparer.Ordinal);
        foreach (var option in AccentColorOptions)
        {
            option.UpdateCustomColor(accentColor);
            option.IsSelected = option.IsCustom
                ? !isPreset
                : string.Equals(option.Hex, accentColor, StringComparison.Ordinal);
        }
    }

    internal void PreviewAccentColor(string accentColor)
    {
        var normalized = AccentColorPalette.Normalize(accentColor);
        AccentThemeResources.Apply(normalized);
        UpdateAccentColorSelection(normalized);
    }

    internal void SetAccentColor(string accentColor)
    {
        var normalized = AccentColorPalette.Normalize(accentColor);
        PreviewAccentColor(normalized);
        if (string.Equals(dependencies.GetSettings().AccentColor, normalized, StringComparison.Ordinal))
        {
            return;
        }

        dependencies.SetSettings(dependencies.GetSettings() with { AccentColor = normalized });
        OnPropertyChanged(nameof(AccentColor));
        dependencies.QueueSettingsSave();
    }

    internal void RestoreAccentColor() => PreviewAccentColor(dependencies.GetSettings().AccentColor);

    partial void OnSelectedLanguageChanged(SettingOption<UiLanguage>? value)
    {
        if (value is null || value.Value == dependencies.GetSettings().Language)
        {
            return;
        }
        dependencies.SetSettings(dependencies.GetSettings() with { Language = value.Value });
        dependencies.ApplyLanguage(value.Value);
        RebuildSettingOptions();
        RebuildCustomModLinksOptions();
        dependencies.RebuildModStatusOptions();
        dependencies.RebuildMarketCatalog();
        dependencies.RebuildInstalledModCatalogProjection();
        dependencies.RefreshGameDirectoryLabels();
        dependencies.NotifyPreflightLabels();
        dependencies.QueueSettingsSave();
    }

    partial void OnSelectedThemeChanged(SettingOption<UiTheme>? value)
    {
        if (value is null || value.Value == dependencies.GetSettings().Theme)
        {
            return;
        }
        dependencies.SetSettings(dependencies.GetSettings() with { Theme = value.Value });
        dependencies.ApplyTheme(value.Value, dependencies.GetSettings().AccentColor);
        dependencies.QueueSettingsSave();
    }

    partial void OnSelectedMotionPreferenceChanged(SettingOption<UiMotionPreference>? value)
    {
        if (value is null || value.Value == dependencies.GetSettings().MotionPreference)
        {
            return;
        }
        dependencies.SetSettings(dependencies.GetSettings() with { MotionPreference = value.Value });
        OnPropertyChanged(nameof(EffectiveMotionPreference));
        dependencies.QueueSettingsSave();
    }

    partial void OnSelectedGitHubRouteChanged(SettingOption<GitHubDownloadRoute>? value)
    {
        if (value is null || value.Value == dependencies.GetSettings().GitHubDownloadRoute)
        {
            return;
        }
        dependencies.SetSettings(dependencies.GetSettings() with { GitHubDownloadRoute = value.Value });
        dependencies.QueueSettingsSave();
        if (value.Value == GitHubDownloadRoute.Auto && !IsTestingGitHubLatency)
        {
            _ = TestGitHubLatencyCommand.ExecuteAsync(null);
        }
    }

    private static bool IsExpectedSettingsException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;
}
