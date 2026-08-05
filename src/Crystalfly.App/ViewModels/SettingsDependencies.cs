using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;

namespace Crystalfly.App.ViewModels;

internal sealed record SettingsDependencies(
    Func<LocalizationViewModel> Loc,
    Func<CrystalflySettings> GetSettings,
    Action<CrystalflySettings> SetSettings,
    Action QueueSettingsSave,
    Func<CrystalflySettings, CancellationToken, Task> SaveSettingsImmediately,
    Func<Task> FlushSettingsSavesAsync,
    Action<UiLanguage> ApplyLanguage,
    Action<UiTheme, string> ApplyTheme,
    Func<GameCatalog> GetCatalog,
    Func<CancellationToken, Task<GameCatalog>> LoadCatalog,
    Func<Task> RefreshAfterCatalogChange,
    Action RebuildModStatusOptions,
    Action RebuildMarketCatalog,
    Action RebuildInstalledModCatalogProjection,
    Action RefreshGameDirectoryLabels,
    Action NotifyPreflightLabels,
    Action RebuildPresetModeOptions,
    Action NotifyOperationCompleted,
    Func<CancellationToken, Task<GitHubRouteLatencyTestResult>> TestGitHubLatency,
    Func<bool> GetCanNavigate,
    Func<InstanceItemViewModel?> GetSelectedInstance,
    Func<string, string> GetInstanceStateRoot,
    Func<string> GetVersionRoot,
    string ApplicationDataRoot,
    Action<string?> SetErrorMessage,
    CancellationToken LifetimeCancellation);
