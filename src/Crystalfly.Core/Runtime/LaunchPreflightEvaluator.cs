using Crystalfly.Core.Models;

namespace Crystalfly.Core.Runtime;

public sealed record LaunchPreflightResult(
    bool GameFilesReady,
    bool LoaderReady,
    bool DependenciesReady,
    bool SaveIsolationReady)
{
    public bool IsReady => GameFilesReady && LoaderReady && DependenciesReady && SaveIsolationReady;
}

public static class LaunchPreflightEvaluator
{
    public static LaunchPreflightResult Evaluate(
        bool isKnownBuild,
        bool executableExists,
        LoaderState loaderState,
        IReadOnlyList<InstalledModReceipt> installedMods,
        bool transactionsHealthy,
        bool localLowReady,
        bool gameProcessRunning = false)
    {
        ArgumentNullException.ThrowIfNull(installedMods);
        var installedById = installedMods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        bool dependenciesReady = installedMods
            .Where(mod => mod.Enabled)
            .All(mod => mod.Dependencies.All(dependency =>
                installedById.TryGetValue(dependency, out var installed) && installed.Enabled));
        bool loaderReady = loaderState is not LoaderState.Conflict and not LoaderState.Drifted
            && (isKnownBuild || loaderState == LoaderState.Vanilla);

        return new LaunchPreflightResult(
            executableExists && !gameProcessRunning,
            loaderReady,
            dependenciesReady,
            transactionsHealthy && localLowReady);
    }
}
