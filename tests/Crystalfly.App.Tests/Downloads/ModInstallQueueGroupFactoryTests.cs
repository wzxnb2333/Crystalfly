using Crystalfly.App.Downloads;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Tests.Downloads;

public sealed class ModInstallQueueGroupFactoryTests
{
    [Fact]
    public void Batch_queue_preserves_requested_mods_and_shared_dependency_once()
    {
        var catalog = Catalog() with { Mods = [Mod("library", 20), Mod("feature", 30), Mod("second", 40)] };
        var plan = Plan(
            Item(ModInstallPlanItemKind.Loader, ModInstallPlanItemState.Satisfied, "loader"),
            Item(ModInstallPlanItemKind.Dependency, ModInstallPlanItemState.NeedsInstall, "library"),
            Item(ModInstallPlanItemKind.Mod, ModInstallPlanItemState.NeedsInstall, "second"),
            Item(ModInstallPlanItemKind.Mod, ModInstallPlanItemState.NeedsInstall, "feature"))
            with { RequestedModIds = ["feature", "second"] };

        var group = ModInstallQueueGroupFactory.Create(plan, catalog, Instance());

        Assert.Equal("practice:mod:feature;second", group.DeduplicationKey);
        Assert.Equal("second + feature", group.Name);
        Assert.Equal(4, group.Items.Count);
        Assert.Single(group.Items, item => item.PackageId == "library");
        Assert.Equal(2, group.Items.Count(item => item.Kind == DownloadQueueItemKind.Mod));
        Assert.Equal(group.DeduplicationKey, ModInstallQueueGroupFactory.Create(
            plan with { RequestedModIds = ["second", "feature"] }, catalog, Instance()).DeduplicationKey);
        Assert.Throws<InvalidDataException>(() => ModInstallQueueGroupFactory.Create(
            plan with { Items = [.. plan.Items.Take(2), plan.Items[1], .. plan.Items.Skip(2)] }, catalog, Instance()));
        Assert.Throws<InvalidDataException>(() => ModInstallQueueGroupFactory.Create(
            plan with { Items = plan.Items.Select(item => item.Id == "second" ? item with { LoaderId = "other" } : item).ToArray() }, catalog, Instance()));
    }

    [Fact]
    public void Resolves_legacy_official_package_for_latest_api78_plan()
    {
        var instance = Instance() with { BuildId = "1.5.12620.0" };
        var catalog = new GameCatalog
        {
            Loaders =
            [
                new LoaderManifest
                {
                    Id = "modding-api-78",
                    Name = "Modding API",
                    Version = "78",
                    DownloadUrl = "https://packages.test/api78.zip",
                    Sha256 = new string('D', 64),
                    SupportedBuildIds = ["1.5.12620.0"]
                }
            ],
            Mods =
            [
                Mod("hkmod:feature", 30) with
                {
                    SourceName = "HK ModLinks",
                    LoaderId = "modding-api-77"
                }
            ]
        };
        var plan = new ModInstallPlan
        {
            ModId = "hkmod:feature",
            InstanceId = instance.Id,
            InstanceName = instance.Name,
            Items =
            [
                new ModInstallPlanItem
                {
                    Kind = ModInstallPlanItemKind.Loader,
                    State = ModInstallPlanItemState.NeedsInstall,
                    Id = "modding-api-78",
                    Name = "Modding API",
                    Version = "78",
                    LoaderId = "modding-api-78",
                    Reason = "install"
                },
                new ModInstallPlanItem
                {
                    Kind = ModInstallPlanItemKind.Mod,
                    State = ModInstallPlanItemState.NeedsInstall,
                    Id = "hkmod:feature",
                    Name = "feature",
                    Version = "1.0",
                    LoaderId = "modding-api-78",
                    Reason = "install"
                }
            ]
        };

        var group = ModInstallQueueGroupFactory.Create(plan, catalog, instance);

        Assert.Equal("modding-api-78", group.ExpectedLoaderId);
        Assert.Equal("https://packages.test/hkmod:feature.zip", group.Items[^1].DownloadUrl);
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
