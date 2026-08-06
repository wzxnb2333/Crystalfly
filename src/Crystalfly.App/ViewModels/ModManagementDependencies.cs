using Crystalfly.Core.Catalog;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.ViewModels;

public sealed record ModManagementDependencies(
    Func<GameCatalog> GetCatalog,
    Func<LocalizationViewModel> GetLoc,
    Action<string> ToastRequested,
    CancellationToken LifetimeCancellation,
    Func<Func<InstanceRecord, Task>, Task> RunInstanceMutation,
    Func<InstanceRecord, ModManager> CreateModManager,
    Func<InstanceRecord, LoaderManager> CreateLoaderManager,
    Func<InstanceRecord, ModInstallService> CreateModInstallService,
    Func<ModManifest, bool?, MarketModItemViewModel> ProjectMarketMod,
    Func<string, MarketModItemViewModel?> FindMarketMod,
    Func<ModOwnership, string> OwnershipDisplay,
    Func<ModHealthStatus, string> HealthDisplay,
    Action<string?> SetErrorMessage,
    Func<InstanceRecord?> GetSelectedInstance,
    Func<ModDependencyRepairPlan, Task> EnqueueModDependencyRepair);
