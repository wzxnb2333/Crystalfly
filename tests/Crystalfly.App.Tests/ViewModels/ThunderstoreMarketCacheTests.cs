using System.Net;
using System.Text.Json;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.ViewModels;

public sealed partial class ThunderstoreViewModelTests
{
    [Fact]
    public async Task Restoring_filters_publishes_once_and_instance_refresh_keeps_the_market_list()
    {
        var path = Path.Combine(root, "filter-cache.json");
        await AtomicJsonStore.WriteAsync(path, new ThunderstoreCatalog
        {
            Packages = [Package("Author", "Keep", "1.0.0"), Package("Author", "Other", "1.0.0")]
        });
        var target = new Crystalfly.Core.Models.InstanceRecord
        {
            Id = "silk", GameId = Crystalfly.Core.Models.GameId.Silksong, Name = "Silk",
            RootPath = Path.Combine(root, "game"), BuildId = "unknown"
        };
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network expected")));
        var vm = new ThunderstoreViewModel(new(), new(client, path), new(client, Path.Combine(root, "packages")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        await vm.EnsureLoadedAsync();
        Assert.Null(vm.Error);
        var resets = 0;
        vm.Packages.CollectionChanged += (_, args) =>
        {
            if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++;
        };
        vm.RestoreFilters(null, 2, true, "Keep");
        Assert.Equal(1, resets);
        var selected = Assert.Single(vm.Packages);
        Assert.Equal("Author-Keep", selected.FullName);
        await vm.SetContextAsync();
        Assert.Null(vm.Error);
        Assert.Equal(1, resets);
        Assert.Same(selected, Assert.Single(vm.Packages));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Market_shows_cache_before_network_finishes_and_retains_it_on_failure(bool fail)
    {
        var path = Path.Combine(root, "market-cache.json");
        await AtomicJsonStore.WriteAsync(path, new ThunderstoreCatalog { Packages = [Package("Author", "Cached", "1.0.0")] });
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new MarketCacheHandler(async (request, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            if (fail) return new(HttpStatusCode.ServiceUnavailable);
            var json = request.RequestUri!.AbsoluteUri == ThunderstoreCatalogSource.IndexUrl
                ? JsonSerializer.Serialize(new[] { "https://ccdn.thunderstore.io/chunk.json" })
                : JsonSerializer.Serialize(new[] { Package("Author", "Updated", "2.0.0") });
            return new(HttpStatusCode.OK) { Content = new StringContent(json) };
        }));
        var vm = new ThunderstoreViewModel(new(), new(client, path), new(client, Path.Combine(root, "packages")),
            () => null, () => false, _ => Task.CompletedTask, () => Task.CompletedTask);
        var refresh = vm.EnsureLoadedAsync();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(refresh.IsCompleted);
            Assert.True(vm.IsLoading);
            Assert.Equal("Author-Cached", Assert.Single(vm.Packages).FullName);
            vm.SearchText = "no match";
            Assert.Empty(vm.Packages);
            vm.SearchText = "";
            Assert.Single(vm.Packages);
        }
        finally { release.TrySetResult(); }
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(vm.IsLoading);
        Assert.Null(vm.Error);
        Assert.Equal(fail ? "Author-Cached" : "Author-Updated", Assert.Single(vm.Packages).FullName);
    }

    private sealed class MarketCacheHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
