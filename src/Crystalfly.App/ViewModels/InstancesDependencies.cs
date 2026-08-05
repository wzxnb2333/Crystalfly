using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.App.ViewModels;

internal sealed record InstancesDependencies(
    Func<LocalizationViewModel> Loc,
    Func<CrystalflySettings> GetSettings,
    Action<CrystalflySettings> SetSettings,
    Action QueueSettingsSave,
    Func<Task> RefreshInstances,
    Func<Task> RefreshInstancesQuietly,
    Func<bool> GetCanNavigate,
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<bool> IsMutationBlocked,
    Func<string, Func<CancellationToken, Task>, CancellationToken, Task> RunCoordinated,
    Func<string, CancellationToken, ValueTask<InstanceDeletionConditions>> EvaluateDeletionConditions,
    Func<
        InstanceRecord,
        Func<CancellationToken, ValueTask<InstanceDeletionConditions>>,
        CancellationToken,
        Task<InstanceDeletionResult>>? InstanceDeletionOverride,
    Func<InstanceRecord, Task<bool>> IsVanillaInstanceAsync,
    Func<string, string> GetVersionDataRoot,
    Func<string> GetVersionRoot,
    Action<string> SetVersionRoot,
    Func<GameCatalog> GetCatalog,
    Func<InstanceItemViewModel?> GetSelectedInstance,
    Action<InstanceItemViewModel?> SetSelectedInstance,
    Func<InstanceItemViewModel?> GetSelectedSpeedrunInstance,
    Action<InstanceItemViewModel?> SetSelectedSpeedrunInstance,
    Action<string?> SetErrorMessage,
    Action<string> SetStatusMessage,
    Action<string> SetCurrentPage,
    Action<string> SetCurrentManageTab,
    Func<string> GetCloneInstanceName,
    Action<string> SetCloneInstanceName,
    Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>> DiscoverInstances,
    Func<string> GetSearchText,
    Func<string, bool> HasBlockingDownloadForPath,
    Action NotifyOperationCompleted,
    bool AutoRequestGameDirectoryDiscovery,
    CancellationToken LifetimeCancellation);
