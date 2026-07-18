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
    private readonly LoaderManager _loaderManager;
    private readonly ModManager _modManager;

    public ModInstallService(
        InstanceRecord instance,
        IReadOnlyList<ModManifest> catalog,
        LoaderManager loaderManager,
        ModManager modManager)
    {
        _instance = instance;
        _catalog = catalog;
        _loaderManager = loaderManager;
        _modManager = modManager;
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
