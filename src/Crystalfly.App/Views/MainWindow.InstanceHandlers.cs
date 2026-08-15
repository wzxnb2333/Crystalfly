using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Ursa.Controls;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
    private async void AddCustomGameDirectory(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await PickAndAddGameDirectoryAsync(viewModel);
        }
    }

    private async Task PickAndAddGameDirectoryAsync(MainViewModel viewModel)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = viewModel.Loc["SelectGameDirectory"],
            AllowMultiple = false
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.Instances.AddCustomGameDirectoryAsync(path);
            await ShowGameDirectoryDiscoveryAsync(viewModel);
        }
    }

    private async Task ShowGameDirectoryDiscoveryAsync(MainViewModel viewModel)
    {
        var dialog = new GameDirectoryDiscoveryDialogViewModel(
            viewModel.Loc["GameDirectoryDiscoveryTitle"],
            viewModel.Loc["GameDirectoryDiscoveryHint"],
            viewModel.Instances.GameDirectoryCandidates,
            viewModel.Loc["ScanGameDirectories"],
            viewModel.Loc["AddGameDirectory"],
            viewModel.Loc["ConfirmAddDirectories"],
            viewModel.Loc["SkipForNow"]);
        var result = await OverlayDialog.ShowCustomAsync<GameDirectoryDiscoveryDialogView,
            GameDirectoryDiscoveryDialogViewModel, GameDirectoryDiscoveryDialogResult>(
            dialog, OverlayHostId, CreateOverlayOptions());
        switch (result)
        {
            case GameDirectoryDiscoveryDialogResult.StartScan:
                await viewModel.Instances.ScanGameDirectoriesCommand.ExecuteAsync(null);
                await ShowGameDirectoryDiscoveryAsync(viewModel);
                break;
            case GameDirectoryDiscoveryDialogResult.AddCustom:
                await PickAndAddGameDirectoryAsync(viewModel);
                break;
            case GameDirectoryDiscoveryDialogResult.Confirm:
                await viewModel.Instances.ConfirmGameDirectoryCandidatesCommand.ExecuteAsync(null);
                break;
        }
    }

    private async Task ShowOnboardingAsync()
    {
        // Let the first-run game-directory discovery dialog show first so the
        // two overlays never stack on startup.
        await Task.Delay(500);
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var dialog = new OnboardingDialogViewModel(
            key => viewModel.Loc[key],
            viewModel.OnboardingTasks,
            action =>
            {
                viewModel.RunOnboardingAction(action);
                return Task.CompletedTask;
            });
        bool completed = await OverlayDialog.ShowCustomAsync<OnboardingDialogView,
            OnboardingDialogViewModel, bool>(
            dialog, OverlayHostId, CreateOverlayOptions());
        if (completed)
        {
            viewModel.CompleteOnboarding();
        }
    }

    private async Task ShowSteamDirectoryRiskAsync(MainViewModel viewModel, GameDirectoryCandidateItemViewModel candidate)
    {
        var dialog = new ThreeChoiceDialogViewModel(
            viewModel.Loc["SteamDirectoryRiskTitle"],
            viewModel.Loc["SteamDirectoryRiskHint"],
            candidate.Path,
            viewModel.Loc["MigrateInstance"],
            viewModel.Loc["AcceptSteamRisk"],
            viewModel.Loc["Cancel"],
            primaryDangerous: false);
        var result = await OverlayDialog.ShowCustomAsync<SteamDirectoryRiskDialogView,
            ThreeChoiceDialogViewModel, ThreeChoiceDialogResult>(dialog, OverlayHostId, CreateOverlayOptions());
        if (result == ThreeChoiceDialogResult.Secondary)
        {
            await viewModel.Instances.AcceptSteamGameDirectoryAsync(candidate);
            return;
        }
        if (result != ThreeChoiceDialogResult.Primary)
        {
            return;
        }
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = viewModel.Loc["SelectMigrationTarget"],
            AllowMultiple = false
        });
        var target = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(target))
        {
            await viewModel.Instances.MigrateSteamGameDirectoryAsync(candidate, target);
        }
    }

    private async void CloneInstanceWithName(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstanceItemViewModel instance })
        {
            return;
        }
        viewModel.SelectedInstance = instance;
        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["CloneInstance"],
            viewModel.Loc["CloneInstanceName"],
            $"{instance.Name} {viewModel.Loc["CopySuffix"]}",
            viewModel.Loc["CloneInstanceName"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var name = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        viewModel.CloneInstanceName = name;
        await viewModel.Instances.CloneSelectedInstanceCommand.ExecuteAsync(null);
        if (viewModel.SelectedInstance is { } selected
            && !string.Equals(selected.Id, instance.Id, StringComparison.Ordinal))
        {
            viewModel.CurrentPage = "Launch";
        }
    }

    private async void ConfirmDeleteInstance(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstanceItemViewModel instance })
        {
            return;
        }
        await ConfirmDeleteInstanceAsync(viewModel, instance);
    }

    private async void ConfirmDeleteSelectedInstance(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            await ConfirmDeleteInstanceAsync(viewModel, instance);
        }
    }

    private async Task ConfirmDeleteInstanceAsync(MainViewModel viewModel, InstanceItemViewModel instance)
    {
        if (viewModel.Instances.SelectedGameDirectory?.IsSteam == true)
        {
            var dialog = new ThreeChoiceDialogViewModel(
                viewModel.Loc["DeleteSteamInstanceTitle"],
                viewModel.Loc["DeleteSteamInstanceHint"],
                instance.RootPath,
                viewModel.Loc["PermanentlyDeleteDirectory"],
                viewModel.Loc["UnregisterOnly"],
                viewModel.Loc["Cancel"],
                primaryDangerous: true);
            var result = await OverlayDialog.ShowCustomAsync<SteamInstanceDeletionDialogView,
                ThreeChoiceDialogViewModel, ThreeChoiceDialogResult>(dialog, OverlayHostId, CreateOverlayOptions());
            if (result == ThreeChoiceDialogResult.Secondary)
            {
                await viewModel.Instances.UnregisterCurrentSteamDirectoryAsync();
            }
            else if (result == ThreeChoiceDialogResult.Primary)
            {
                await viewModel.Instances.DeleteInstanceCommand.ExecuteAsync(instance);
                if (!Directory.Exists(instance.RootPath))
                {
                    await viewModel.Instances.UnregisterCurrentSteamDirectoryAsync();
                }
            }
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["DeleteInstance"],
            viewModel.Loc["PermanentDeleteWarning"],
            instance.Name,
            viewModel,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.Instances.DeleteInstanceCommand.ExecuteAsync(instance);
        }
    }

    private async void RenameSelectedInstance(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            return;
        }
        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["RenameInstance"],
            viewModel.Loc["RenameInstanceHint"],
            instance.Name,
            viewModel.Loc["HistoricalInstanceName"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var name = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (!string.IsNullOrWhiteSpace(name))
        {
            await viewModel.Instances.RenameInstanceCommand.ExecuteAsync(name);
        }
    }

    private void OpenSelectedInstanceFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            OpenSafeAbsoluteFolder(instance.RootPath, instance.RootPath, create: false, viewModel);
        }
    }

    private void OpenSelectedSaveFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || viewModel.GetSelectedInstanceSaveDirectory() is not { } saveDirectory)
        {
            return;
        }
        OpenSafeAbsoluteFolder(
            Path.Combine(viewModel.VersionRoot, ".crystalfly"),
            saveDirectory,
            create: true,
            viewModel);
    }

    private async void ConfirmCompleteInstanceFiles(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["CompleteInstanceFiles"],
            viewModel.Loc["CompleteInstanceFilesHint"],
            instance.Name,
            viewModel,
            isDangerous: false);
        if (confirmed)
        {
            await viewModel.EnqueueSelectedInstanceRepairCommand.ExecuteAsync(null);
        }
    }
}
