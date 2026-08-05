using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Transactions;
using Crystalfly.Steam.Discovery;

namespace Crystalfly.App.ViewModels;

public sealed partial class InstancesViewModel : ViewModelBase
{
    private readonly InstancesDependencies dependencies;
    private bool gameDirectoryStateInitialized;
    private bool suppressGameDirectorySelection;
    private readonly SemaphoreSlim gameDirectoryActivationGate = new(1, 1);
    private int gameDirectorySelectionVersion;

    internal InstancesViewModel(InstancesDependencies dependencies)
    {
        this.dependencies = dependencies;
    }

    public event Action? GameDirectoryDiscoveryRequested;

    public event Action<GameDirectoryCandidateItemViewModel>? SteamDirectoryRiskRequested;

    public ObservableCollection<InstanceItemViewModel> Instances { get; } = [];

    public ObservableCollection<InstanceItemViewModel> VisibleInstances { get; } = [];

    public ObservableCollection<InstanceItemViewModel> SpeedrunInstances { get; } = [];

    public ObservableCollection<GameDirectoryItemViewModel> GameDirectories { get; } = [];

    public ObservableCollection<GameDirectoryCandidateItemViewModel> GameDirectoryCandidates { get; } = [];

    [ObservableProperty]
    public partial GameDirectoryItemViewModel? SelectedGameDirectory { get; set; }

    [ObservableProperty]
    public partial bool IsScanningGameDirectories { get; set; }

    public bool IsGameDirectoryDiscoveryRequired =>
        GameDirectories.Count == 0 && !dependencies.GetSettings().GameDirectoryDiscoveryCompleted;

