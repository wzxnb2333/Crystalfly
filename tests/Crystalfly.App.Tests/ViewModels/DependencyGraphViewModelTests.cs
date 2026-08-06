using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.Core.Models;
using System.ComponentModel;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphViewModelTests
{
    [Fact]
    public void Missing_node_ids_are_exposed_from_graph()
    {
        var viewModel = new DependencyGraphViewModel(Dependencies());
        viewModel.Rebuild(
        [
            Mod("feature", dependencies: ["library"])
        ],
        selectedId: null,
        instanceId: null);

        Assert.Equal(["library"], viewModel.MissingNodeIds);
    }

    [Fact]
    public void Select_node_tracks_the_selected_id()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectNode("b");

        Assert.Equal("b", viewModel.SelectedNodeId);
    }

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

    [Fact]
    public void Rebuild_raises_property_changed_for_graph_so_the_view_binding_updates()
    {
        var viewModel = new DependencyGraphViewModel(Dependencies());
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        viewModel.Rebuild(
            [Mod("a"), Mod("b", dependencies: ["a"])],
            selectedId: null,
            instanceId: null);

        // The view binds DataContext="{Binding DependencyGraph.Graph}"; without the
        // change notification the binding keeps the initial empty graph ("暂无依赖关系").
        Assert.Contains(nameof(DependencyGraphViewModel.Graph), changed);
        Assert.True(viewModel.Graph.HasNodes);
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
