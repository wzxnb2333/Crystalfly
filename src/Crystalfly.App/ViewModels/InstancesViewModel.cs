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

    [ObservableProperty]
    public partial string GameDirectoryScanProgress { get; set; } = string.Empty;

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
                async token =>
                {
                    if (new SystemHollowKnightProcessProbe(GameDefinition.Get(dependencies.GetSettings().SelectedGameId).ProcessName).IsRunning())
                    {
                        throw new InvalidOperationException(dependencies.Loc()["CloseGameFirst"]);
                    }
                    if (source.GameId == GameId.HollowKnight && !await dependencies.IsVanillaInstanceAsync(source))
                    {
                        throw new InvalidOperationException(dependencies.Loc()["CloneVanillaOnly"]);
                    }
                    clone = await InstanceCloneService.CloneAsync(
                        source.RootPath,
                        dependencies.GetCloneInstanceName().Trim(),
                        Guid.NewGuid().ToString("N"), token);
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
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
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
                    if (new SystemHollowKnightProcessProbe(GameDefinition.Get(dependencies.GetSettings().SelectedGameId).ProcessName).IsRunning())
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
            var renamed = Instances.FirstOrDefault(instance => instance.Id == instanceId);
            dependencies.SetSelectedInstance(renamed);
            if (selected.Record.Purpose == InstancePurpose.OfficialSpeedrun)
            {
                dependencies.SetSelectedSpeedrunInstance(renamed);
                dependencies.SetCurrentPage("Speedrun");
            }
            else
            {
                dependencies.SetCurrentManageTab("Overview");
                dependencies.SetCurrentPage("Manage");
            }
            dependencies.NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
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
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
        finally
        {
            dependencies.SetIsBusy(false);
        }
    }

    internal async Task InitializeGameDirectoriesAsync()
    {
        Interlocked.Increment(ref gameDirectorySelectionVersion);
        GameDirectoryCandidates.Clear();
        var settings = dependencies.GetSettings();
        var registrations = settings.GameDirectories.Where(item => item.GameId == settings.SelectedGameId).ToList();
        if (registrations.Count == 0 && Directory.Exists(dependencies.GetVersionRoot()))
        {
            var versionRoot = dependencies.GetVersionRoot();
            registrations.Add(new GameDirectoryRegistration
            {
                GameId = settings.SelectedGameId,
                Path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot)),
                DisplayName = DirectoryName(versionRoot),
                Source = GameDirectorySourceKind.Managed
            });
            dependencies.SetSettings(settings with
            {
                GameDirectories = settings.GameDirectories.Where(item => item.GameId != settings.SelectedGameId).Concat(registrations).ToArray(),
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
            UpdateActiveDirectoryScanStatus();
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
        var entered = false;
        try
        {
            await gameDirectoryActivationGate.WaitAsync(dependencies.LifetimeCancellation);
            entered = true;
            if (selectionVersion != Volatile.Read(ref gameDirectorySelectionVersion)
                || directory.Registration.GameId != dependencies.GetSettings().SelectedGameId)
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
            if (selectionVersion != Volatile.Read(ref gameDirectorySelectionVersion)) return;
            directory.InstanceCount = Instances.Count;
            UpdateActiveDirectoryScanStatus();
        }
        catch (OperationCanceledException) when (dependencies.LifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or ArgumentException or System.Text.Json.JsonException)
        {
            directory.ScanStatus = dependencies.Loc()["ScanFailed"];
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
        finally
        {
            if (entered)
            {
                gameDirectoryActivationGate.Release();
            }
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
        var scanGame = dependencies.GetSettings().SelectedGameId;
        dependencies.SetErrorMessage(null);
        try
        {
            var game = GameDefinition.Get(scanGame);
            var discovered = await new SteamLibraryDiscoveryService(new WindowsSteamInstallPathProvider(), game.SteamAppId, game.ExecutableName, game.DataDirectoryName)
                .DiscoverAsync(dependencies.LifetimeCancellation);
            if (scanGame != dependencies.GetSettings().SelectedGameId) return;
            AddCandidates(discovered.Select(candidate => new GameDirectoryCandidate
            {
                Path = candidate.GamePath,
                DisplayName = candidate.DisplayName,
                Source = GameDirectorySourceKind.Steam
            }));

            var treeScan = await new GameDirectoryTreeScanner().ScanAllDrivesAsync(
                dependencies.LifetimeCancellation,
                new Progress<int>(checkedDirectories => GameDirectoryScanProgress =
                    string.Format(dependencies.Loc()["ScanGameDirectoriesProgress"], checkedDirectories)));
            if (scanGame != dependencies.GetSettings().SelectedGameId) return;
            AddCandidates(treeScan.Candidates.Where(item => GameDefinition.Detect(item.Path)?.Id == scanGame));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
        }
        finally
        {
            IsScanningGameDirectories = false;
        }
    }

    internal async Task<CustomGameDirectoryScanOutcome> AddCustomGameDirectoryAsync(string path)
    {
        var scanGame = dependencies.GetSettings().SelectedGameId;
        var result = await new GameDirectoryScanner().ScanAsync(path, dependencies.LifetimeCancellation);
        if (scanGame != dependencies.GetSettings().SelectedGameId)
        {
            return CustomGameDirectoryScanOutcome.GameChanged;
        }
        var ownCandidates = result.Candidates
            .Where(item => GameDefinition.Detect(item.Path)?.Id == scanGame)
            .ToArray();
        AddCandidates(ownCandidates);
        if (ownCandidates.Length > 0)
        {
            return CustomGameDirectoryScanOutcome.CandidatesFound;
        }
        if (result.Candidates.Count > 0)
        {
            dependencies.SetErrorMessage(dependencies.Loc()["NoGameDirectoryCandidates"]);
            return CustomGameDirectoryScanOutcome.OtherGameInstallation;
        }
        if (!InstanceDirectory.IsEmptyVersionRoot(path))
        {
            dependencies.SetErrorMessage(dependencies.Loc()["VersionRootFolderMustBeEmpty"]);
            return CustomGameDirectoryScanOutcome.NotAnEmptyFolder;
        }
        if (InstanceDirectory.IsNestedInVersionRoot(path))
        {
            dependencies.SetErrorMessage(dependencies.Loc()["VersionRootFolderMustNotBeNested"]);
            return CustomGameDirectoryScanOutcome.NestedVersionRoot;
        }
        dependencies.SetErrorMessage(null);
        return CustomGameDirectoryScanOutcome.NoGameInstallation;
    }

    /// <summary>
    /// Registers the selected empty folder itself as a version root so a folder
    /// can be managed before any game files exist. Game instances are still
    /// discovered only from complete installations in its direct subdirectories.
    /// </summary>
    internal async Task<bool> AddGameDirectoryAsVersionRootAsync(string path)
    {
        string root;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            InstanceDirectory.PrepareVersionRootRegistration(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or ArgumentException or NotSupportedException)
        {
            dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
            return false;
        }

        var settings = dependencies.GetSettings();
        var gameId = settings.SelectedGameId;
        var existing = GameDirectories.FirstOrDefault(directory =>
            string.Equals(directory.Path, root, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new GameDirectoryItemViewModel(new GameDirectoryRegistration
            {
                GameId = gameId,
                Path = root,
                DisplayName = DirectoryName(root),
                Source = GameDirectorySourceKind.Managed
            })
            {
                ScanStatus = dependencies.Loc()["ScanPending"]
            };
            GameDirectories.Add(existing);
        }
        dependencies.SetSettings(settings with
        {
            GameDirectories = settings.GameDirectories
                .Where(item => item.GameId != settings.SelectedGameId)
                .Concat(GameDirectories.Select(directory => directory.Registration))
                .ToArray(),
            GameDirectoryDiscoveryCompleted = true,
            VersionRoot = root,
            CurrentInstanceId = null
        });
        // Invalidate a pending activation of the previously selected directory so it
        // cannot overwrite the version root we are applying here.
        var selectionVersion = Interlocked.Increment(ref gameDirectorySelectionVersion);
        suppressGameDirectorySelection = true;
        SelectedGameDirectory = existing;
        suppressGameDirectorySelection = false;
        dependencies.SetVersionRoot(root);
        dependencies.QueueSettingsSave();
        try
        {
            await dependencies.RefreshInstances();
        }
        catch (OperationCanceledException) when (dependencies.LifetimeCancellation.IsCancellationRequested)
        {
            return true;
        }
        if (selectionVersion != Volatile.Read(ref gameDirectorySelectionVersion)
            || gameId != dependencies.GetSettings().SelectedGameId
            || !ReferenceEquals(SelectedGameDirectory, existing))
        {
            return true;
        }
        if (!string.IsNullOrWhiteSpace(dependencies.GetErrorMessage()))
        {
            return false;
        }
        existing.InstanceCount = Instances.Count;
        UpdateActiveDirectoryScanStatus();
        dependencies.SetStatusMessage(dependencies.Loc()["EmptyGameDirectoryAdded"]);
        OnPropertyChanged(nameof(IsGameDirectoryDiscoveryRequired));
        return true;
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
            try
            {
                await RegisterGameDirectoryAsync(item.Candidate, steamRiskAccepted: false);
                GameDirectoryCandidates.Remove(item);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidDataException or ArgumentException)
            {
                dependencies.SetErrorMessage(dependencies.Loc().ErrorMessageFor(exception));
            }
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
            if (new SystemHollowKnightProcessProbe(GameDefinition.Get(dependencies.GetSettings().SelectedGameId).ProcessName).IsRunning())
            {
                throw new InvalidOperationException(dependencies.Loc()["CloseGameFirst"]);
            }
            if (dependencies.HasBlockingDownloadForPath(source))
            {
                throw new InvalidOperationException(dependencies.Loc()["MigrationBlockedByDownload"]);
            }
            InstanceDirectory.RejectReparseAncestors(dependencies.GetVersionDataRoot(sourceVersionRoot));
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
        var record = await InstanceSidecar.LoadAsync(source, dependencies.LifetimeCancellation);
        if (record is not null)
        {
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

    internal async Task UseDownloadDirectoryAsync(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        InstanceDirectory.RejectReparseAncestors(root);
        if (!Directory.Exists(root) || GameDefinition.Detect(root) is not null)
            throw new InvalidOperationException(dependencies.Loc()["ChooseDownloadLibraryHint"]);
        var settings = dependencies.GetSettings();
        var registration = new GameDirectoryRegistration { GameId = settings.SelectedGameId, Path = root, DisplayName = DirectoryName(root), Source = GameDirectorySourceKind.Managed };
        var existing = GameDirectories.FirstOrDefault(item => string.Equals(item.Path, root, StringComparison.OrdinalIgnoreCase));
        if (existing is null) { existing = new GameDirectoryItemViewModel(registration); GameDirectories.Add(existing); }
        dependencies.SetSettings(settings with { VersionRoot = root, CurrentInstanceId = null,
            GameDirectories = settings.GameDirectories.Where(item => item.GameId != settings.SelectedGameId).Concat(GameDirectories.Select(item => item.Registration)).ToArray() });
        dependencies.SetVersionRoot(root);
        dependencies.QueueSettingsSave();
        // Invalidate a pending activation of the previously selected directory so it
        // cannot overwrite the version root we are applying here.
        Interlocked.Increment(ref gameDirectorySelectionVersion);
        suppressGameDirectorySelection = true;
        SelectedGameDirectory = existing;
        suppressGameDirectorySelection = false;
        await dependencies.RefreshInstances();
    }

    private async Task RegisterGameDirectoryAsync(GameDirectoryCandidate candidate, bool steamRiskAccepted)
    {
        var instancePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate.Path));
        var rootPath = Directory.GetParent(instancePath)?.FullName
            ?? throw new InvalidOperationException(dependencies.Loc()["InvalidGameDirectory"]);
        var registration = new GameDirectoryRegistration
        {
            GameId = dependencies.GetSettings().SelectedGameId,
            Path = Path.TrimEndingDirectorySeparator(rootPath),
            DisplayName = DirectoryName(rootPath),
            Source = candidate.Source,
            SteamRiskAccepted = steamRiskAccepted
        };
        InstanceDirectory.PrepareRegistration(instancePath);
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
            GameDirectories = settings.GameDirectories.Where(item => item.GameId != settings.SelectedGameId).Concat(GameDirectories.Select(directory => directory.Registration)).ToArray(),
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
            GameDirectories = settings.GameDirectories.Where(item => item.GameId != settings.SelectedGameId).Concat(GameDirectories.Select(directory => directory.Registration)).ToArray(),
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
            UpdateActiveDirectoryScanStatus();
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
            UpdateActiveDirectoryScanStatus();
        }
        await RefreshInactiveGameDirectoriesAsync();
    }

    private async Task RefreshInactiveGameDirectoriesAsync()
    {
        foreach (var directory in GameDirectories.Where(directory => !ReferenceEquals(directory, SelectedGameDirectory)).ToArray())
        {
            try
            {
                directory.ScanStatus = dependencies.Loc()["Scanning"];
                var records = await dependencies.DiscoverInstances(
                    directory.Path,
                    dependencies.GetCatalog(),
                    dependencies.LifetimeCancellation);
                directory.InstanceCount = records.Count(item => item.GameId == directory.Registration.GameId);
                directory.ScanStatus = dependencies.Loc()[
                    records is InstanceDiscoveryResult { Issues.Count: > 0 }
                        ? records.Count > 0 ? "ScanPartial" : "ScanFailed"
                        : "ScanReady"];
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

    internal void UpdateActiveDirectoryScanStatus()
    {
        if (SelectedGameDirectory is not { } directory)
        {
            return;
        }
        directory.InstanceCount = Instances.Count;
        directory.ScanStatus = dependencies.Loc()[!Directory.Exists(directory.Path)
            ? "ScanFailed"
            : !string.IsNullOrWhiteSpace(dependencies.GetErrorMessage())
                ? Instances.Count > 0 ? "ScanPartial" : "ScanFailed"
                : "ScanReady"];
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

internal enum CustomGameDirectoryScanOutcome
{
    CandidatesFound,
    NoGameInstallation,
    NotAnEmptyFolder,
    NestedVersionRoot,
    OtherGameInstallation,
    GameChanged
}
