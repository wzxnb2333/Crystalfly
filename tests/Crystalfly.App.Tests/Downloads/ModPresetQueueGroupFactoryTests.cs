using Crystalfly.App.Downloads;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Tests.Downloads;

public sealed class ModPresetQueueGroupFactoryTests
{
    [Fact]
    public void Create_projects_install_enable_disable_steps_and_skips_unresolved_entries()
    {
        var preset = new ModPreset
        {
            Id = "preset",
            Name = "Practice",
            GameBuildId = "build",
            LoaderId = "modding-api-77",
            ApplyMode = ModPresetApplyMode.Exact
        };
        var plan = new PresetApplyPlan
        {
            Preset = preset,
            PreApplyStates = [],
            Steps =
            [
                Step(PresetApplyStepKind.Install, "dependency", "1.0"),
                Step(PresetApplyStepKind.Enable, "existing", "2.0"),
                Step(PresetApplyStepKind.Unresolved, "Local helper", null),
                Step(PresetApplyStepKind.Disable, "extra", "3.0")
            ]
        };
        var catalog = new GameCatalog
        {
            Mods =
            [
                new ModManifest
                {
                    Id = "dependency",
                    Name = "Dependency",
                    Version = "1.0",
                    LoaderId = "modding-api-77",
                    DownloadUrl = "https://example.test/dependency.zip",
                    Sha256 = new string('A', 64),
                    SupportedBuildIds = ["build"]
                }
            ]
        };
        var instance = new InstanceRecord
        {
            Id = "instance",
            Name = "Instance",
            RootPath = "C:\\Games\\Instance",
            BuildId = "build",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var group = ModPresetQueueGroupFactory.Create(plan, catalog, instance);

        Assert.Equal(DownloadQueueGroupKind.ModPresetApply, group.Kind);
        Assert.Equal("build", group.ExpectedBuildId);
        Assert.Equal("modding-api-77", group.ExpectedLoaderId);
        Assert.Equal(
            [
                DownloadQueueItemKind.PresetPrepare,
                DownloadQueueItemKind.PresetInstall,
                DownloadQueueItemKind.PresetEnable,
                DownloadQueueItemKind.PresetDisable
            ],
            group.Items.Select(item => item.Kind));
        Assert.Equal("https://example.test/dependency.zip", group.Items[1].DownloadUrl);
    }

    private static PresetApplyStep Step(PresetApplyStepKind kind, string id, string? version) => new()
    {
        Kind = kind,
        State = kind == PresetApplyStepKind.Unresolved
            ? PresetApplyStepState.Unresolved
            : PresetApplyStepState.Pending,
        ModId = id,
        Version = version,
        LoaderId = kind == PresetApplyStepKind.Unresolved ? null : "modding-api-77",
        Reason = kind.ToString()
    };
}
