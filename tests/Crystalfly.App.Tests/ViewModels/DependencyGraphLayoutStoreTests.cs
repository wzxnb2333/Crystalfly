using Crystalfly.App.ViewModels.DependencyGraph;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphLayoutStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"crystalfly-graph-layout-{Guid.NewGuid():N}");

    [Fact]
    public async Task Store_round_trips_each_instance_layout_without_cross_contamination()
    {
        var firstPath = Path.Combine(directory, "instance-a", "dependency-graph.layout.json");
        var secondPath = Path.Combine(directory, "instance-b", "dependency-graph.layout.json");
        await DependencyGraphLayoutStore.WriteAsync(firstPath, new DependencyGraphLayout
        {
            Positions = new Dictionary<string, DependencyGraphNodePosition>(StringComparer.OrdinalIgnoreCase)
            {
                ["mod-a"] = new(120, 240)
            }
        });
        await DependencyGraphLayoutStore.WriteAsync(secondPath, new DependencyGraphLayout
        {
            Positions = new Dictionary<string, DependencyGraphNodePosition>(StringComparer.OrdinalIgnoreCase)
            {
                ["mod-b"] = new(360, 480)
            }
        });

        var first = await DependencyGraphLayoutStore.TryReadAsync(firstPath);
        var second = await DependencyGraphLayoutStore.TryReadAsync(secondPath);

        Assert.Equal(new DependencyGraphNodePosition(120, 240), first!.Positions["mod-a"]);
        Assert.False(first.Positions.ContainsKey("mod-b"));
        Assert.Equal(new DependencyGraphNodePosition(360, 480), second!.Positions["mod-b"]);
        Assert.False(second.Positions.ContainsKey("mod-a"));
    }

    [Fact]
    public async Task Store_returns_null_for_corrupted_or_unsupported_layout()
    {
        Directory.CreateDirectory(directory);
        var corrupted = Path.Combine(directory, "corrupted.json");
        await File.WriteAllTextAsync(corrupted, "{not-json");
        var unsupported = Path.Combine(directory, "unsupported.json");
        await File.WriteAllTextAsync(unsupported, "{\"schemaVersion\":999,\"positions\":{}}");

        Assert.Null(await DependencyGraphLayoutStore.TryReadAsync(corrupted));
        Assert.Null(await DependencyGraphLayoutStore.TryReadAsync(unsupported));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
