using System.Globalization;
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
    private async void OnCheckForApplicationUpdatesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await CheckForApplicationUpdateAsync(viewModel, force: true);
        }
    }

    private async Task CheckForApplicationUpdateAsync(MainViewModel viewModel, bool force)
    {
        try
        {
            var result = await viewModel.CheckApplicationUpdateAsync(force);
            if (result.Manifest is not { } manifest || !IsVisible)
            {
                return;
            }

            var dialogViewModel = new ApplicationUpdateDialogViewModel(
                viewModel.Loc["ApplicationUpdateTitle"],
                string.Format(
                    System.Globalization.CultureInfo.CurrentUICulture,
                    viewModel.Loc["ApplicationUpdateVersionFormat"],
                    manifest.Version),
                manifest.NotesMarkdown,
                viewModel.Loc["UpdateNow"],
                viewModel.Loc["Later"],
                viewModel.Loc["SkipThisVersion"]);
            var choice = await OverlayDialog.ShowCustomAsync<
                ApplicationUpdateDialogView,
                ApplicationUpdateDialogViewModel,
                ApplicationUpdateDialogResult>(
                dialogViewModel,
                OverlayHostId,
                CreateOverlayOptions());
            switch (choice)
            {
                case ApplicationUpdateDialogResult.Update:
                    await StartApplicationUpdateAsync(
                        viewModel,
                        viewModel.StartAvailableApplicationUpdateAsync);
                    break;
                case ApplicationUpdateDialogResult.SkipVersion:
                    await viewModel.SkipApplicationUpdateAsync();
                    break;
                case ApplicationUpdateDialogResult.Later:
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            viewModel.ReportApplicationUpdateFailure(exception);
        }
    }

    private async void OnAccentColorClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: AccentColorOptionViewModel option }
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!option.IsCustom)
        {
            viewModel.Settings.SetAccentColor(option.Hex);
            return;
        }

        var originalColor = viewModel.Settings.AccentColor;
        var dialog = new AccentColorDialogViewModel(
            viewModel.Loc["AccentColorPickerTitle"],
            viewModel.Loc["AccentOriginal"],
            viewModel.Loc["AccentNew"],
            viewModel.Loc["AccentHex"],
            viewModel.Loc["AccentInvalid"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"],
            originalColor,
            viewModel.Settings.PreviewAccentColor);
        var selected = await OverlayDialog.ShowCustomAsync<
            AccentColorDialogView,
            AccentColorDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (selected is null)
        {
            viewModel.Settings.RestoreAccentColor();
            return;
        }

        viewModel.Settings.SetAccentColor(selected);
    }

    private void OnGlobalBackgroundScopeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Settings.SelectedBackgroundScope = viewModel.Settings.BackgroundScopeOptions.First(option =>
                option.Value == BackgroundEditScope.Global);
        }
    }

    private void OnInstanceBackgroundScopeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel { CanEditInstanceBackground: true } viewModel)
        {
            viewModel.Settings.SelectedBackgroundScope = viewModel.Settings.BackgroundScopeOptions.First(option =>
                option.Value == BackgroundEditScope.CurrentInstance);
        }
    }

    private async void OnSelectBackgroundImageClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["BackgroundSelect"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["BackgroundImage"])
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.Settings.SetBackgroundImageAsync(path);
        }
    }

    private async void OnRemoveBackgroundImageClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.Settings.RemoveBackgroundImageAsync();
        }
    }
}
