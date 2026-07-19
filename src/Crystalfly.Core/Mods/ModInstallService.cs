using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public enum ModInstallReadiness
{
    Ready,
    RequiresLoader,
    Blocked
}

public sealed record ModInstallEvaluation
{
    public required ModInstallReadiness Status { get; init; }

    public required string RequiredLoaderId { get; init; }

    public required LoaderInspection Loader { get; init; }

    public required string Reason { get; init; }
}

public sealed class ModInstallService
{
    private readonly InstanceRecord _instance;
    private readonly IReadOnlyList<ModManifest> _catalog;
    private readonly IReadOnlyList<LoaderManifest> _loaders;
    private readonly LoaderManager _loaderManager;
    private readonly ModManager _modManager;

    public ModInstallService(
        InstanceRecord instance,
        IReadOnlyList<ModManifest> catalog,
        IReadOnlyList<LoaderManifest> loaders,
        LoaderManager loaderManager,
        ModManager modManager)
    {
        _instance = instance;
        _catalog = catalog;
        _loaders = loaders;
        _loaderManager = loaderManager;
        _modManager = modManager;
    }

    public async Task<ModInstallPlan> CreatePlanAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = ModDependencyResolver.ResolveInstallOrder(_catalog, [modId]);
        var evaluation = await EvaluateAsync(modId, cancellationToken);
        var installed = (await _modManager.GetInstalledAsync(cancellationToken))
            .ToDictionary(receipt => receipt.Id, StringComparer.OrdinalIgnoreCase);
        var loaderManifest = _loaders.FirstOrDefault(loader =>
            string.Equals(loader.Id, evaluation.RequiredLoaderId, StringComparison.OrdinalIgnoreCase)
            && loader.SupportedBuildIds.Contains(_instance.BuildId, StringComparer.OrdinalIgnoreCase));
        var blockedReason = evaluation.Status == ModInstallReadiness.Blocked
            ? evaluation.Reason
            : loaderManifest is null && _loaders.Count != 0
                ? $"Loader '{evaluation.RequiredLoaderId}' is not available for build '{_instance.BuildId}'."
                : null;
        var items = new List<ModInstallPlanItem>(order.Count + 1)
        {
            new()
            {
                Kind = ModInstallPlanItemKind.Loader,
                State = blockedReason is not null
                    ? ModInstallPlanItemState.Blocked
                    : evaluation.Status == ModInstallReadiness.RequiresLoader
                        ? ModInstallPlanItemState.NeedsInstall
                        : ModInstallPlanItemState.Satisfied,
                Id = evaluation.RequiredLoaderId,
                Name = loaderManifest?.Name ?? evaluation.RequiredLoaderId,
                Version = loaderManifest?.Version ?? evaluation.Loader.Version ?? string.Empty,
                LoaderId = evaluation.RequiredLoaderId,
                Reason = blockedReason
                    ?? (evaluation.Status == ModInstallReadiness.RequiresLoader
                        ? evaluation.Reason
                        : $"Loader '{evaluation.RequiredLoaderId}' is already installed.")
            }
        };

        foreach (var manifest in order)
        {
            var state = ModInstallPlanItemState.NeedsInstall;
            var reason = $"Mod '{manifest.Id}' version '{manifest.Version}' will be installed.";
            if (blockedReason is not null)
            {
                state = ModInstallPlanItemState.Blocked;
                reason = blockedReason;
            }
            else if (installed.TryGetValue(manifest.Id, out var receipt))
            {
                if (receipt.IsLocal)
                {
                    state = ModInstallPlanItemState.Blocked;
                    reason = $"Local mod '{manifest.Id}' cannot be updated automatically.";
                }
                else if (!string.Equals(receipt.LoaderId, manifest.LoaderId, StringComparison.OrdinalIgnoreCase))
                {
                    state = ModInstallPlanItemState.Blocked;
                    reason = $"Installed mod '{manifest.Id}' uses loader '{receipt.LoaderId}'.";
                }
                else
                {
                    state = receipt.Enabled
                        && string.Equals(receipt.Version, manifest.Version, StringComparison.OrdinalIgnoreCase)
                        ? ModInstallPlanItemState.Satisfied
                        : ModInstallPlanItemState.NeedsUpdate;
                    reason = state == ModInstallPlanItemState.Satisfied
                        ? $"Mod '{manifest.Id}' version '{manifest.Version}' is already installed."
                        : string.Equals(receipt.Version, manifest.Version, StringComparison.OrdinalIgnoreCase)
                            ? $"Mod '{manifest.Id}' version '{manifest.Version}' will be enabled."
                        : $"Mod '{manifest.Id}' will be updated to version '{manifest.Version}'.";
                }
            }

            items.Add(new ModInstallPlanItem
            {
                Kind = string.Equals(manifest.Id, modId, StringComparison.OrdinalIgnoreCase)
                    ? ModInstallPlanItemKind.Mod
                    : ModInstallPlanItemKind.Dependency,
                State = state,
                Id = manifest.Id,
                Name = manifest.DisplayName ?? manifest.Name,
                Version = manifest.Version,
                LoaderId = manifest.LoaderId,
                Reason = reason
            });
        }

