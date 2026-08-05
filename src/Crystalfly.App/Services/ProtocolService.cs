using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Services;

public sealed record ProtocolCommandExecutor(
    Func<ProtocolCommand, CancellationToken, Task> DownloadMod,
    Func<ProtocolCommand, CancellationToken, Task> ReinstallAllMods,
    Func<ProtocolCommand, CancellationToken, Task> ResetApplicationSettings,
    Func<ProtocolCommand, CancellationToken, Task> ApplyModLinks,
    Func<ProtocolCommand, CancellationToken, Task> DeleteModSettings,
    Func<ProtocolCommand, CancellationToken, Task> LaunchInstance,
    Func<ProtocolCommand, CancellationToken, Task> OpenModLocation,
    Func<ProtocolCommand, CancellationToken, Task> ImportPresetShare);

public sealed class ProtocolService
{
    private readonly Func<LocalizationViewModel> loc;
    private readonly Func<InstanceItemViewModel?> getSelectedInstance;
    private readonly Func<IReadOnlyList<InstanceItemViewModel>> getInstances;
    private readonly Func<GameCatalog> getCatalog;
    private readonly Func<bool> isBusy;
    private readonly Func<bool> isGameRunning;
    private readonly Func<bool> hasUnfinishedDownloads;
    private readonly CancellationToken lifetimeCancellation;
    private readonly object sync = new();
    private Task commandTask = Task.CompletedTask;

    public ProtocolService(
        Func<LocalizationViewModel> loc,
        Func<InstanceItemViewModel?> getSelectedInstance,
        Func<IReadOnlyList<InstanceItemViewModel>> getInstances,
        Func<GameCatalog> getCatalog,
        Func<bool> isBusy,
        Func<bool> isGameRunning,
        Func<bool> hasUnfinishedDownloads,
        CancellationToken lifetimeCancellation)
    {
        this.loc = loc;
        this.getSelectedInstance = getSelectedInstance;
        this.getInstances = getInstances;
        this.getCatalog = getCatalog;
        this.isBusy = isBusy;
        this.isGameRunning = isGameRunning;
        this.hasUnfinishedDownloads = hasUnfinishedDownloads;
        this.lifetimeCancellation = lifetimeCancellation;
    }

    internal Task PendingExecution
    {
        get
        {
            lock (sync)
            {
                return commandTask;
            }
        }
    }

    public ProtocolCommand Parse(string input) => ProtocolCommandParser.Parse(input);

    public ProtocolCommand Prepare(ProtocolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Kind != ProtocolCommandKind.ImportPresetShare || command.InstanceId is not null)
        {
            return command;
        }

        return command with
        {
            InstanceId = getSelectedInstance()?.Id
                ?? throw new InvalidOperationException(loc()["NoInstance"])
        };
    }

    public bool CanExecute(ProtocolCommand command, out string reason)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (isBusy())
        {
            reason = loc()["ExternalCommandBusy"];
            return false;
        }
        if (isGameRunning() || new SystemHollowKnightProcessProbe().IsRunning())
        {
            reason = loc()["ExternalCommandGameRunning"];
            return false;
        }
        if (hasUnfinishedDownloads()
            && command.Kind is ProtocolCommandKind.ResetApplicationSettings
                or ProtocolCommandKind.UseOfficialModLinks
                or ProtocolCommandKind.UseCustomModLinks)
        {
            reason = loc()["ExternalCommandDownloadsActive"];
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public string Describe(ProtocolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var instance = command.InstanceId is null
            ? null
            : getInstances().FirstOrDefault(candidate =>
                string.Equals(candidate.Id, command.InstanceId, StringComparison.Ordinal));
        var mod = command.ModId is null
            ? null
            : getCatalog().Mods.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, command.ModId, StringComparison.OrdinalIgnoreCase));
        var action = loc()[command.Kind switch
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
            details.Add($"{loc()["QueueTarget"]}: {instance?.Name ?? command.InstanceId}");
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
            details.Add($"{loc()["ShareCode"]}: {command.ShareCode}");
        }
        return string.Join(Environment.NewLine, details);
    }

    public Task ExecuteAsync(ProtocolCommand command, ProtocolCommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (sync)
        {
            if (!commandTask.IsCompleted)
            {
                throw new InvalidOperationException(loc()["ExternalCommandBusy"]);
            }

            commandTask = ExecuteCoreAsync(command, executor);
            return commandTask;
        }
    }

    public Task ExecuteAsync(
        ProtocolCommand command,
        Func<ProtocolCommand, CancellationToken, Task> executeCore)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (sync)
        {
            if (!commandTask.IsCompleted)
            {
                throw new InvalidOperationException(loc()["ExternalCommandBusy"]);
            }

            commandTask = ExecuteCoreAsync(command, executeCore);
            return commandTask;
        }
    }

    private async Task ExecuteCoreAsync(ProtocolCommand command, ProtocolCommandExecutor executor)
    {
        command = Prepare(command);
        if (lifetimeCancellation.IsCancellationRequested)
        {
            throw new InvalidOperationException(loc()["ExternalCommandClosing"]);
        }
        if (!CanExecute(command, out string reason))
        {
            throw new InvalidOperationException(reason);
        }

        await DispatchAsync(command, executor, lifetimeCancellation);
    }

    private async Task ExecuteCoreAsync(
        ProtocolCommand command,
        Func<ProtocolCommand, CancellationToken, Task> executeCore)
    {
        command = Prepare(command);
        if (lifetimeCancellation.IsCancellationRequested)
        {
            throw new InvalidOperationException(loc()["ExternalCommandClosing"]);
        }
        if (!CanExecute(command, out string reason))
        {
            throw new InvalidOperationException(reason);
        }

        await executeCore(command, lifetimeCancellation);
    }

    public static Task DispatchAsync(
        ProtocolCommand command,
        ProtocolCommandExecutor executor,
        CancellationToken cancellationToken)
    {
        return command.Kind switch
        {
            ProtocolCommandKind.DownloadMod => executor.DownloadMod(command, cancellationToken),
            ProtocolCommandKind.ReinstallAllMods => executor.ReinstallAllMods(command, cancellationToken),
            ProtocolCommandKind.ResetApplicationSettings => executor.ResetApplicationSettings(command, cancellationToken),
            ProtocolCommandKind.UseOfficialModLinks or ProtocolCommandKind.UseCustomModLinks =>
                executor.ApplyModLinks(command, cancellationToken),
            ProtocolCommandKind.DeleteModSettings or ProtocolCommandKind.DeleteAllModSettings =>
                executor.DeleteModSettings(command, cancellationToken),
            ProtocolCommandKind.LaunchInstance => executor.LaunchInstance(command, cancellationToken),
            ProtocolCommandKind.OpenModLocation => executor.OpenModLocation(command, cancellationToken),
            ProtocolCommandKind.ImportPresetShare => executor.ImportPresetShare(command, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }
}
