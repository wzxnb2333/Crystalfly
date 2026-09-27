using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Ursa.Controls;
using Crystalfly.App.Views.Dialogs;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
    private async void AddSpeedrunCommunityLink(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await ShowCommunityLinkDialogAsync(viewModel);
        }
    }

    private async void EditSpeedrunCommunityLink(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Button { DataContext: SpeedrunCommunityLinkItemViewModel { IsCustom: true } item })
        {
            await ShowCommunityLinkDialogAsync(viewModel, item);
        }
    }

    private async Task ShowCommunityLinkDialogAsync(MainViewModel viewModel, SpeedrunCommunityLinkItemViewModel? item = null)
    {
        var dialog = new CommunityLinkDialogViewModel(viewModel.Loc,
            item is null ? viewModel.SpeedrunCommunityLinks.AddAsync : viewModel.SpeedrunCommunityLinks.UpdateAsync,
            item?.Definition);
        var options = CreateOverlayOptions();
        options.IsCloseButtonVisible = false;
        await OverlayDialog.ShowCustomAsync<CommunityLinkDialogView, CommunityLinkDialogViewModel, bool>(
            dialog, OverlayHostId, options);
    }

    private async void RemoveSpeedrunCommunityLink(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Button { DataContext: SpeedrunCommunityLinkItemViewModel { IsCustom: true } item })
        {
            return;
        }
        if (!await ShowConfirmationAsync(viewModel.Loc["SpeedrunCommunityRemove"],
                viewModel.Loc["SpeedrunCommunityRemoveHint"], item.Name, viewModel, isDangerous: true))
        {
            return;
        }
        try
        {
            await viewModel.SpeedrunCommunityLinks.RemoveAsync(item.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private async void ShowCreateSpeedrunEnvironmentDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedSpeedrunTemplate: { } template } viewModel)
        {
            return;
        }
        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["SpeedrunCreate"],
            viewModel.Loc["SpeedrunHint"],
            string.IsNullOrWhiteSpace(viewModel.SpeedrunEnvironmentName)
                ? string.Format(
                    System.Globalization.CultureInfo.CurrentUICulture,
                    viewModel.Loc["SpeedrunDefaultNameFormat"],
                    template.Name)
                : viewModel.SpeedrunEnvironmentName,
            viewModel.Loc["SpeedrunEnvironmentName"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        string? name = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        viewModel.SpeedrunEnvironmentName = name;
        await viewModel.CreateSpeedrunEnvironmentCommand.ExecuteAsync(null);
    }

    private async void RenameSelectedSpeedrunEnvironment(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel { SelectedSpeedrunInstance: { } instance } viewModel)
        {
            await ShowRenameInstanceDialogAsync(viewModel, instance);
        }
    }

    private void OpenSpeedrunReport(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SpeedrunReportPath: { } reportPath } viewModel
            || !File.Exists(reportPath))
        {
            if (DataContext is MainViewModel missingViewModel)
            {
                missingViewModel.ErrorMessage = missingViewModel.Loc["SpeedrunReportPathMissing"];
            }
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private async void AddLiveSplitFavorite(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["LiveSplitFavoriteAdd"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["LiveSplitFiles"])
                {
                    Patterns = ["*.lss"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!viewModel.TryAddLiveSplitFavorite(path))
        {
            viewModel.ErrorMessage = viewModel.Loc["LiveSplitFavoriteInvalid"];
        }
    }

    private void OpenLiveSplitFavorite(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string path }
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!File.Exists(path))
        {
            viewModel.ErrorMessage = viewModel.Loc["LiveSplitFavoriteMissing"];
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private void RemoveLiveSplitFavorite(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string path } && DataContext is MainViewModel viewModel)
        {
            viewModel.RemoveLiveSplitFavorite(path);
        }
    }

    private async void CopySpeedrunReportPath(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SpeedrunReportPath: { } reportPath } viewModel
            || Clipboard is null
            || !File.Exists(reportPath))
        {
            if (DataContext is MainViewModel missingViewModel)
            {
                missingViewModel.ErrorMessage = missingViewModel.Loc["SpeedrunReportPathMissing"];
            }
            return;
        }

        try
        {
            await Clipboard.SetTextAsync(reportPath);
            ShowToast(viewModel, viewModel.Loc["SpeedrunReportPathCopied"], NotificationType.Success);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private void OpenSpeedrunRun(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string value }
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(string.Equals(uri.Host, "speedrun.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "www.speedrun.com", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
            }
        }
    }

    private void OnSpeedrunWorkspacePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not ScrollViewer host
            || eventArgs.GetCurrentPoint(host).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed
            || !CanBeginSpeedrunSwipe(eventArgs))
        {
            return;
        }

        speedrunSwipeStart = eventArgs.GetPosition(host);
        eventArgs.Pointer.Capture(host);
    }

    private void OnSpeedrunWorkspacePointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (sender is not ScrollViewer host || speedrunSwipeStart is not { } start)
        {
            return;
        }

        speedrunSwipeStart = null;
        eventArgs.Pointer.Capture(null);
        Point end = eventArgs.GetPosition(host);
        double horizontal = end.X - start.X;
        double vertical = end.Y - start.Y;
        if (Math.Abs(horizontal) < 64
            || Math.Abs(horizontal) <= Math.Abs(vertical) * 1.25
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SelectSpeedrunTabCommand.Execute(horizontal < 0 ? "Activity" : "Environment");
        eventArgs.Handled = true;
    }

    private static bool CanBeginSpeedrunSwipe(PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.Source is not Avalonia.Visual visual)
        {
            return true;
        }

        return visual.FindAncestorOfType<Button>() is null
            && visual.FindAncestorOfType<ComboBox>() is null
            && visual.FindAncestorOfType<TextBox>() is null
            && visual.FindAncestorOfType<ScrollBar>() is null;
    }
}
