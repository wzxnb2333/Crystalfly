using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.Core.Models;
using System.ComponentModel;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Graph_includes_same_version_catalog_dependencies_even_without_a_receipt(bool external)
    {
        var installed = Mod("feature");
        var manifest = Manifest("feature", "1.0.0", ["core"]);
        var item = new InstalledModItemViewModel(installed.Discovery, external ? null : installed.Receipt,
            installed.HealthReport, manifest, static () => { });
        var viewModel = new DependencyGraphViewModel(Dependencies());

        viewModel.Rebuild([item, Mod("core")], "feature", null);

        var edge = Assert.Single(viewModel.Graph.Edges);
        Assert.Equal("core", edge.Source.Id);
        Assert.Equal("feature", edge.Target.Id);
    }

    [Fact]
    public void Graph_preserves_installed_dependencies_without_using_a_newer_versions_requirements()
    {
        var installed = Mod("feature", ["old-core"]);
        var manifest = Manifest("feature", "2.0.0", ["new-core"]);
        var item = new InstalledModItemViewModel(installed.Discovery, installed.Receipt,
            installed.HealthReport, manifest, static () => { });
        var viewModel = new DependencyGraphViewModel(Dependencies());

        viewModel.Rebuild([item], "feature", null);

        Assert.Equal("old-core", Assert.Single(viewModel.Graph.Edges).Source.Id);
        Assert.DoesNotContain(viewModel.Graph.Nodes, node => node.Id == "new-core");
    }

    [Fact]
    public void Graph_expands_missing_dependencies_recursively_without_looping()
    {
        var catalog = new[]
        {
            Manifest("core", "1.0.0", ["base"]),
            Manifest("base", "1.0.0", ["core"])
        };
        var viewModel = new DependencyGraphViewModel(Dependencies() with
        {
            FindMarketMod = id => catalog.FirstOrDefault(mod => mod.Id == id) is { } manifest
                ? new MarketModItemViewModel(manifest, null, new Dictionary<string, string>(), false) : null
        });

        viewModel.Rebuild([Mod("feature", ["core"])], "feature", null);

        Assert.Equal(3, viewModel.Graph.Nodes.Count);
        Assert.Equal(3, viewModel.Graph.Edges.Count);
        Assert.Contains("base", viewModel.MissingNodeIds);
    }

    private static ModManifest Manifest(string id, string version, string[] dependencies) => new()
    {
        Id = id, Name = id, Version = version, Dependencies = dependencies,
        LoaderId = "modding-api", DownloadUrl = "https://example.invalid/mod.zip", Sha256 = new string('0', 64)
    };

    private static DependencyGraphViewModel CreateViewModel()
    {
        var viewModel = new DependencyGraphViewModel(Dependencies());
        viewModel.Rebuild(
        [
            Mod("a"),
            Mod("b", dependencies: ["a"]),
            Mod("c", dependencies: ["b"])
        ],
        selectedId: null,
        instanceId: null);
        return viewModel;
    }

    private static DependencyGraphDependencies Dependencies() => new(
        () => new LocalizationViewModel(),
        _ => null,
        _ => Path.Combine(Path.GetTempPath(), "crystalfly-test", "graph.layout.json"),
        () => null,
        _ => { });

    private static InstalledModItemViewModel Mod(string id, string[]? dependencies = null)
    {
        var receipt = new InstalledModReceipt
        {
            Id = id,
            Name = $"Mod {id}",
            Version = "1.0.0",
            LoaderId = "modding-api",
            InstallRoot = $"Mods/{id}",
            Enabled = true,
            Dependencies = dependencies ?? []
        };
        var discovery = new ModDiscoveryEntry
        {
            Id = receipt.Id,
            Name = receipt.Name,
            LoaderId = receipt.LoaderId,
            InstallRoot = receipt.InstallRoot,
            Enabled = receipt.Enabled,
            Ownership = ModOwnership.Managed,
            Files = receipt.Files.Select(file => file.RelativePath).ToArray(),
            EntryFiles = receipt.EntryFiles
        };
        return new InstalledModItemViewModel(
            discovery,
            receipt,
            new ModHealthReport { ModId = receipt.Id, Status = ModHealthStatus.Healthy },
            null,
            static () => { });
    }
}
