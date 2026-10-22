using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class InstalledModDependencyGraphTests
{

    [Fact]
    public void Removal_plan_marks_only_selected_targets_for_deletion_and_enabled_dependents_as_broken()
    {
        var mods = new[]
        {
            Receipt("library"),
            Receipt("feature", ["library"]),
            Receipt("addon", ["feature"]),
            Receipt("disabled", ["library"]) with { Enabled = false },
            Receipt("other", ["library"])
        };

        var plan = InstalledModDependencyGraph.CreateRemovalPlan(mods, ["library"]);

        Assert.Equal(["library"], plan.TargetModIds);
        Assert.Equal(ModRemovalImpactKind.WillRemove, plan.Nodes.Single(node => node.ModId == "library").Kind);
        Assert.Equal(
            ["addon", "feature", "other"],
            plan.Nodes.Where(node => node.Kind == ModRemovalImpactKind.DependencyWillBeMissing)
                .Select(node => node.ModId)
                .Order());
        Assert.DoesNotContain(plan.Nodes, node => node.ModId == "disabled");
        Assert.All(plan.Nodes, node =>
        {
            Assert.False(string.IsNullOrWhiteSpace(node.ReceiptName));
            Assert.False(string.IsNullOrWhiteSpace(node.InstallRoot));
        });
    }

    [Fact]
    public void Removal_plan_keeps_shared_dependencies_and_handles_cycles_once()
    {
        var mods = new[]
        {
            Receipt("shared"),
            Receipt("first", ["shared", "second"]),
            Receipt("second", ["shared", "first"])
        };

        var plan = InstalledModDependencyGraph.CreateRemovalPlan(mods, ["first"]);

        Assert.Equal(["first"], plan.TargetModIds);
        Assert.DoesNotContain(plan.Nodes, node => node.ModId == "shared");
        Assert.Equal(2, plan.Nodes.Count);
        Assert.Equal(ModRemovalImpactKind.DependencyWillBeMissing, plan.Nodes.Single(node => node.ModId == "second").Kind);
    }

    private static InstalledModReceipt Receipt(string id, IReadOnlyList<string>? dependencies = null) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        LoaderId = "modding-api",
        InstallRoot = $"Mods/{id}",
        Dependencies = dependencies ?? []
    };
}
