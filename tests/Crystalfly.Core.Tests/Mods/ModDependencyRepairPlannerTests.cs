using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModDependencyRepairPlannerTests
{
    [Fact]
    public void Creates_dependency_first_plan_for_disabled_and_downloadable_dependencies()
    {
        var installed = new[]
        {
            Receipt("feature", enabled: true, dependencies: ["middle"]),
            Receipt("middle", enabled: false, dependencies: ["base"])
        };
        var catalog = new[]
        {
            Manifest("middle", dependencies: ["base"]),
            Manifest("base")
        };

        var plan = ModDependencyRepairPlanner.CreatePlan(
            installed, catalog, "1.5.78.11833", "modding-api-77");

        Assert.Equal(["base", "middle"], plan.Items.Select(item => item.ModId));
        Assert.Equal(ModDependencyRepairAction.DownloadAndInstall, plan.Items[0].Action);
        Assert.Equal(ModDependencyRepairAction.ReEnable, plan.Items[1].Action);
        Assert.Equal("base", plan.Items[0].PackageId);
        Assert.False(plan.HasUnresolved);
    }

    [Fact]
    public void Missing_or_incompatible_catalog_entries_are_unresolved()
    {
        var installed = new[]
        {
            Receipt("feature", enabled: true, dependencies: ["missing", "wrong-build", "wrong-loader"])
        };
        var catalog = new[]
        {
            Manifest("wrong-build") with { SupportedBuildIds = ["1.4.3.2"] },
            Manifest("wrong-loader") with { LoaderId = "bepinex-5.4.23.4" }
        };

        var plan = ModDependencyRepairPlanner.CreatePlan(
            installed, catalog, "1.5.78.11833", "modding-api-77");

        Assert.Equal(3, plan.Items.Count);
        Assert.All(plan.Items, item => Assert.Equal(ModDependencyRepairAction.Unresolved, item.Action));
        Assert.True(plan.HasUnresolved);
    }

    [Fact]
    public void Circular_catalog_dependencies_are_reported_once_and_do_not_throw()
    {
        var installed = new[] { Receipt("feature", enabled: true, dependencies: ["a"]) };
        var catalog = new[]
        {
            Manifest("a", dependencies: ["b"]),
            Manifest("b", dependencies: ["a"])
        };

        var plan = ModDependencyRepairPlanner.CreatePlan(
            installed, catalog, "1.5.78.11833", "modding-api-77");

        Assert.Equal(["a", "b"], plan.Items.Select(item => item.ModId).Order());
        Assert.All(plan.Items, item => Assert.Equal(ModDependencyRepairAction.Unresolved, item.Action));
    }

    [Fact]
    public void Enabled_but_transitively_broken_dependency_is_inspected()
    {
        var installed = new[]
        {
            Receipt("feature", enabled: true, dependencies: ["middle"]),
            Receipt("middle", enabled: true, dependencies: ["base"])
        };

        var plan = ModDependencyRepairPlanner.CreatePlan(
            installed, [Manifest("base")], "1.5.78.11833", "modding-api-77");

        var item = Assert.Single(plan.Items);
        Assert.Equal("base", item.ModId);
        Assert.Equal(ModDependencyRepairAction.DownloadAndInstall, item.Action);
        Assert.Equal(["middle"], item.RequiredByModIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_enabled_dependency_keeps_its_full_unrepairable_result_across_root_order(bool reverse)
    {
        var installed = new[]
        {
            Receipt("first-root", enabled: true, dependencies: ["broken-branch"]),
            Receipt("broken-branch", enabled: true, dependencies: ["missing"]),
            Receipt("second-root", enabled: true, dependencies: ["candidate"]),
            Receipt("candidate", enabled: false, dependencies: ["broken-branch"])
        };
        if (reverse)
        {
            Array.Reverse(installed);
        }

        var plan = ModDependencyRepairPlanner.CreatePlan(
            installed, [], "1.5.78.11833", "modding-api-77");

        Assert.Equal(ModDependencyRepairAction.Unresolved,
            plan.Items.Single(item => item.ModId == "missing").Action);
        Assert.Equal(ModDependencyRepairAction.Unresolved,
            plan.Items.Single(item => item.ModId == "candidate").Action);
    }

    private static InstalledModReceipt Receipt(
        string id,
        bool enabled,
        IReadOnlyList<string>? dependencies = null) => new()
        {
            Id = id,
            Name = id,
            Version = "1.0.0",
            LoaderId = "modding-api-77",
            InstallRoot = $"Mods/{id}",
            Enabled = enabled,
            Dependencies = dependencies ?? []
        };

    private static ModManifest Manifest(
        string id,
        IReadOnlyList<string>? dependencies = null) => new()
        {
            Id = id,
            Name = id,
            Version = "1.0.0",
            DownloadUrl = "https://example.invalid/mod.zip",
            Sha256 = new string('A', 64),
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"],
            Dependencies = dependencies ?? []
        };
}
