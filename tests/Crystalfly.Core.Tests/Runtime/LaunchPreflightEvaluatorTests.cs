using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Tests.Runtime;

public sealed class LaunchPreflightEvaluatorTests
{
    [Theory]
    [InlineData(LoaderState.Conflict)]
    [InlineData(LoaderState.Drifted)]
    public void Conflict_or_drifted_loader_blocks_launch(LoaderState loaderState)
    {
        var result = LaunchPreflightEvaluator.Evaluate(
            isKnownBuild: true,
            executableExists: true,
            loaderState,
            [],
            transactionsHealthy: true,
            localLowReady: true);

        Assert.False(result.LoaderReady);
        Assert.False(result.IsReady);
    }

    [Fact]
    public void Unknown_build_only_allows_vanilla_loader()
    {
        var vanilla = LaunchPreflightEvaluator.Evaluate(false, true, LoaderState.Vanilla, [], true, true);
        var modded = LaunchPreflightEvaluator.Evaluate(false, true, LoaderState.BepInEx, [], true, true);

        Assert.True(vanilla.IsReady);
        Assert.False(modded.LoaderReady);
        Assert.False(modded.IsReady);
    }

    [Fact]
    public void Missing_or_disabled_dependency_blocks_launch()
    {
        var mod = Receipt("feature", enabled: true, dependencies: ["library"]);
        var disabledLibrary = Receipt("library", enabled: false);

        var missing = LaunchPreflightEvaluator.Evaluate(true, true, LoaderState.ModdingApi, [mod], true, true);
        var disabled = LaunchPreflightEvaluator.Evaluate(
            true,
            true,
            LoaderState.ModdingApi,
            [mod, disabledLibrary],
            true,
            true);

        Assert.False(missing.DependenciesReady);
        Assert.False(disabled.DependenciesReady);
    }

    [Fact]
    public void Healthy_instance_passes_all_checks()
    {
        var library = Receipt("library", enabled: true);
        var mod = Receipt("feature", enabled: true, dependencies: ["library"]);

        var result = LaunchPreflightEvaluator.Evaluate(
            true,
            true,
            LoaderState.ModdingApi,
            [mod, library],
            true,
            true);

        Assert.True(result.GameFilesReady);
        Assert.True(result.LoaderReady);
        Assert.True(result.DependenciesReady);
        Assert.True(result.SaveIsolationReady);
        Assert.True(result.IsReady);
    }

    [Fact]
    public void Running_game_process_blocks_preflight()
    {
        var result = LaunchPreflightEvaluator.Evaluate(
            true,
            true,
            LoaderState.Vanilla,
            [],
            true,
            true,
            gameProcessRunning: true);

        Assert.False(result.GameFilesReady);
        Assert.False(result.IsReady);
    }

    private static InstalledModReceipt Receipt(
        string id,
        bool enabled,
        IReadOnlyList<string>? dependencies = null) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        LoaderId = "modding-api",
        InstallRoot = $"Mods/{id}",
        Enabled = enabled,
        Dependencies = dependencies ?? []
    };
}