    internal void ApplyInstanceFilter()
    {
        VisibleInstances.Clear();
        foreach (var instance in Instances
                     .Where(instance =>
                         string.IsNullOrWhiteSpace(dependencies.GetSearchText())
                         || instance.Name.Contains(dependencies.GetSearchText(), StringComparison.OrdinalIgnoreCase)
                         || instance.DisplayVersion.Contains(dependencies.GetSearchText(), StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(instance => instance.IsFavorite)
                     .ThenBy(instance => instance.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            VisibleInstances.Add(instance);
        }
    }

    [RelayCommand]
    private void ToggleFavoriteInstance(InstanceItemViewModel? instance)
    {
        if (instance is null)
        {
            return;
        }

        var updated = instance with { IsFavorite = !instance.IsFavorite };
        var index = Instances.IndexOf(instance);
        if (index >= 0)
        {
            Instances[index] = updated;
        }
        if (dependencies.GetSelectedInstance()?.Id == instance.Id)
        {
            dependencies.SetSelectedInstance(updated);
        }
        var settings = dependencies.GetSettings();
        dependencies.SetSettings(settings with
        {
            FavoriteInstanceIds = updated.IsFavorite
                ? settings.FavoriteInstanceIds
                    .Append(updated.Id)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : settings.FavoriteInstanceIds
                    .Where(id => !string.Equals(id, updated.Id, StringComparison.Ordinal))
                    .ToArray()
        });
        ApplyInstanceFilter();
        dependencies.QueueSettingsSave();
    }

    [RelayCommand]
    private void SelectInstanceForLaunch(InstanceItemViewModel? instance)
    {
        if (!dependencies.GetCanNavigate() || instance is null)
        {
            return;
        }
        dependencies.SetSelectedInstance(instance);
        dependencies.SetCurrentPage("Launch");
    }

    [RelayCommand]
    private async Task CloneSelectedInstanceAsync()
    {
        if (dependencies.GetSelectedInstance() is null)
        {
            dependencies.SetErrorMessage(dependencies.Loc()["NoInstance"]);
            return;
        }
        if (string.IsNullOrWhiteSpace(dependencies.GetCloneInstanceName()))
        {
            dependencies.SetErrorMessage(dependencies.Loc()["CloneNameRequired"]);
            return;
        }
        if (dependencies.IsMutationBlocked())
        {
            return;
        }

        dependencies.SetIsBusy(true);
        dependencies.SetErrorMessage(null);
        try
        {
            var source = dependencies.GetSelectedInstance()!.Record;
            InstanceRecord? clone = null;
            await dependencies.RunCoordinated(
                source.Id,
                async _ =>
                {
                    if (new SystemHollowKnightProcessProbe().IsRunning())
                    {
                        throw new InvalidOperationException(dependencies.Loc()["CloseGameFirst"]);
                    }
                    if (!await dependencies.IsVanillaInstanceAsync(source))
                    {
                        throw new InvalidOperationException(dependencies.Loc()["CloneVanillaOnly"]);
                    }
                    clone = await InstanceCloneService.CloneAsync(
                        source.RootPath,
                        dependencies.GetCloneInstanceName().Trim(),
                        Guid.NewGuid().ToString("N"));
                },
                dependencies.LifetimeCancellation);
            var createdClone = clone
                ?? throw new InvalidOperationException("The instance clone was not created.");
            dependencies.SetCloneInstanceName(string.Empty);
            await dependencies.RefreshInstances();
            var selectedClone = Instances.FirstOrDefault(instance => instance.Id == createdClone.Id);
            if (selectedClone is not null)
            {
                dependencies.SetSelectedInstance(selectedClone);
                dependencies.SetCurrentPage("Launch");
            }
            dependencies.NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            dependencies.SetErrorMessage($"{dependencies.Loc()["OperationFailed"]}: {exception.Message}");
        }
        finally
        {
            dependencies.SetIsBusy(false);
        }
    }

    [RelayCommand]
    private async Task RenameInstanceAsync(string? newName)
    {
        if (dependencies.GetSelectedInstance() is not { } selected || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }
        if (dependencies.IsMutationBlocked())
        {
            return;
        }

        dependencies.SetIsBusy(true);
        dependencies.SetErrorMessage(null);
        try
        {
            var instanceId = selected.Id;
            await dependencies.RunCoordinated(
                instanceId,
                async cancellationToken =>
                {
                    if (new SystemHollowKnightProcessProbe().IsRunning())
                    {
                        throw new InvalidOperationException(dependencies.Loc()["CloseGameFirst"]);
                    }
                    var conditions = await dependencies.EvaluateDeletionConditions(instanceId, cancellationToken);
                    if (conditions.HasBlockingQueueTasks)
                    {
                        throw new InvalidOperationException(dependencies.Loc()["RenameBlockedDownloads"]);
                    }
                    if (!conditions.TransactionsHealthy)
                    {
                        throw new InvalidOperationException(dependencies.Loc()["RenameBlockedTransactions"]);
                    }
                    await InstanceRenameService.RenameAsync(selected.Record, newName, cancellationToken);
                },
                dependencies.LifetimeCancellation);

            await dependencies.RefreshInstancesQuietly();
            dependencies.SetSelectedInstance(Instances.FirstOrDefault(instance => instance.Id == instanceId));
            dependencies.SetCurrentManageTab("Overview");
            dependencies.SetCurrentPage("Manage");
            dependencies.NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            dependencies.SetErrorMessage($"{dependencies.Loc()["OperationFailed"]}: {exception.Message}");
        }
        finally
        {
            dependencies.SetIsBusy(false);
        }
    }

    [RelayCommand]
    private async Task DeleteInstanceAsync(InstanceItemViewModel? instance)
    {
        if (instance is null || dependencies.IsMutationBlocked())
        {
            return;
        }

        var originalIndex = Instances.IndexOf(instance);
        bool wasSpeedrunInstance = instance.Record.Purpose == InstancePurpose.OfficialSpeedrun;
        var nextId = Instances
            .Where(candidate => !string.Equals(candidate.Id, instance.Id, StringComparison.Ordinal))
            .ElementAtOrDefault(Math.Min(Math.Max(originalIndex, 0), Math.Max(Instances.Count - 2, 0)))
            ?.Id;
        dependencies.SetIsBusy(true);
        dependencies.SetErrorMessage(null);
        try
        {
            InstanceDeletionResult? result = null;
            await dependencies.RunCoordinated(
                instance.Id,
                async cancellationToken =>
                {
                    Func<CancellationToken, ValueTask<InstanceDeletionConditions>> evaluateConditions =
                        token => dependencies.EvaluateDeletionConditions(instance.Id, token);
                    result = dependencies.InstanceDeletionOverride is null
                        ? await new InstanceDeletionService(dependencies.GetVersionRoot()).DeleteAsync(
                            instance.Record,
                            evaluateConditions,
                            cancellationToken)
                        : await dependencies.InstanceDeletionOverride(
                            instance.Record,
                            evaluateConditions,
                            cancellationToken);
                },
                dependencies.LifetimeCancellation);

            Instances.Remove(instance);
            VisibleInstances.Remove(instance);
            SpeedrunInstances.Remove(instance);
            if (dependencies.GetSelectedInstance()?.Id == instance.Id)
            {
                dependencies.SetSelectedInstance(nextId is null
                    ? Instances.FirstOrDefault()
                    : Instances.FirstOrDefault(candidate => candidate.Id == nextId)
                        ?? Instances.FirstOrDefault());
            }
            if (dependencies.GetSelectedSpeedrunInstance()?.Id == instance.Id)
            {
                dependencies.SetSelectedSpeedrunInstance(SpeedrunInstances.FirstOrDefault());
            }
            var settings = dependencies.GetSettings();
            dependencies.SetSettings(settings with
            {
                CurrentInstanceId = dependencies.GetSelectedInstance()?.Id,
                FavoriteInstanceIds = settings.FavoriteInstanceIds
                    .Where(id => !string.Equals(id, instance.Id, StringComparison.Ordinal))
                    .ToArray()
            });
            dependencies.QueueSettingsSave();
            dependencies.SetCurrentPage(wasSpeedrunInstance ? "Speedrun" : "Launch");
            if (result is { CleanupCompleted: false })
            {
                var status = dependencies.Loc()["DeleteCleanupPending"];
                dependencies.SetStatusMessage(status);
                NotifyToast(status);
            }
            else
            {
                dependencies.NotifyOperationCompleted();
            }
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            dependencies.SetErrorMessage($"{dependencies.Loc()["OperationFailed"]}: {exception.Message}");
        }
        finally
        {
            dependencies.SetIsBusy(false);
        }
    }

    internal async Task InitializeGameDirectoriesAsync()
    {
        var settings = dependencies.GetSettings();
        var registrations = settings.GameDirectories.ToList();
        if (registrations.Count == 0 && Directory.Exists(dependencies.GetVersionRoot()))
        {
            var versionRoot = dependencies.GetVersionRoot();
            registrations.Add(new GameDirectoryRegistration
            {
                Path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot)),
                DisplayName = DirectoryName(versionRoot),
                Source = GameDirectorySourceKind.Managed
            });
            dependencies.SetSettings(settings with
            {
                GameDirectories = registrations,
                GameDirectoryDiscoveryCompleted = true
            });
            dependencies.QueueSettingsSave();
        }

        GameDirectories.Clear();
        foreach (var registration in registrations)
        {
            GameDirectories.Add(new GameDirectoryItemViewModel(registration)
            {
                ScanStatus = Directory.Exists(registration.Path)
                    ? dependencies.Loc()["ScanPending"]
                    : dependencies.Loc()["ScanFailed"]
            });
        }

        suppressGameDirectorySelection = true;
        SelectedGameDirectory = GameDirectories.FirstOrDefault(directory =>
            string.Equals(directory.Path, dependencies.GetVersionRoot(), StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(directory.Path))
            ?? GameDirectories.FirstOrDefault(directory => Directory.Exists(directory.Path))
            ?? GameDirectories.FirstOrDefault();
        dependencies.SetVersionRoot(SelectedGameDirectory?.Path ?? string.Empty);
        suppressGameDirectorySelection = false;
        gameDirectoryStateInitialized = true;
        OnPropertyChanged(nameof(IsGameDirectoryDiscoveryRequired));
    }

    internal async Task CompleteGameDirectoryInitializationAsync()
    {
        if (SelectedGameDirectory is not null)
        {
            SelectedGameDirectory.InstanceCount = Instances.Count;
            SelectedGameDirectory.ScanStatus = dependencies.Loc()["ScanReady"];
        }
        _ = RefreshInactiveGameDirectoriesAsync();
        if (dependencies.AutoRequestGameDirectoryDiscovery && IsGameDirectoryDiscoveryRequired)
        {
            GameDirectoryDiscoveryRequested?.Invoke();
        }
        await Task.CompletedTask;
    }

    partial void OnSelectedGameDirectoryChanged(GameDirectoryItemViewModel? value)
    {
        if (!gameDirectoryStateInitialized || suppressGameDirectorySelection || value is null)
        {
            return;
        }
        var selectionVersion = Interlocked.Increment(ref gameDirectorySelectionVersion);
        _ = ActivateGameDirectoryAsync(value, selectionVersion);
    }

    private async Task ActivateGameDirectoryAsync(
        GameDirectoryItemViewModel directory,
        int selectionVersion)
    {
        await gameDirectoryActivationGate.WaitAsync(dependencies.LifetimeCancellation);
        try
        {
            if (selectionVersion != Volatile.Read(ref gameDirectorySelectionVersion))
            {
                return;
            }
            dependencies.SetVersionRoot(directory.Path);
            dependencies.SetSettings(dependencies.GetSettings() with
            {
                VersionRoot = directory.Path,
                CurrentInstanceId = null
            });
            dependencies.QueueSettingsSave();
            await dependencies.RefreshInstances();
            directory.InstanceCount = Instances.Count;
            directory.ScanStatus = Directory.Exists(directory.Path)
                ? dependencies.Loc()["ScanReady"]
                : dependencies.Loc()["ScanFailed"];
        }
        finally
        {
            gameDirectoryActivationGate.Release();
        }
    }

    [RelayCommand]
    private void RequestGameDirectoryDiscovery() => GameDirectoryDiscoveryRequested?.Invoke();

    [RelayCommand]
    private async Task ScanGameDirectoriesAsync()
    {
        if (IsScanningGameDirectories)
        {
            return;
        }
        IsScanningGameDirectories = true;
        dependencies.SetErrorMessage(null);
        try
        {
            var discovered = await new SteamLibraryDiscoveryService(new WindowsSteamInstallPathProvider())
                .DiscoverAsync(dependencies.LifetimeCancellation);
            AddCandidates(discovered.Select(candidate => new GameDirectoryCandidate
            {
                Path = candidate.GamePath,
                DisplayName = candidate.DisplayName,
                Source = GameDirectorySourceKind.Steam
            }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            dependencies.SetErrorMessage($"{dependencies.Loc()["OperationFailed"]}: {exception.Message}");
        }
        finally
        {
            IsScanningGameDirectories = false;
        }
    }

    internal async Task AddCustomGameDirectoryAsync(string path)
    {
        var result = await new GameDirectoryScanner().ScanAsync(path, dependencies.LifetimeCancellation);
        AddCandidates(result.Candidates);
        if (result.Candidates.Count == 0)
        {
            dependencies.SetErrorMessage(dependencies.Loc()["NoGameDirectoryCandidates"]);
        }
    }

    private void AddCandidates(IEnumerable<GameDirectoryCandidate> candidates)
    {
        var registered = GameDirectories.Select(directory => directory.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = GameDirectoryCandidates.Select(candidate => candidate.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate.Path));
            var parent = Directory.GetParent(path)?.FullName;
            if (parent is null || registered.Contains(parent) || !existing.Add(path))
            {
                continue;
            }
            GameDirectoryCandidates.Add(new GameDirectoryCandidateItemViewModel(candidate with { Path = path }));
        }
        OnPropertyChanged(nameof(IsGameDirectoryDiscoveryRequired));
    }

    [RelayCommand]
    private async Task ConfirmGameDirectoryCandidatesAsync()
    {
        foreach (var item in GameDirectoryCandidates.Where(candidate => candidate.IsConfirmed).ToArray())
        {
            if (item.IsSteam)
            {
                SteamDirectoryRiskRequested?.Invoke(item);
                break;
            }
            await RegisterGameDirectoryAsync(item.Candidate, steamRiskAccepted: false);
            GameDirectoryCandidates.Remove(item);
        }
    }

    internal async Task AcceptSteamGameDirectoryAsync(GameDirectoryCandidateItemViewModel item)
    {
        await RegisterGameDirectoryAsync(item.Candidate, steamRiskAccepted: true);
        GameDirectoryCandidates.Remove(item);
    }

    internal async Task MigrateSteamGameDirectoryAsync(GameDirectoryCandidateItemViewModel item, string destinationRoot)
    {
        var normalizedDestinationRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationRoot));
        if (GameDirectories.Any(directory => directory.IsSteam
            && (string.Equals(directory.Path, normalizedDestinationRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedDestinationRoot.StartsWith(
                    directory.Path + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException(dependencies.Loc()["MigrationTargetMustNotBeSteam"]);
        }

        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path));
        var destination = Path.Combine(normalizedDestinationRoot, Path.GetFileName(source));
        var sourceVersionRoot = Directory.GetParent(source)?.FullName
            ?? throw new InvalidOperationException(dependencies.Loc()["InvalidGameDirectory"]);
        async Task<GameDirectoryMigrationResult> MigrateCoreAsync(CancellationToken cancellationToken)
        {
            if (new SystemHollowKnightProcessProbe().IsRunning())
            {
                throw new InvalidOperationException(dependencies.Loc()["CloseGameFirst"]);
            }
            if (dependencies.HasBlockingDownloadForPath(source))
            {
                throw new InvalidOperationException(dependencies.Loc()["MigrationBlockedByDownload"]);
            }
            var recoveries = await FileTransaction.RecoverPendingAsync(
                Path.Combine(dependencies.GetVersionDataRoot(sourceVersionRoot), "transactions"),
                cancellationToken);
            if (recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention))
            {
                throw new InvalidOperationException(dependencies.Loc()["RecoveryNeedsAttention"]);
            }
            return await new GameDirectoryMigrationService().MigrateAsync(
                source,
                destination,
                cancellationToken);
        }

        GameDirectoryMigrationResult result;
        if (File.Exists(InstanceSidecar.GetMarkerPath(source)))
        {
            var record = await InstanceSidecar.LoadAsync(source, dependencies.LifetimeCancellation);
            GameDirectoryMigrationResult? coordinatedResult = null;
            await dependencies.RunCoordinated(
                record.Id,
                async cancellationToken => coordinatedResult = await MigrateCoreAsync(cancellationToken),
                dependencies.LifetimeCancellation);
            result = coordinatedResult
                ?? throw new InvalidOperationException(dependencies.Loc()["OperationFailed"]);
        }
        else
        {
            result = await MigrateCoreAsync(dependencies.LifetimeCancellation);
        }
        await RegisterGameDirectoryAsync(new GameDirectoryCandidate
        {
            Path = result.DestinationPath,
            DisplayName = Path.GetFileName(result.DestinationPath),
            Source = GameDirectorySourceKind.Managed
        }, steamRiskAccepted: false);
        GameDirectoryCandidates.Remove(item);
        if (!result.SourceCleanupCompleted && !string.IsNullOrWhiteSpace(result.SourceCleanupError))
        {
            dependencies.SetErrorMessage(result.SourceCleanupError);
        }
    }

    private async Task RegisterGameDirectoryAsync(GameDirectoryCandidate candidate, bool steamRiskAccepted)
    {
        var instancePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate.Path));
        var rootPath = Directory.GetParent(instancePath)?.FullName
            ?? throw new InvalidOperationException(dependencies.Loc()["InvalidGameDirectory"]);
        var registration = new GameDirectoryRegistration
        {
            Path = Path.TrimEndingDirectorySeparator(rootPath),
            DisplayName = DirectoryName(rootPath),
            Source = candidate.Source,
            SteamRiskAccepted = steamRiskAccepted
        };
        var existing = GameDirectories.FirstOrDefault(directory =>
            string.Equals(directory.Path, registration.Path, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new GameDirectoryItemViewModel(registration) { ScanStatus = dependencies.Loc()["ScanPending"] };
            GameDirectories.Add(existing);
        }
        else
        {
            existing.UpdateRegistration(registration);
        }
        var settings = dependencies.GetSettings();
        dependencies.SetSettings(settings with
        {
            GameDirectories = GameDirectories.Select(directory => directory.Registration).ToArray(),
            GameDirectoryDiscoveryCompleted = true,
            VersionRoot = registration.Path
        });
        foreach (var pending in GameDirectoryCandidates
                     .Where(item => string.Equals(
                         Directory.GetParent(item.Path)?.FullName,
                         registration.Path,
                         StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            GameDirectoryCandidates.Remove(pending);
        }
        dependencies.QueueSettingsSave();
        SelectedGameDirectory = existing;
        OnPropertyChanged(nameof(IsGameDirectoryDiscoveryRequired));
    }

    [RelayCommand]
    private async Task RemoveSelectedGameDirectoryAsync()
    {
        if (SelectedGameDirectory is not { } selected)
        {
            return;
        }
        var index = GameDirectories.IndexOf(selected);
        GameDirectories.Remove(selected);
        suppressGameDirectorySelection = true;
        SelectedGameDirectory = GameDirectories.ElementAtOrDefault(Math.Min(index, Math.Max(0, GameDirectories.Count - 1)));
        suppressGameDirectorySelection = false;
        dependencies.SetVersionRoot(SelectedGameDirectory?.Path ?? string.Empty);
        Instances.Clear();
        VisibleInstances.Clear();
        dependencies.SetSelectedInstance(null);
        var settings = dependencies.GetSettings();
        dependencies.SetSettings(settings with
        {
            GameDirectories = GameDirectories.Select(directory => directory.Registration).ToArray(),
            VersionRoot = string.IsNullOrWhiteSpace(dependencies.GetVersionRoot())
                ? null
                : dependencies.GetVersionRoot(),
            CurrentInstanceId = null
        });
        dependencies.QueueSettingsSave();
        if (SelectedGameDirectory is not null)
        {
            await dependencies.RefreshInstances();
            SelectedGameDirectory.InstanceCount = Instances.Count;
            SelectedGameDirectory.ScanStatus = dependencies.Loc()["ScanReady"];
        }
        OnPropertyChanged(nameof(IsGameDirectoryDiscoveryRequired));
    }

    internal Task UnregisterCurrentSteamDirectoryAsync() => RemoveSelectedGameDirectoryAsync();

    [RelayCommand]
    private async Task RefreshGameDirectoriesAsync()
    {
        if (SelectedGameDirectory is not null)
        {
            SelectedGameDirectory.ScanStatus = dependencies.Loc()["Scanning"];
            await dependencies.RefreshInstances();
            SelectedGameDirectory.InstanceCount = Instances.Count;
            SelectedGameDirectory.ScanStatus = dependencies.Loc()["ScanReady"];
        }
        await RefreshInactiveGameDirectoriesAsync();
    }

    private async Task RefreshInactiveGameDirectoriesAsync()
    {
        foreach (var directory in GameDirectories.Where(directory => !ReferenceEquals(directory, SelectedGameDirectory)))
        {
            try
            {
                directory.ScanStatus = dependencies.Loc()["Scanning"];
                var records = await dependencies.DiscoverInstances(
                    directory.Path,
                    dependencies.GetCatalog(),
                    dependencies.LifetimeCancellation);
                directory.InstanceCount = records.Count;
                directory.ScanStatus = dependencies.Loc()["ScanReady"];
            }
            catch (OperationCanceledException) when (dependencies.LifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                directory.ScanStatus = dependencies.Loc()["ScanFailed"];
            }
        }
    }

    internal void RefreshGameDirectoryLabels()
    {
        foreach (var directory in GameDirectories)
        {
            directory.ScanStatus = Directory.Exists(directory.Path)
                ? dependencies.Loc()["ScanReady"]
                : dependencies.Loc()["ScanFailed"];
        }
    }

    private static string DirectoryName(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var name = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(name) ? fullPath : name;
    }
}
