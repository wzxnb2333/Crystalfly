using System.Net;
using System.Reflection;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class GameSwitchPerformanceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.GameSwitch", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(GameId.HollowKnight, GameId.Silksong)]
    [InlineData(GameId.Silksong, GameId.HollowKnight)]
    public async Task Switching_a_shared_library_does_not_inspect_or_register_the_other_game(GameId original, GameId target)
    {
        var library = Directory.CreateDirectory(Path.Combine(root, "library")).FullName;
        var app = Path.Combine(root, "app");
        await CrystalflySettingsStore.SaveAsync(Path.Combine(app, "settings.json"), new()
        {
            OfflineMode = true, CheckForUpdates = false, OnboardingCompleted = true, SelectedGameId = original,
            GameDirectories = [new() { GameId = original, Path = library, DisplayName = "Original" },
                new() { GameId = target, Path = library, DisplayName = "Target" }],
            GameWorkspaces = [new() { GameId = original, VersionRoot = library }, new() { GameId = target, VersionRoot = library }]
        });
        await using var model = new MainViewModel(app);
        await model.InitializeAsync();
        await ((Task)typeof(MainViewModel).GetField("catalogRefreshTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!)
            .WaitAsync(TimeSpan.FromSeconds(10));
        var originalPath = CreateGame(original);
        var targetPath = CreateGame(target);

        await model.SelectGameCommand.ExecuteAsync(target.ToString());

        Assert.Equal(targetPath, Assert.Single(model.Instances.Instances).RootPath);
        Assert.False(File.Exists(InstanceSidecar.GetMarkerPath(originalPath)), "Switching must not read and register instances of the inactive game.");
        await model.SelectGameCommand.ExecuteAsync(original.ToString());
        Assert.Equal(originalPath, Assert.Single(model.Instances.Instances).RootPath);
    }

    [Fact]
    public async Task Market_network_wait_does_not_block_switching_and_returning_reuses_the_catalog()
    {
        var app = Path.Combine(root, "app");
        await CrystalflySettingsStore.SaveAsync(Path.Combine(app, "settings.json"), new()
        {
            OfflineMode = true, CheckForUpdates = false, OnboardingCompleted = true
        });
        await using var model = new MainViewModel(app);
        await model.InitializeAsync();
        using var handler = new DelayedCatalogHandler();
        using var client = new HttpClient(handler);
        var target = new InstanceRecord { GameId = GameId.Silksong, Id = "silk", Name = "Silk", BuildId = "unknown", RootPath = Path.Combine(root, "silk") };
        var market = new ThunderstoreViewModel(model.Loc, new(client, Path.Combine(root, "catalog.json")),
            new(client, Path.Combine(root, "packages")), () => model.IsSilksong ? target : null, () => false,
            _ => Task.CompletedTask, () => Task.CompletedTask);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        market.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ThunderstoreViewModel.IsLoading) && !market.IsLoading) loaded.TrySetResult();
        };
        typeof(MainViewModel).GetField("thunderstore", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, market);
        model.CurrentPage = "Downloads";
        model.CurrentDownloadSection = "ModMarket";
        var switching = model.ToggleGameCommand.ExecuteAsync(null);
        try
        {
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await switching.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(model.IsSwitchingGame);
            Assert.True(model.CanNavigate);
            Assert.True(market.IsLoading);
            await market.SetContextAsync();
            await model.ToggleGameCommand.ExecuteAsync(null);
            Assert.Equal(GameId.HollowKnight, model.SelectedGameId);
            await market.SetContextAsync();
        }
        finally
        {
            handler.Release.TrySetResult();
            await switching.WaitAsync(TimeSpan.FromSeconds(10));
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Null(market.Error);
        Assert.Equal("Author-Example", Assert.Single(market.Packages).FullName);
        var requests = handler.RequestCount;
        await model.ToggleGameCommand.ExecuteAsync(null);
        Assert.Equal(GameId.Silksong, model.SelectedGameId);
        Assert.Equal(requests, handler.RequestCount);
    }

    private string CreateGame(GameId gameId)
    {
        var game = GameDefinition.Get(gameId);
        var path = Path.Combine(root, "library", gameId.ToString());
        Directory.CreateDirectory(Path.Combine(path, game.DataDirectoryName));
        File.WriteAllText(Path.Combine(path, game.ExecutableName), "exe");
        File.WriteAllText(Path.Combine(path, "UnityPlayer.dll"), "unity");
        File.WriteAllText(Path.Combine(path, game.GlobalGameManagersPath), "data");
        return path;
    }

    private sealed class DelayedCatalogHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return request.RequestUri!.AbsoluteUri == ThunderstoreCatalogSource.IndexUrl
                ? new(HttpStatusCode.NotFound)
                : new(HttpStatusCode.OK) { Content = new StringContent("""
                    [{"name":"Example","owner":"Author","full_name":"Author-Example","versions":[
                        {"name":"Example","full_name":"Author-Example-1.0.0","version_number":"1.0.0","file_size":100,
                         "download_url":"https://thunderstore.io/package/download/Author/Example/1.0.0/"}]}]
                    """) };
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
