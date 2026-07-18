using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace Crystalfly.App.Views;

public partial class MainWindow : Window
{
    internal const string OverlayHostId = "Crystalfly.Main";

    private bool closeAfterDispose;
    private Task? disposeBeforeCloseTask;
    private Task<bool>? marketInstallDialogTask;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        Opened -= OnOpened;
        if (DataContext is MainViewModel viewModel)
        {
            try
            {
                await viewModel.InitializeAsync();
            }
            catch (Exception exception)
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!closeAfterDispose && DataContext is MainViewModel { IsBusy: true })
        {
            e.Cancel = true;
            base.OnClosing(e);
            return;
        }

        if (closeAfterDispose)
        {
            base.OnClosing(e);
            return;
        }

        foreach (var dialog in MainOverlayDialogHost.Children.OfType<CustomDialogControl>().ToArray())
        {
            dialog.Close();
        }
        e.Cancel = true;
        base.OnClosing(e);
        disposeBeforeCloseTask ??= DisposeBeforeCloseAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape
            && MainOverlayDialogHost.Children.OfType<CustomDialogControl>().LastOrDefault()?.DataContext
                is IDialogContext context)
        {
            e.Handled = true;
            context.Close();
            return;
        }

        base.OnKeyDown(e);
    }

    private async Task DisposeBeforeCloseAsync()
    {
        try
        {
            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.DisposeAsync();
            }
        }
        finally
        {
            closeAfterDispose = true;
            Close();
        }
    }

    private async void ConfirmUninstallLoader(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedInstance is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmLoaderUninstallTitle"],
            viewModel.Loc["ConfirmLoaderUninstallMessage"],
            viewModel.SelectedInstance.Name,
            viewModel,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.UninstallLoaderCommand.ExecuteAsync(null);
        }
    }

    private async void ConfirmUninstallMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedInstalledMod is null)
        {
            return;
        }
        var dependents = viewModel.GetSelectedModExternalDependentNames(bulk: false);
        var message = viewModel.Loc["ConfirmModUninstallMessage"];
        if (dependents.Count > 0)
        {
            message += $"{Environment.NewLine}{Environment.NewLine}{viewModel.Loc["CannotUninstallDependents"]} "
                + string.Join(", ", dependents);
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmModUninstallTitle"],
            message,
            viewModel.SelectedInstalledMod.Name,
            viewModel,
            canConfirm: dependents.Count == 0,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.UninstallSelectedModCommand.ExecuteAsync(null);
        }
    }

    private async void ConfirmBulkUninstallMods(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.HasSelectedMods)
        {
            return;
        }
        var dependents = viewModel.GetSelectedModExternalDependentNames(bulk: true);
        var message = viewModel.Loc["ConfirmBulkUninstallMessage"];
        if (dependents.Count > 0)
        {
            message += $"{Environment.NewLine}{Environment.NewLine}{viewModel.Loc["CannotUninstallDependents"]} "
                + string.Join(", ", dependents);
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmBulkUninstallTitle"],
            message,
            viewModel.GetSelectedModNames(bulk: true),
            viewModel,
            canConfirm: dependents.Count == 0,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.UninstallSelectedModsCommand.ExecuteAsync(null);
        }
    }

    private async void ConfirmRestoreSnapshot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedSnapshot is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmSnapshotRestoreTitle"],
            viewModel.Loc["ConfirmSnapshotRestoreMessage"],
            viewModel.SelectedSnapshot.Name,
            viewModel);
        if (confirmed)
        {
            await viewModel.RestoreSnapshotCommand.ExecuteAsync(null);
        }
    }

    private async void OpenMarketInstallDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedMarketMod is not { } mod)
        {
            return;
        }
        if (viewModel.PrepareMarketInstallTargetsCommand.IsRunning || marketInstallDialogTask is not null)
        {
            return;
        }

        try
        {
            await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!IsVisible || !ReferenceEquals(viewModel.SelectedMarketMod, mod))
        {
            return;
        }
        using var dialogViewModel = new MarketInstallDialogViewModel(viewModel, mod.DisplayName ?? mod.Name);
        try
        {
            marketInstallDialogTask = OverlayDialog.ShowCustomAsync<
                MarketInstallDialogView,
                MarketInstallDialogViewModel,
                bool>(
                dialogViewModel,
                OverlayHostId,
                CreateOverlayOptions());
            await marketInstallDialogTask;
        }
        finally
        {
            marketInstallDialogTask = null;
        }
    }

    internal async Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string target,
        MainViewModel viewModel,
        bool canConfirm = true,
        bool isDangerous = false)
    {
        var dialogViewModel = new ConfirmationDialogViewModel(
            title,
            message,
            target,
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"],
            canConfirm,
            isDangerous);
        return await OverlayDialog.ShowCustomAsync<
            ConfirmationDialogView,
            ConfirmationDialogViewModel,
            bool>(
            dialogViewModel,
            OverlayHostId,
            CreateOverlayOptions());
    }

    private static OverlayDialogOptions CreateOverlayOptions() => new()
    {
        CanLightDismiss = false,
        CanDragMove = false,
        IsCloseButtonVisible = true,
        CanResize = false
    };
}