        return new ModInstallPlan
        {
            ModId = modId,
            InstanceId = _instance.Id,
            InstanceName = _instance.Name,
            Items = items
        };
    }

    public async Task<ModInstallEvaluation> EvaluateAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        var order = ModDependencyResolver.ResolveInstallOrder(_catalog, [modId]);
        var requiredLoaderId = order[^1].LoaderId;
        var loader = await _loaderManager.InspectAsync(cancellationToken);
        string? incompatibility = _instance.Purpose == InstancePurpose.OfficialSpeedrun
            ? "Official speedrun instances cannot be modified."
            : string.Equals(_instance.BuildId, "unknown", StringComparison.OrdinalIgnoreCase)
                ? "Unknown game builds are restricted to vanilla mode."
                : order.FirstOrDefault(manifest =>
                    !string.Equals(manifest.LoaderId, requiredLoaderId, StringComparison.OrdinalIgnoreCase)
                    || !manifest.SupportedBuildIds.Contains(_instance.BuildId, StringComparer.OrdinalIgnoreCase)) is { } incompatible
                    ? $"Mod '{incompatible.Id}' is not compatible with build '{_instance.BuildId}' and loader '{requiredLoaderId}'."
                    : null;
        var status = incompatibility is not null
            ? ModInstallReadiness.Blocked
            : loader.State == LoaderState.Vanilla
                ? ModInstallReadiness.RequiresLoader
                : loader.State is LoaderState.ModdingApi or LoaderState.BepInEx
                    && string.Equals(loader.PackageId, requiredLoaderId, StringComparison.OrdinalIgnoreCase)
                    ? ModInstallReadiness.Ready
                    : ModInstallReadiness.Blocked;
        return new ModInstallEvaluation
        {
            Status = status,
            RequiredLoaderId = requiredLoaderId,
            Loader = loader,
            Reason = incompatibility ?? (status == ModInstallReadiness.RequiresLoader
                ? $"Loader '{requiredLoaderId}' must be installed first."
                : status == ModInstallReadiness.Ready
                    ? string.Empty
                    : $"Installed loader '{loader.PackageId ?? loader.State.ToString()}' does not match '{requiredLoaderId}'.")
        };
    }

    public async Task<IReadOnlyList<InstalledModReceipt>> InstallAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        await RequireReadyAsync(modId, cancellationToken);
        return await _modManager.InstallWithDependenciesFromUrisAsync(_catalog, [modId], cancellationToken);
    }

    public async Task<InstalledModReceipt> UpdateAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        await RequireReadyAsync(modId, cancellationToken);
        var manifest = _catalog.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, modId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Mod '{modId}' was not found.");
        return await _modManager.UpdateFromUriAsync(manifest, cancellationToken);
    }

    private async Task RequireReadyAsync(string modId, CancellationToken cancellationToken)
    {
        var evaluation = await EvaluateAsync(modId, cancellationToken);
        if (evaluation.Status != ModInstallReadiness.Ready)
        {
            throw new InvalidOperationException(
                $"Mod '{modId}' cannot be installed with loader state '{evaluation.Loader.State}'.");
        }
    }
}
