using System.Reflection;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Tests.Ui;

public sealed class MainWindowCodeBehindTests : IDisposable
{
    private readonly TemporaryDirectory test = new();

    [Fact]
    public void Safe_instance_folder_requires_existing_regular_directories_below_instance_root()
    {
        var root = test.CreateDirectory("instance");
        var mods = test.CreateDirectory("instance", "Mods");

        Assert.Equal(mods, ResolveSafeInstanceFolder(root, "Mods"));
        Assert.Throws<InvalidOperationException>(() => ResolveSafeInstanceFolder(root, "."));
        Assert.Throws<DirectoryNotFoundException>(() =>
            ResolveSafeInstanceFolder(root, Path.Combine("Mods", "Missing")));
        Assert.False(Directory.Exists(Path.Combine(mods, "Missing")));
    }

    [Fact]
    public void Safe_instance_folder_rejects_reparse_points_in_root_and_each_path_segment()
    {
        var root = test.CreateDirectory("instance");
        var outside = test.CreateDirectory("outside");
        var linked = Path.Combine(root, "linked");
        Directory.CreateSymbolicLink(linked, outside);

        Assert.Throws<IOException>(() => ResolveSafeInstanceFolder(root, "linked"));

        var rootLink = Path.Combine(test.Root, "instance-link");
        Directory.CreateSymbolicLink(rootLink, root);
        Assert.Throws<IOException>(() => ResolveSafeInstanceFolder(rootLink, "linked"));
    }

    [Fact]
    public void Installed_mod_row_handler_returns_focus_to_the_list()
    {
        var repositoryRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));
        var handlerStart = code.IndexOf("private void OnInstalledModPointerPressed", StringComparison.Ordinal);
        var nextHandler = code.IndexOf("private void OnInstalledModsKeyDown", handlerStart, StringComparison.Ordinal);

        Assert.True(handlerStart >= 0 && nextHandler > handlerStart);
        Assert.Contains("InstalledModsList.Focus", code[handlerStart..nextHandler], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dependency_repair_nodes_show_required_by_hierarchy_current_state_and_localized_labels()
    {
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"));
        viewModel.Loc.Apply(UiLanguage.SimplifiedChinese);
        viewModel.InstalledMods.Add(Installed("feature", enabled: true, dependencies: ["middle"]));
        viewModel.InstalledMods.Add(Installed("middle", enabled: false, dependencies: ["base"]));
        var plan = new ModDependencyRepairPlan
        {
            BuildId = "build",
            LoaderId = "modding-api-77",
            Items =
            [
                Repair("base", ModDependencyRepairAction.DownloadAndInstall, ["middle"]),
                Repair("middle", ModDependencyRepairAction.ReEnable, ["feature"])
            ]
        };

        var nodes = BuildDependencyRepairNodes(viewModel, plan);

        Assert.Equal(["feature", "middle", "base"], nodes.Select(node => node.ModId));
        Assert.Equal([0, 1, 2], nodes.Select(node => node.Depth));
        Assert.True(nodes[0].IsTarget);
        Assert.Contains(viewModel.Loc["Enabled"], nodes[0].Status, StringComparison.Ordinal);
        Assert.Contains(viewModel.Loc["Disabled"], nodes[1].Status, StringComparison.Ordinal);
        Assert.Contains(viewModel.Loc["WillReEnable"], nodes[1].Status, StringComparison.Ordinal);
        Assert.Contains(viewModel.Loc["Missing"], nodes[2].Status, StringComparison.Ordinal);
        Assert.Contains(viewModel.Loc["WillDownloadAndInstall"], nodes[2].Status, StringComparison.Ordinal);
        Assert.Equal(viewModel.Loc["Target"], GetNodeLabel(nodes[0], "TargetLabel"));
        Assert.Equal(viewModel.Loc["Unresolved"], GetNodeLabel(nodes[2], "UnresolvedLabel"));
    }

    private static string ResolveSafeInstanceFolder(string root, string relativePath)
    {
        var method = typeof(MainWindow).GetMethod(
            "ResolveSafeInstanceFolder",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            return Assert.IsType<string>(method.Invoke(null, [root, relativePath]));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static IReadOnlyList<DependencyPlanNodeViewModel> BuildDependencyRepairNodes(
        MainViewModel viewModel,
        ModDependencyRepairPlan plan)
    {
        var method = typeof(MainWindow).GetMethod(
            "BuildDependencyRepairNodes",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IReadOnlyList<DependencyPlanNodeViewModel>>(
            method.Invoke(null, [viewModel, plan]));
    }

    private static string GetNodeLabel(DependencyPlanNodeViewModel node, string propertyName)
    {
        var property = typeof(DependencyPlanNodeViewModel).GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<string>(property.GetValue(node));
    }

    private static InstalledModItemViewModel Installed(
        string id,
        bool enabled,
        IReadOnlyList<string> dependencies) => new(
            new InstalledModReceipt
            {
                Id = id,
                Name = id,
                Version = "1.0.0",
                LoaderId = "modding-api-77",
                InstallRoot = $"Mods/{id}",
                Enabled = enabled,
                Dependencies = dependencies
            },
            null,
            static () => { });

    private static ModDependencyRepairPlanItem Repair(
        string id,
        ModDependencyRepairAction action,
        IReadOnlyList<string> requiredBy) => new()
        {
            ModId = id,
            PackageId = id,
            Name = id,
            Version = "1.0.0",
            LoaderId = "modding-api-77",
            Action = action,
            RequiredByModIds = requiredBy,
            Reason = action.ToString()
        };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Crystalfly.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    public void Dispose() => test.Dispose();

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), $"crystalfly-window-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateDirectory(params string[] segments)
        {
            var path = segments.Aggregate(Root, Path.Combine);
            return Directory.CreateDirectory(path).FullName;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
