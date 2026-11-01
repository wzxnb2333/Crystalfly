using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;
using System.Text.RegularExpressions;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class InstanceHierarchyTests
{
    [Fact]
    public void Search_keeps_ancestors_and_favorites_do_not_separate_children()
    {
        var parent = Item("parent");
        var child = Item("child", "parent") with { IsFavorite = true };
        var grandchild = Item("grandchild", "child");
        var rows = InstancesViewModel.BuildHierarchy([grandchild, child, parent, Item("other")], "grandchild", new());
        Assert.Equal(new[] { "parent", "child", "grandchild" }, rows.Select(item => item.Id));
        Assert.Equal(new[] { 0d, 0d, 0d }, rows.Select(item => item.HierarchyMargin.Left));
        Assert.Single(rows, item => item.HasGroupHeading);
        Assert.False(parent.IsChildInstance);
        Assert.True(child.IsChildInstance);
    }

    [Fact]
    public void Missing_parents_cycles_and_old_shared_instances_remain_visible_without_invented_origins()
    {
        var old = Item("old") with { Record = Item("old").Record with { UsesSharedContent = true } };
        var orphan = Item("orphan", "missing");
        var rows = InstancesViewModel.BuildHierarchy([old, orphan, Item("a", "b"), Item("b", "a")], "", new());
        Assert.Equal(4, rows.Select(item => item.Id).Distinct().Count());
        Assert.False(old.IsChildInstance);
        Assert.False(orphan.IsChildInstance);
        Assert.Null(old.Record.ParentInstanceId);
        Assert.Contains("missing", orphan.StorageRelationship);
    }

    [Fact]
    public void Build_groups_do_not_mix_games_or_assign_legacy_clones_a_parent()
    {
        var hk = Item("hk");
        var ss = Item("ss") with { Record = Item("ss").Record with { GameId = GameId.Silksong } };
        var rows = InstancesViewModel.BuildHierarchy([ss, hk], "", new());
        Assert.All(rows, item => Assert.True(item.HasGroupHeading));
        Assert.All(rows, item => Assert.False(item.IsChildInstance));
    }

    [Fact]
    public void All_descendants_remain_visible_and_aligned_while_search_keeps_their_ancestors()
    {
        var items = new[] { Item("parent"), Item("child-a", "parent"), Item("grandchild", "child-a"), Item("child-b", "parent") };
        var rows = InstancesViewModel.BuildHierarchy(items, "", new());
        Assert.Equal(new[] { "parent", "child-a", "grandchild", "child-b" }, rows.Select(row => row.Id));
        Assert.All(rows, row => Assert.Equal(0, row.HierarchyMargin.Left));
        Assert.All(rows, row => Assert.Equal(40, row.HierarchyContentMargin.Left));
        Assert.All(rows, row => Assert.NotNull(row.TreeGeometry));
        Assert.Equal(new[] { 0, 1, 2, 1 }, rows.Select(row => row.HierarchyDepth));
        rows = InstancesViewModel.BuildHierarchy(items, "grandchild", new());
        Assert.Equal(new[] { "parent", "child-a", "grandchild" }, rows.Select(row => row.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deep_shared_chains_and_sibling_returns_have_continuous_bounded_lanes(bool branches)
    {
        var items = Enumerable.Range(0, 32).Select(depth => Item($"node-{depth:D2}",
            depth == 0 ? null : $"node-{depth - 1:D2}")).ToList();
        if (branches)
            items.AddRange(new[] { Item("sibling-deep", "node-08"), Item("sibling-middle", "node-03"), Item("sibling-root", "node-00") });
        var rows = InstancesViewModel.BuildHierarchy(items, "", new());
        Assert.Equal(items.Count, rows.Count);
        Assert.Equal(31, rows.Max(row => row.HierarchyDepth));
        for (var index = 0; index < rows.Count - 1; index++)
        {
            var exits = Regex.Matches(rows[index].TreeGeometry ?? "", @"[MQ](\d+),(?:0 V64|24 V64|32 \d+,40 V64)")
                .Select(match => int.Parse(match.Groups[1].Value)).Distinct().Order().ToArray();
            var entries = Regex.Matches(rows[index + 1].TreeGeometry ?? "", @"M(\d+),0 ")
                .Select(match => int.Parse(match.Groups[1].Value)).Distinct().Order().ToArray();
            Assert.Equal(entries, exits);
            Assert.All(exits, x => Assert.InRange(x, 12, 30));
        }
        Assert.All(rows, row => Assert.Equal(40, row.HierarchyContentMargin.Left));
    }

    private static InstanceItemViewModel Item(string id, string? parent = null) => new(new InstanceRecord
    {
        Id = id, Name = id, RootPath = Path.Combine(Path.GetTempPath(), id), BuildId = "1.0",
        ParentInstanceId = parent, ParentInstanceName = parent
    }, "1.0", "Vanilla", 0);
}
