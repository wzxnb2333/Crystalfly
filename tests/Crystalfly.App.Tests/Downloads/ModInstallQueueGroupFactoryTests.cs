using Crystalfly.App.Downloads;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Tests.Downloads;

public sealed class ModInstallQueueGroupFactoryTests
{
    [Fact]
    public void Creates_loader_dependency_and_mod_items_in_plan_order()
    {
        var instance = Instance();
        var catalog = Catalog();
        var plan = Plan(
            Item(ModInstallPlanItemKind.Loader, ModInstallPlanItemState.NeedsInstall, "loader"),
            Item(ModInstallPlanItemKind.Dependency, ModInstallPlanItemState.Satisfied, "library"),
            Item(ModInstallPlanItemKind.Mod, ModInstallPlanItemState.NeedsInstall, "feature"));

        var group = ModInstallQueueGroupFactory.Create(plan, catalog, instance);

        Assert.Equal("practice:mod:feature", group.DeduplicationKey);
        Assert.Equal(instance.RootPath, group.TargetInstanceRoot);
        Assert.Equal(
            [DownloadQueueItemKind.Loader, DownloadQueueItemKind.Dependency, DownloadQueueItemKind.Mod],
            group.Items.Select(item => item.Kind));
        Assert.Equal(DownloadQueueItemState.Pending, group.Items[1].State);
        Assert.True(group.Items[1].IsSatisfied);
        Assert.Equal(group.Items[1].TotalBytes, group.Items[1].CompletedBytes);
        Assert.Equal("https://packages.test/feature.zip", group.Items[2].DownloadUrl);
    }

    [Fact]
    public void Rejects_blocked_or_mismatched_plan()
    {
        var instance = Instance();
        var catalog = Catalog();
        var blocked = Plan(
            Item(ModInstallPlanItemKind.Loader, ModInstallPlanItemState.Satisfied, "loader"),
            Item(ModInstallPlanItemKind.Mod, ModInstallPlanItemState.Blocked, "feature"));

        Assert.Throws<InvalidOperationException>(() =>
            ModInstallQueueGroupFactory.Create(blocked, catalog, instance));
        Assert.Throws<InvalidDataException>(() =>
            ModInstallQueueGroupFactory.Create(
                blocked with { InstanceId = "other", Items = blocked.Items.Take(1).ToArray() },
                catalog,
                instance));
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
        Loaders =
        [
            new()
            {
                Id = "loader",
                Name = "Loader",
                Version = "77",
                DownloadUrl = "https://packages.test/loader.zip",
                Sha256 = new string('A', 64),
                SizeBytes = 10,
                SupportedBuildIds = ["1.5.78.11833"]
            }
        ],
        Mods =
        [
            Mod("library", 20),
            Mod("feature", 30)
        ]
    };

    private static ModManifest Mod(string id, long size) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0",
        LoaderId = "loader",
        DownloadUrl = $"https://packages.test/{id}.zip",
        Sha256 = new string(id == "library" ? 'B' : 'C', 64),
        SizeBytes = size,
        SupportedBuildIds = ["1.5.78.11833"]
    };

    private static ModInstallPlan Plan(params ModInstallPlanItem[] items) => new()
    {
        ModId = "feature",
        InstanceId = "practice",
        InstanceName = "Practice",
        Items = items
    };

    private static ModInstallPlanItem Item(
        ModInstallPlanItemKind kind,
        ModInstallPlanItemState state,
        string id) => new()
        {
            Kind = kind,
            State = state,
            Id = id,
            Name = id,
            Version = kind == ModInstallPlanItemKind.Loader ? "77" : "1.0",
            LoaderId = "loader",
            Reason = state.ToString()
        };
}
