using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Ursa.Controls;
using Crystalfly.App.Views.Dialogs;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
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
                ? $"{template.Name} Speedrun"
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
