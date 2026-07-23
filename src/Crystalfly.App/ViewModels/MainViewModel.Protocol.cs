using System.Diagnostics;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    internal ProtocolCommand PrepareProtocolCommand(ProtocolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Kind != ProtocolCommandKind.ImportPresetShare || command.InstanceId is not null)
        {
            return command;
        }

        return command with
        {
            InstanceId = SelectedInstance?.Id ?? throw new InvalidOperationException(Loc["NoInstance"])
        };
    }

    internal bool CanExecuteProtocolCommand(ProtocolCommand command, out string reason)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsBusy)
        {
            reason = Loc["ExternalCommandBusy"];
            return false;
        }
        if (IsGameRunning || new SystemHollowKnightProcessProbe().IsRunning())
        {
            reason = Loc["ExternalCommandGameRunning"];
            return false;
        }
        if (HasUnfinishedDownloads
            && command.Kind is ProtocolCommandKind.ResetApplicationSettings
                or ProtocolCommandKind.UseOfficialModLinks
                or ProtocolCommandKind.UseCustomModLinks)
        {
            reason = Loc["ExternalCommandDownloadsActive"];
            return false;
        }

        reason = string.Empty;
        return true;
    }

    internal string DescribeProtocolCommand(ProtocolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var instance = command.InstanceId is null
            ? null
            : Instances.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, command.InstanceId, StringComparison.Ordinal));
        var mod = command.ModId is null
            ? null
            : catalog.Mods.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, command.ModId, StringComparison.OrdinalIgnoreCase));
        var action = Loc[command.Kind switch
        {
            ProtocolCommandKind.DownloadMod => "ProtocolDownloadMod",
            ProtocolCommandKind.ReinstallAllMods => "ProtocolReinstallAllMods",
            ProtocolCommandKind.ResetApplicationSettings => "ProtocolResetApplicationSettings",
            ProtocolCommandKind.UseOfficialModLinks => "ProtocolUseOfficialModLinks",
            ProtocolCommandKind.UseCustomModLinks => "ProtocolUseCustomModLinks",
            ProtocolCommandKind.DeleteModSettings => "ProtocolDeleteModSettings",
            ProtocolCommandKind.DeleteAllModSettings => "ProtocolDeleteAllModSettings",
            ProtocolCommandKind.LaunchInstance => "ProtocolLaunchInstance",
            ProtocolCommandKind.OpenModLocation => "ProtocolOpenModLocation",
            ProtocolCommandKind.ImportPresetShare => "ProtocolImportPresetShare",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        }];
        var details = new List<string> { action };
        if (command.InstanceId is not null)
        {
            details.Add($"{Loc["QueueTarget"]}: {instance?.Name ?? command.InstanceId}");
        }
        if (command.ModId is not null)
        {
            details.Add($"Mod: {mod?.DisplayName ?? mod?.Name ?? command.ModId}");
        }
        if (command.SourceUrl is not null)
        {
            details.Add(command.SourceUrl);
            details.Add($"{command.BuildId} · {command.LoaderId}");
        }
        if (command.ShareCode is not null)
        {
            details.Add($"{Loc["ShareCode"]}: {command.ShareCode}");
        }
        return string.Join(Environment.NewLine, details);
    }

    internal Task ExecuteProtocolCommandAsync(ProtocolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (externalProtocolCommandSync)
        {
            if (!externalProtocolCommandTask.IsCompleted)
            {
                throw new InvalidOperationException(Loc["ExternalCommandBusy"]);
            }

            externalProtocolCommandTask = ExecuteProtocolCommandCoreAsync(command);
            return externalProtocolCommandTask;
        }
    }

    private async Task ExecuteProtocolCommandCoreAsync(ProtocolCommand command)
    {
        command = PrepareProtocolCommand(command);
        if (lifetimeCancellation.IsCancellationRequested)
        {
            throw new InvalidOperationException(Loc["ExternalCommandClosing"]);
        }
        if (!CanExecuteProtocolCommand(command, out string reason))
        {
            throw new InvalidOperationException(reason);
        }

        IsExternalCommandRunning = true;
        ErrorMessage = null;
        try
        {
            switch (command.Kind)
            {
                case ProtocolCommandKind.DownloadMod:
                    await DownloadProtocolModAsync(command);
                    break;
                case ProtocolCommandKind.ReinstallAllMods:
                    await ReinstallAllProtocolModsAsync(command);
                    break;
                case ProtocolCommandKind.ResetApplicationSettings:
                    await ResetApplicationSettingsAsync();
                    break;
                case ProtocolCommandKind.UseOfficialModLinks:
                    await ApplyProtocolModLinksAsync(null);
                    break;
                case ProtocolCommandKind.UseCustomModLinks:
                    await ApplyProtocolModLinksAsync(new CustomModLinksDefinition
                    {
                        Url = command.SourceUrl!,
                        BuildId = command.BuildId!,
                        LoaderId = command.LoaderId!
                    });
                    break;
                case ProtocolCommandKind.DeleteModSettings:
                    await DeleteProtocolModSettingsAsync(command, deleteAll: false);
                    break;
                case ProtocolCommandKind.DeleteAllModSettings:
                    await DeleteProtocolModSettingsAsync(command, deleteAll: true);
                    break;
                case ProtocolCommandKind.LaunchInstance:
                    SelectProtocolInstance(command.InstanceId!);
                    await LaunchGameCoreAsync(force: false);
                    break;
                case ProtocolCommandKind.OpenModLocation:
                    await OpenProtocolModLocationAsync(command);
                    break;
                case ProtocolCommandKind.ImportPresetShare:
                    SelectProtocolInstance(command.InstanceId!);
                    PresetShareCode = command.ShareCode!;
                    await ImportSharedPresetAsync();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command));
            }
        }
        finally
        {
            IsExternalCommandRunning = false;
        }
    }

    private async Task DownloadProtocolModAsync(ProtocolCommand command)
    {
        var instance = SelectProtocolInstance(command.InstanceId!);
        if (!catalog.Mods.Any(mod => string.Equals(mod.Id, command.ModId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new KeyNotFoundException($"Mod '{command.ModId}' was not found in the active catalog.");
        }
        await downloadQueue.InitializeAsync(lifetimeCancellation.Token);
        var plan = await CreateModInstallService(instance.Record)
            .CreatePlanAsync(command.ModId!, lifetimeCancellation.Token);
        var result = await downloadQueue.EnqueueAsync(
            ModInstallQueueGroupFactory.Create(plan, catalog, instance.Record),
            lifetimeCancellation.Token);
        ToastRequested?.Invoke(result.Added
            ? Loc["AddedToDownloadQueue"]
            : Loc["QueueTaskAlreadyExists"]);
    }

    private async Task ReinstallAllProtocolModsAsync(ProtocolCommand command)
    {
        SelectProtocolInstance(command.InstanceId!);
        var repaired = 0;
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            var installed = await manager.GetInstalledAsync(lifetimeCancellation.Token);
            foreach (var receipt in installed.Where(receipt =>
                         receipt.Ownership == ModOwnership.Managed
                         && !receipt.IsLocal
                         && !receipt.Pinned))
            {
                var manifest = catalog.Mods.SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, receipt.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.Version, receipt.Version, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.LoaderId, receipt.LoaderId, StringComparison.OrdinalIgnoreCase)
                    && candidate.SupportedBuildIds.Contains(record.BuildId, StringComparer.OrdinalIgnoreCase));
                if (manifest is null)
                {
                    continue;
                }
                await manager.RepairFromUriAsync(manifest, lifetimeCancellation.Token);
                repaired++;
            }
        });
        if (repaired == 0 && string.IsNullOrWhiteSpace(ErrorMessage))
        {
            throw new InvalidOperationException(Loc["ProtocolNoModsToReinstall"]);
        }
    }

    private async Task ResetApplicationSettingsAsync()
    {
        settings = new CrystalflySettings();
        networkPolicy.SetOffline(false);
        IsOfflineMode = false;
        SelectedInstance = null;
        Instances.Clear();
        VisibleInstances.Clear();
        VersionRoot = string.Empty;
        CustomSourcesText = string.Empty;
        CustomModLinksUrl = string.Empty;
        SelectedMarketMod = null;
        CurrentPage = "Launch";
        CurrentManageTab = "Overview";
        catalog = EmbeddedCatalog.Load();
        ApplyLanguage(settings.Language);
        ApplyTheme(settings.Theme);
        InitializeApplicationUpdateSettings();
        await QueueSettingsSave();
        RebuildSettingOptions();
        RebuildCustomModLinksOptions();
        RebuildMarketCatalog();
        StatusMessage = Loc["ChooseRoot"];
        ToastRequested?.Invoke(Loc["ProtocolSettingsReset"]);
    }

    private async Task ApplyProtocolModLinksAsync(CustomModLinksDefinition? definition)
    {
        if (definition is not null)
        {
            var embedded = EmbeddedCatalog.Load();
            var loader = embedded.Loaders.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, definition.LoaderId, StringComparison.OrdinalIgnoreCase)
                && candidate.SupportedBuildIds.Contains(definition.BuildId, StringComparer.OrdinalIgnoreCase));
            if (loader is null || embedded.Builds.All(build =>
                    !string.Equals(build.Id, definition.BuildId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(Loc["CustomModLinksInvalid"]);
            }
        }
        settings = settings with { CustomModLinks = definition };
        CustomModLinksUrl = definition?.Url ?? string.Empty;
        await QueueSettingsSave();
        catalog = await LoadCatalogAsync(lifetimeCancellation.Token);
        RebuildCustomModLinksOptions();
        RebuildMarketCatalog();
        if (Directory.Exists(VersionRoot))
        {
            await RefreshAsync();
        }
        NotifyOperationCompleted();
    }

    private async Task DeleteProtocolModSettingsAsync(ProtocolCommand command, bool deleteAll)
    {
        var instance = SelectProtocolInstance(command.InstanceId!);
        var manifests = deleteAll
            ? catalog.Mods.Where(IsOfficialMod).ToArray()
            : catalog.Mods.Where(manifest =>
                IsOfficialMod(manifest)
                && string.Equals(manifest.Id, command.ModId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (manifests.Length == 0)
        {
            throw new KeyNotFoundException($"Mod '{command.ModId}' was not found in HK ModLinks.");
        }
        await instanceOperationCoordinator.RunAsync(instance.Id, async cancellationToken =>
        {
            var deleted = await CreateGlobalModSettingsService().DeleteAsync(
                instance.Id,
                manifests,
                cancellationToken);
            if (deleted == 0)
            {
                throw new FileNotFoundException(Loc["ProtocolNoGlobalSettings"]);
            }
        }, lifetimeCancellation.Token);
        NotifyOperationCompleted();
    }

    private async Task OpenProtocolModLocationAsync(ProtocolCommand command)
    {
        var instance = SelectProtocolInstance(command.InstanceId!);
        await instanceOperationCoordinator.RunAsync(instance.Id, async cancellationToken =>
        {
            var receipt = (await CreateModManager(instance.Record)
                    .GetInstalledAsync(cancellationToken))
                .SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, command.ModId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Mod '{command.ModId}' is not installed.");
            var root = Path.GetFullPath(instance.RootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.IsPathRooted(receipt.InstallRoot))
            {
                throw new InvalidDataException("The installed Mod receipt contains a rooted directory.");
            }
            var relative = receipt.InstallRoot.Replace('\\', '/');
            var target = Path.GetFullPath(Path.Combine(
                root,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(target))
            {
                throw new DirectoryNotFoundException(target);
            }
            for (var current = target; current.Length >= root.Length; current = Path.GetDirectoryName(current)!)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Mod directory traverses a reparse point: '{current}'.");
                }
                if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })
                ?? throw new InvalidOperationException(Loc["ProtocolOpenFolderFailed"]);
        }, lifetimeCancellation.Token);
    }

    private InstanceItemViewModel SelectProtocolInstance(string id)
    {
        var instance = Instances.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Instance '{id}' was not found.");
        SelectedInstance = instance;
        return instance;
    }

    private static bool IsOfficialMod(ModManifest manifest) =>
        manifest.Id.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
        && string.Equals(manifest.SourceName, "HK ModLinks", StringComparison.Ordinal);
}
