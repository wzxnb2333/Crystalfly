using System.Diagnostics;
using Crystalfly.App.Downloads;
using Crystalfly.App.Services;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    internal ProtocolCommand PrepareProtocolCommand(ProtocolCommand command) =>
        protocolService.Prepare(command);

    internal bool CanExecuteProtocolCommand(ProtocolCommand command, out string reason) =>
        protocolService.CanExecute(command, out reason);

    internal string DescribeProtocolCommand(ProtocolCommand command) =>
        protocolService.Describe(command);

    internal Task ExecuteProtocolCommandAsync(ProtocolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return protocolService.ExecuteAsync(command, (command, cancellationToken) =>
            ExecuteProtocolCommandDispatchAsync(command, cancellationToken));
    }

    private async Task ExecuteProtocolCommandDispatchAsync(
        ProtocolCommand command,
        CancellationToken cancellationToken)
    {
        IsExternalCommandRunning = true;
        ErrorMessage = null;
        try
        {
            await ProtocolService.DispatchAsync(
                command,
                CreateProtocolCommandExecutor(),
                cancellationToken);
        }
        finally
        {
            IsExternalCommandRunning = false;
        }
    }

    private ProtocolCommandExecutor CreateProtocolCommandExecutor() => new(
        DownloadMod: (command, _) => DownloadProtocolModAsync(command),
        ReinstallAllMods: (command, _) => ReinstallAllProtocolModsAsync(command),
        ResetApplicationSettings: (_, _) => ResetApplicationSettingsAsync(),
        ApplyModLinks: (command, _) => command.Kind == ProtocolCommandKind.UseOfficialModLinks
            ? ApplyProtocolModLinksAsync(null)
            : ApplyProtocolModLinksAsync(new CustomModLinksDefinition
            {
                Url = command.SourceUrl!,
                BuildId = command.BuildId!,
                LoaderId = command.LoaderId!
            }),
        DeleteModSettings: (command, _) =>
            DeleteProtocolModSettingsAsync(command, deleteAll: command.Kind == ProtocolCommandKind.DeleteAllModSettings),
        LaunchInstance: async (command, _) =>
        {
            SelectProtocolInstance(command.InstanceId!);
            await LaunchGameCoreAsync(force: false);
        },
        OpenModLocation: (command, _) => OpenProtocolModLocationAsync(command),
        ImportPresetShare: async (command, _) =>
        {
            SelectProtocolInstance(command.InstanceId!);
            PresetShareCode = command.ShareCode!;
            await ImportSharedPresetAsync();
        });

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
        NotifyToast(result.Added
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
            var compatibleMods = ModCatalogCompatibility.ProjectForBuild(catalog.Mods, record.BuildId);
            foreach (var receipt in installed.Where(receipt =>
                         receipt.Ownership == ModOwnership.Managed
                         && !receipt.IsLocal
                         && !receipt.Pinned))
            {
                var manifest = compatibleMods.SingleOrDefault(candidate =>
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
        Instances.Instances.Clear();
        Instances.VisibleInstances.Clear();
        VersionRoot = string.Empty;
        Settings.CustomSourcesText = string.Empty;
        Settings.CustomModLinksUrl = string.Empty;
        SelectedMarketMod = null;
        CurrentPage = "Launch";
        CurrentManageTab = "Overview";
        catalog = EmbeddedCatalog.Load();
        ApplyLanguage(settings.Language);
        ApplyTheme(settings.Theme, settings.AccentColor);
        InitializeApplicationUpdateSettings();
        await QueueSettingsSave();
        RebuildSettingOptions();
        RebuildCustomModLinksOptions();
        RebuildMarketCatalog();
        StatusMessage = Loc["ChooseRoot"];
        NotifyToast(Loc["ProtocolSettingsReset"]);
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
        Settings.CustomModLinksUrl = definition?.Url ?? string.Empty;
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
        var instance = Instances.Instances.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Instance '{id}' was not found.");
        SelectedInstance = instance;
        return instance;
    }

    private static bool IsOfficialMod(ModManifest manifest) =>
        manifest.Id.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
        && string.Equals(manifest.SourceName, "HK ModLinks", StringComparison.Ordinal);
}
