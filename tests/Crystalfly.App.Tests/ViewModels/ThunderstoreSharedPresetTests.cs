using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.ViewModels;

public sealed partial class ThunderstoreViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_preset_waits_for_initial_catalog_load_and_rechecks_target(bool switchTarget)
    {
        var catalogPath = Path.Combine(root, "loading-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog
        {
            Packages = [Package("Author", "Main", "1.0.0"), Package("silksong_modding", "BepInExPack_Silksong", "1.0.3")]
        });
        var handler = new SharedPresetLoadingHandler();
        using var http = new HttpClient(handler);
        var original = new InstanceRecord { Id = "first", GameId = GameId.Silksong, Name = "First", BuildId = "build", RootPath = Path.Combine(root, "first") };
        var selected = original;
        var vm = new ThunderstoreViewModel(new(), new(http, catalogPath), new(http, Path.Combine(root, "cache")),
            () => selected, () => false, _ => throw new InvalidOperationException("Preview must not install"), () => Task.CompletedTask);
        var loading = vm.EnsureLoadedAsync();
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var import = vm.ImportSharedPresetAsync(original, new("Practice", GameId.Silksong, "build", [new("Author-Main", "1.0.0", false, true)]));
        Assert.False(import.IsCompleted);
        if (switchTarget)
        {
            selected = original with { Id = "second", RootPath = Path.Combine(root, "second") };
            await vm.SetContextAsync();
        }
        handler.Release.SetResult();
        await Task.WhenAll(loading, import);
        Assert.Equal(!switchTarget, vm.HasPlan);
        Assert.Equal(switchTarget ? vm.Loc["TsTargetChanged"] : null, vm.Error);
    }

    [Fact]
    public async Task Shared_preset_previews_saved_version_and_state_before_confirmation()
    {
        var package = Package("Author", "Main", "2.0.0") with
        {
            Versions = [Version("Author", "Main", "2.0.0"), Version("Author", "Main", "1.0.0")]
        };
        var loader = Package("silksong_modding", "BepInExPack_Silksong", "1.0.3");
        var catalogPath = Path.Combine(root, "shared-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [package, loader] });
        using var http = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "target-build", RootPath = Path.Combine(root, "game") };
        DownloadQueueGroup? queued = null;
        var vm = new ThunderstoreViewModel(new(), new(http, catalogPath), new(http, Path.Combine(root, "cache")),
            () => target, () => true, group => { queued = group; return Task.CompletedTask; }, () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.ImportSharedPresetAsync(target, new("Practice", GameId.Silksong, "source-build",
            [new(package.FullName, "1.0.0", false, true)]));

        Assert.Null(vm.Error);
        Assert.True(vm.HasPlan);
        Assert.Null(queued);
        Assert.False(File.Exists(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json")));
        await vm.ConfirmInstallCommand.ExecuteAsync(null);
        Assert.NotNull(queued);
        Assert.Equal(target.Id, queued.TargetInstanceId);
        Assert.Equal(target.BuildId, queued.ThunderstorePlan!.BuildId);
        var planned = Assert.Single(queued.ThunderstorePlan.Packages, item => item.Package.FullName == package.FullName);
        Assert.Equal("1.0.0", planned.Version.VersionNumber);
        Assert.False(planned.DesiredEnabled);
        Assert.True(planned.DesiredPinned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_preset_rejects_wrong_game_or_stale_target(bool wrongGame)
    {
        var catalogPath = Path.Combine(root, "shared-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [Package("Author", "Main", "1.0.0")] });
        using var http = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var original = new InstanceRecord { Id = "first", GameId = GameId.Silksong, Name = "First", BuildId = "build", RootPath = Path.Combine(root, "first") };
        var selected = original with { Id = "second", RootPath = Path.Combine(root, "second") };
        var vm = new ThunderstoreViewModel(new(), new(http, catalogPath), new(http, Path.Combine(root, "cache")),
            () => selected, () => true, _ => throw new InvalidOperationException("Must not install"), () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.ImportSharedPresetAsync(wrongGame ? selected : original,
            new("Practice", wrongGame ? GameId.HollowKnight : GameId.Silksong, "build", [new("Author-Main", "1.0.0", true, false)]));

        Assert.False(vm.HasPlan);
        Assert.Equal(vm.Loc[wrongGame ? "TsInvalidPreset" : "TsTargetChanged"], vm.Error);
    }

    private sealed class SharedPresetLoadingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new(System.Net.HttpStatusCode.NotFound);
        }
    }
}
