using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphViewModelTests
{
    [Fact]
    public void Nodes_start_collapsed_and_expand_command_expands_selected_node()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsExpanded("a"));
        Assert.False(viewModel.IsExpanded("b"));

        viewModel.ExpandNodeCommand.Execute("a");

        Assert.True(viewModel.IsExpanded("a"));
        Assert.False(viewModel.IsExpanded("b"));
    }

    [Fact]
    public void Collapse_command_collapses_an_expanded_node()
    {
        var viewModel = CreateViewModel();
        viewModel.ExpandNodeCommand.Execute("b");

        viewModel.CollapseNodeCommand.Execute("b");

        Assert.False(viewModel.IsExpanded("b"));
    }

    [Fact]
    public void Collapse_command_ignores_unknown_ids()
    {
        var viewModel = CreateViewModel();
        viewModel.ExpandNodeCommand.Execute("a");

        viewModel.CollapseNodeCommand.Execute("missing");

        Assert.True(viewModel.IsExpanded("a"));
    }

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
    public void Graph_selection_keeps_expansion_state_intact()
    {
        var viewModel = CreateViewModel();
        viewModel.ExpandNodeCommand.Execute("a");

        viewModel.SelectNode("b");

        Assert.True(viewModel.IsExpanded("a"));
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
