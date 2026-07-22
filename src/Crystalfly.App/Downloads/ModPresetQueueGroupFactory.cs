using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Downloads;

public static class ModPresetQueueGroupFactory
{
    public static DownloadQueueGroup Create(
        PresetApplyPlan plan,
        GameCatalog catalog,
        InstanceRecord instance)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(instance);
        if (plan.IsBlocked)
        {
            throw new InvalidOperationException(
                plan.Steps.First(step => step.State == PresetApplyStepState.Blocked).Reason);
        }
        if (!string.Equals(plan.Preset.GameBuildId, instance.BuildId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The preset apply plan does not match the target instance build.");
        }

        var changes = plan.Steps
            .Where(step => step.State != PresetApplyStepState.Satisfied
                && step.Kind != PresetApplyStepKind.Unresolved)
            .Select(step => CreateItem(step, plan.Preset, catalog))
            .ToArray();
        if (changes.Length == 0)
        {
            throw new InvalidOperationException("The preset contains no automatic changes.");
        }
        DownloadQueueItem[] items =
        [
            new DownloadQueueItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = DownloadQueueItemKind.PresetPrepare,
                PackageId = plan.Preset.Id,
                Name = plan.Preset.Name,
                Version = plan.Preset.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                LoaderId = plan.Preset.LoaderId,
                Stage = "Pending"
            },
            .. changes
        ];
        return new DownloadQueueGroup
        {
            Id = Guid.NewGuid().ToString("N"),
            DeduplicationKey = $"{instance.Id}:preset:{plan.Preset.Id}",
            Kind = DownloadQueueGroupKind.ModPresetApply,
            Name = plan.Preset.Name,
            TargetInstanceId = instance.Id,
            TargetInstanceName = instance.Name,
            TargetInstanceRoot = instance.RootPath,
            ExpectedBuildId = plan.Preset.GameBuildId,
            ExpectedLoaderId = plan.Preset.LoaderId,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = items
        };
    }

    private static DownloadQueueItem CreateItem(
        PresetApplyStep step,
        ModPreset preset,
        GameCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(step.Version)
            || !string.Equals(step.LoaderId, preset.LoaderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Preset step '{step.ModId}' has incomplete package identity.");
        }
        var version = step.Version;
        var loaderId = step.LoaderId!;
        ModManifest? manifest = null;
        if (step.Kind == PresetApplyStepKind.Install)
        {
            manifest = catalog.Mods.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, step.ModId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Version, version, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.LoaderId, loaderId, StringComparison.OrdinalIgnoreCase)
                && candidate.SupportedBuildIds.Contains(preset.GameBuildId, StringComparer.OrdinalIgnoreCase))
                ?? throw new InvalidDataException(
                    $"Preset package '{step.ModId}' version '{step.Version}' is missing from the catalog.");
        }
        return new DownloadQueueItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = step.Kind switch
            {
                PresetApplyStepKind.Install => DownloadQueueItemKind.PresetInstall,
                PresetApplyStepKind.Enable => DownloadQueueItemKind.PresetEnable,
                PresetApplyStepKind.Disable => DownloadQueueItemKind.PresetDisable,
                _ => throw new InvalidDataException($"Unsupported preset step: {step.Kind}.")
            },
            PackageId = step.ModId,
            Name = manifest?.Name ?? step.ModId,
            Version = version,
            LoaderId = loaderId,
            DownloadUrl = manifest?.DownloadUrl,
            SizeBytes = manifest?.SizeBytes,
            Sha256 = manifest?.Sha256,
            TotalBytes = manifest?.SizeBytes ?? 0,
            Stage = "Pending"
        };
    }
}
