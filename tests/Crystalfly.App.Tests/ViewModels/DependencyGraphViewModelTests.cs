using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.Core.Models;
using System.ComponentModel;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphViewModelTests
{

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
