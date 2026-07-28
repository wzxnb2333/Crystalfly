using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Transactions;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Steam.Discovery;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private bool gameDirectoryStateInitialized;
    private bool suppressGameDirectorySelection;
    private readonly SemaphoreSlim gameDirectoryActivationGate = new(1, 1);
    private int gameDirectorySelectionVersion;

    public event Action? GameDirectoryDiscoveryRequested;

    public event Action<GameDirectoryCandidateItemViewModel>? SteamDirectoryRiskRequested;

    public ObservableCollection<GameDirectoryItemViewModel> GameDirectories { get; } = [];

    public ObservableCollection<GameDirectoryCandidateItemViewModel> GameDirectoryCandidates { get; } = [];

    [ObservableProperty]
    public partial GameDirectoryItemViewModel? SelectedGameDirectory { get; set; }

    [ObservableProperty]
    public partial bool IsScanningGameDirectories { get; set; }

    public bool IsGameDirectoryDiscoveryRequired =>
        GameDirectories.Count == 0 && !settings.GameDirectoryDiscoveryCompleted;

    private async Task InitializeGameDirectoriesAsync()
    {
        var registrations = settings.GameDirectories.ToList();
        if (registrations.Count == 0 && Directory.Exists(VersionRoot))
        {
            registrations.Add(new GameDirectoryRegistration
            {
                Path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(VersionRoot)),
                DisplayName = DirectoryName(VersionRoot),
                Source = GameDirectorySourceKind.Managed
            });
            settings = settings with
            {
                GameDirectories = registrations,
                GameDirectoryDiscoveryCompleted = true
            };
            await QueueSettingsSave();
        }

        GameDirectories.Clear();
        foreach (var registration in registrations)
        {
            GameDirectories.Add(new GameDirectoryItemViewModel(registration)
            {
                ScanStatus = Directory.Exists(registration.Path)
                    ? Loc["ScanPending"]
                    : Loc["ScanFailed"]
            });
        }

        suppressGameDirectorySelection = true;
        SelectedGameDirectory = GameDirectories.FirstOrDefault(directory =>
            string.Equals(directory.Path, VersionRoot, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(directory.Path))
            ?? GameDirectories.FirstOrDefault(directory => Directory.Exists(directory.Path))
            ?? GameDirectories.FirstOrDefault();
        VersionRoot = SelectedGameDirectory?.Path ?? string.Empty;
        suppressGameDirectorySelection = false;
        gameDirectoryStateInitialized = true;
        OnPropertyChanged(nameof(IsGameDirectoryDiscoveryRequired));
    }

    private async Task CompleteGameDirectoryInitializationAsync()
    {
        if (SelectedGameDirectory is not null)
        {
            SelectedGameDirectory.InstanceCount = Instances.Count;
            SelectedGameDirectory.ScanStatus = Loc["ScanReady"];
        }
        _ = RefreshInactiveGameDirectoriesAsync();
        if (autoRequestGameDirectoryDiscovery && IsGameDirectoryDiscoveryRequired)
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
        await gameDirectoryActivationGate.WaitAsync(lifetimeCancellation.Token);
        try
        {
            if (selectionVersion != Volatile.Read(ref gameDirectorySelectionVersion))
            {
                return;
            }
            VersionRoot = directory.Path;
            settings = settings with
            {
                VersionRoot = directory.Path,
                CurrentInstanceId = null
            };
            await QueueSettingsSave();
            await RefreshAsync();
            directory.InstanceCount = Instances.Count;
            directory.ScanStatus = Directory.Exists(directory.Path)
                ? Loc["ScanReady"]
                : Loc["ScanFailed"];
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
        ErrorMessage = null;
        try
        {
            var discovered = await new SteamLibraryDiscoveryService(new WindowsSteamInstallPathProvider())
                .DiscoverAsync(lifetimeCancellation.Token);
            AddCandidates(discovered.Select(candidate => new GameDirectoryCandidate
            {
                Path = candidate.GamePath,
                DisplayName = candidate.DisplayName,
                Source = GameDirectorySourceKind.Steam
            }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
        finally
        {
            IsScanningGameDirectories = false;
        }
    }

    internal async Task AddCustomGameDirectoryAsync(string path)
    {
        var result = await new GameDirectoryScanner().ScanAsync(path, lifetimeCancellation.Token);
        AddCandidates(result.Candidates);
        if (result.Candidates.Count == 0)
        {
            ErrorMessage = Loc["NoGameDirectoryCandidates"];
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
            throw new InvalidOperationException(Loc["MigrationTargetMustNotBeSteam"]);
        }

        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path));
        var destination = Path.Combine(normalizedDestinationRoot, Path.GetFileName(source));
        var sourceVersionRoot = Directory.GetParent(source)?.FullName
            ?? throw new InvalidOperationException(Loc["InvalidGameDirectory"]);
        async Task<GameDirectoryMigrationResult> MigrateCoreAsync(CancellationToken cancellationToken)
        {
            if (new SystemHollowKnightProcessProbe().IsRunning())
            {
                throw new InvalidOperationException(Loc["CloseGameFirst"]);
            }
            if (downloadQueue.Groups.Any(group =>
                string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(group.TargetInstanceRoot)),
                    source,
                    StringComparison.OrdinalIgnoreCase)
                && group.State is DownloadQueueGroupState.Pending
                    or DownloadQueueGroupState.Running
                    or DownloadQueueGroupState.Failed
                    or DownloadQueueGroupState.WaitingForNetwork))
            {
                throw new InvalidOperationException(Loc["MigrationBlockedByDownload"]);
            }
            var recoveries = await FileTransaction.RecoverPendingAsync(
                Path.Combine(paths.GetVersionDataRoot(sourceVersionRoot), "transactions"),
                cancellationToken);
            if (recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention))
            {
                throw new InvalidOperationException(Loc["RecoveryNeedsAttention"]);
            }
            return await new GameDirectoryMigrationService().MigrateAsync(
                source,
                destination,
                cancellationToken);
        }

        GameDirectoryMigrationResult result;
        if (File.Exists(InstanceSidecar.GetMarkerPath(source)))
        {
            var record = await InstanceSidecar.LoadAsync(source, lifetimeCancellation.Token);
            GameDirectoryMigrationResult? coordinatedResult = null;
            await instanceOperationCoordinator.RunAsync(
                record.Id,
                async cancellationToken => coordinatedResult = await MigrateCoreAsync(cancellationToken),
                lifetimeCancellation.Token);
            result = coordinatedResult
                ?? throw new InvalidOperationException(Loc["OperationFailed"]);
        }
        else
        {
            result = await MigrateCoreAsync(lifetimeCancellation.Token);
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
            ErrorMessage = result.SourceCleanupError;
        }
    }

    private async Task RegisterGameDirectoryAsync(GameDirectoryCandidate candidate, bool steamRiskAccepted)
    {
        var instancePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate.Path));
        var rootPath = Directory.GetParent(instancePath)?.FullName
            ?? throw new InvalidOperationException(Loc["InvalidGameDirectory"]);
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
            existing = new GameDirectoryItemViewModel(registration) { ScanStatus = Loc["ScanPending"] };
            GameDirectories.Add(existing);
        }
        else
        {
            existing.UpdateRegistration(registration);
        }
        settings = settings with
        {
            GameDirectories = GameDirectories.Select(directory => directory.Registration).ToArray(),
            GameDirectoryDiscoveryCompleted = true,
            VersionRoot = registration.Path
        };
        foreach (var pending in GameDirectoryCandidates
                     .Where(item => string.Equals(
                         Directory.GetParent(item.Path)?.FullName,
                         registration.Path,
                         StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            GameDirectoryCandidates.Remove(pending);
        }
        await QueueSettingsSave();
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
        VersionRoot = SelectedGameDirectory?.Path ?? string.Empty;
        Instances.Clear();
        VisibleInstances.Clear();
        SelectedInstance = null;
        settings = settings with
        {
            GameDirectories = GameDirectories.Select(directory => directory.Registration).ToArray(),
            VersionRoot = string.IsNullOrWhiteSpace(VersionRoot) ? null : VersionRoot,
            CurrentInstanceId = null
        };
        await QueueSettingsSave();
        if (SelectedGameDirectory is not null)
        {
            await RefreshAsync();
            SelectedGameDirectory.InstanceCount = Instances.Count;
            SelectedGameDirectory.ScanStatus = Loc["ScanReady"];
        }
        OnPropertyChanged(nameof(IsGameDirectoryDiscoveryRequired));
    }

    internal Task UnregisterCurrentSteamDirectoryAsync() => RemoveSelectedGameDirectoryAsync();

    [RelayCommand]
    private async Task RefreshGameDirectoriesAsync()
    {
        if (SelectedGameDirectory is not null)
        {
            SelectedGameDirectory.ScanStatus = Loc["Scanning"];
            await RefreshAsync();
            SelectedGameDirectory.InstanceCount = Instances.Count;
            SelectedGameDirectory.ScanStatus = Loc["ScanReady"];
        }
        await RefreshInactiveGameDirectoriesAsync();
    }

    private async Task RefreshInactiveGameDirectoriesAsync()
    {
        foreach (var directory in GameDirectories.Where(directory => !ReferenceEquals(directory, SelectedGameDirectory)))
        {
            try
            {
                directory.ScanStatus = Loc["Scanning"];
                var records = await instanceDiscovery(directory.Path, catalog, lifetimeCancellation.Token);
                directory.InstanceCount = records.Count;
                directory.ScanStatus = Loc["ScanReady"];
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                directory.ScanStatus = Loc["ScanFailed"];
            }
        }
    }

    private void RefreshGameDirectoryLabels()
    {
        foreach (var directory in GameDirectories)
        {
            directory.ScanStatus = Directory.Exists(directory.Path)
                ? Loc["ScanReady"]
                : Loc["ScanFailed"];
        }
    }

    private static string DirectoryName(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var name = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(name) ? fullPath : name;
    }
}
