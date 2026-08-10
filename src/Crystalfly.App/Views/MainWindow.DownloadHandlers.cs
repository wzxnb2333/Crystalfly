using Avalonia.Controls;
using Avalonia.Interactivity;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Ursa.Controls;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
    private async void ShowHistoricalManifestDownloadDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new HistoricalManifestDialogViewModel(
            viewModel.DownloadBuilds,
            viewModel.Loc["HistoricalManifestTitle"],
            viewModel.Loc["HistoricalManifestHint"],
            viewModel.Loc["HistoricalManifestId"],
            viewModel.Loc["HistoricalInstanceName"],
            viewModel.Loc["HistoricalManifestKnown"],
            viewModel.Loc["HistoricalManifestUnverified"],
            viewModel.Loc["InvalidHistoricalManifest"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var request = await OverlayDialog.ShowCustomAsync<
            HistoricalManifestDialogView,
            HistoricalManifestDialogViewModel,
            HistoricalManifestDownloadRequest?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (request is null)
        {
            return;
        }

        try
        {
            await viewModel.EnqueueCustomSteamManifestAsync(request, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private async void OpenMarketInstallDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedMarketMod is not { } mod)
        {
            return;
        }
        if (viewModel.PrepareMarketInstallTargetsCommand.IsRunning
            || marketInstallDialogTask is not null
            || Interlocked.Exchange(ref marketInstallDialogOpening, 1) != 0)
        {
            return;
        }

        try
        {
            await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);
            if (!IsVisible || !ReferenceEquals(viewModel.SelectedMarketMod, mod))
            {
                return;
            }
            using var dialogViewModel = new MarketInstallDialogViewModel(
                viewModel,
                mod.DisplayName ?? mod.Name);
            await dialogViewModel.LoadPlanAsync();
            marketInstallDialogTask = OverlayDialog.ShowCustomAsync<
                MarketInstallDialogView,
                MarketInstallDialogViewModel,
                bool>(
                dialogViewModel,
                OverlayHostId,
                CreateOverlayOptions());
            await marketInstallDialogTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            marketInstallDialogTask = null;
            Interlocked.Exchange(ref marketInstallDialogOpening, 0);
        }
    }
}
