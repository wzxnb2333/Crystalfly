using Crystalfly.App.Downloads;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Tests.Downloads;

public sealed class ModDependencyRepairQueueGroupFactoryTests
{
    [Fact]
    public void Appends_repair_enum_values_without_reinterpreting_persisted_queue_entries()
    {
        Assert.Equal(1, (int)DownloadQueueGroupKind.LoaderInstall);
        Assert.Equal(2, (int)DownloadQueueGroupKind.AssetInstall);
        Assert.Equal(3, (int)DownloadQueueGroupKind.ModDependencyRepair);
        Assert.Equal(2, (int)DownloadQueueItemKind.Mod);
        Assert.Equal(3, (int)DownloadQueueItemKind.Asset);
        Assert.Equal(4, (int)DownloadQueueItemKind.DependencyReEnable);
    }

    [Fact]
    public void Creates_only_repairable_items_in_dependency_order()
    {
        var instance = Instance();
        var plan = new ModDependencyRepairPlan
        {
            BuildId = instance.BuildId,
            LoaderId = "modding-api-77",
            Items =
            [
                Item("library", ModDependencyRepairAction.ReEnable),
                Item("bridge", ModDependencyRepairAction.DownloadAndInstall),
                Item("unknown", ModDependencyRepairAction.Unresolved)
            ]
        };

        var group = ModDependencyRepairQueueGroupFactory.Create(plan, Catalog(), instance);

        Assert.Equal(DownloadQueueGroupKind.ModDependencyRepair, group.Kind);
        Assert.StartsWith("practice:repair:modding-api-77:", group.DeduplicationKey);
        Assert.Equal(instance.BuildId, group.ExpectedBuildId);
        Assert.Equal("modding-api-77", group.ExpectedLoaderId);
        Assert.Equal(["library", "bridge"], group.Items.Select(item => item.PackageId));
        Assert.Equal(
            [DownloadQueueItemKind.DependencyReEnable, DownloadQueueItemKind.Dependency],
            group.Items.Select(item => item.Kind));
        Assert.Null(group.Items[0].DownloadUrl);
        Assert.Equal("https://packages.test/bridge.zip", group.Items[1].DownloadUrl);
    }

    [Fact]
    public void Deduplication_key_is_order_independent_and_distinguishes_repair_steps()
    {
        var instance = Instance();
        var library = Item("library", ModDependencyRepairAction.ReEnable);
        var bridge = Item("bridge", ModDependencyRepairAction.DownloadAndInstall);
        var plan = new ModDependencyRepairPlan
        {
            BuildId = instance.BuildId,
            LoaderId = "modding-api-77",
            Items = [library, bridge]
        };

        var key = ModDependencyRepairQueueGroupFactory.Create(plan, Catalog(), instance)
            .DeduplicationKey;
        var reorderedKey = ModDependencyRepairQueueGroupFactory.Create(
            plan with { Items = [bridge, library] }, Catalog(), instance)
            .DeduplicationKey;
        var changedVersionKey = ModDependencyRepairQueueGroupFactory.Create(
            plan with { Items = [library with { Version = "2.0" }, bridge] }, Catalog(), instance)
            .DeduplicationKey;
        var changedActionKey = ModDependencyRepairQueueGroupFactory.Create(
            plan with
            {
                Items = [library, bridge with { Action = ModDependencyRepairAction.ReEnable }]
            },
            Catalog(),
            instance)
            .DeduplicationKey;

        Assert.Equal(key, reorderedKey);
        Assert.NotEqual(key, changedVersionKey);
        Assert.NotEqual(key, changedActionKey);
        Assert.Contains("bridge", key, StringComparison.Ordinal);
        Assert.Contains(nameof(ModDependencyRepairAction.DownloadAndInstall), key, StringComparison.Ordinal);
        Assert.Contains("1.0", key, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_mismatched_or_empty_repair_plan()
    {
        var instance = Instance();
        var plan = new ModDependencyRepairPlan
        {
            BuildId = "other",
            LoaderId = "modding-api-77",
            Items = [Item("unknown", ModDependencyRepairAction.Unresolved)]
        };

        Assert.Throws<InvalidDataException>(() =>
            ModDependencyRepairQueueGroupFactory.Create(plan, Catalog(), instance));
        Assert.Throws<InvalidOperationException>(() =>
            ModDependencyRepairQueueGroupFactory.Create(
                plan with { BuildId = instance.BuildId }, Catalog(), instance));
    }

    private static InstanceRecord Instance() => new()
    {
        Id = "practice",
        Name = "Practice",
        RootPath = "C:\\versions\\practice",
        BuildId = "1.5.78.11833",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static GameCatalog Catalog() => new()
    {
        Mods =
        [
            new()
            {
                Id = "bridge",
                Name = "Bridge",
                Version = "1.0",
                LoaderId = "modding-api-77",
                DownloadUrl = "https://packages.test/bridge.zip",
                Sha256 = new string('A', 64),
                SizeBytes = 42,
                SupportedBuildIds = ["1.5.78.11833"]
            }
        ]
    };

    private static ModDependencyRepairPlanItem Item(
        string id,
        ModDependencyRepairAction action) => new()
        {
            ModId = id,
            PackageId = id,
            Name = id,
            Version = "1.0",
            LoaderId = "modding-api-77",
            Action = action,
            RequiredByModIds = ["feature"],
            Reason = action.ToString()
        };
}
