using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Configuration;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class GameDirectoryRemovalConfirmationTests
{
    [AvaloniaFact]
    public async Task Remove_game_directory_shows_dangerous_confirmation_before_removal()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var viewModel = new MainViewModel(root);
        var directory = new GameDirectoryItemViewModel(new GameDirectoryRegistration
        {
            Path = Path.Combine(root, "game"),
            DisplayName = "Test versions",
            Source = GameDirectorySourceKind.Managed
        });
        viewModel.Instances.GameDirectories.Add(directory);
        viewModel.Instances.SelectedGameDirectory = directory;
        viewModel.CurrentPage = "Versions";
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        try
        {
            Dispatcher.UIThread.RunJobs();
            var remove = Assert.Single(window.GetVisualDescendants().OfType<Button>(), button =>
                button.IsEffectivelyVisible && AutomationProperties.GetName(button) == viewModel.Loc["RemoveGameDirectory"]);
            remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ConfirmationDialogView? dialog = null;
            for (var attempt = 0; attempt < 50 && dialog is null; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialog = window.GetVisualDescendants().OfType<ConfirmationDialogView>().SingleOrDefault();
            }
            Assert.NotNull(dialog);
            var dialogViewModel = Assert.IsType<ConfirmationDialogViewModel>(dialog.DataContext);
            Assert.True(dialogViewModel.IsDangerous);
            Assert.Equal(directory.Path, dialogViewModel.Target);
            Assert.Contains(directory, viewModel.Instances.GameDirectories);
            Assert.Same(directory, viewModel.Instances.SelectedGameDirectory);

            var confirm = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button =>
                AutomationProperties.GetName(button) == viewModel.Loc["Confirm"]);
            // ConfirmCommand 驱动按钮：直接触发路由 Click 事件不会执行命令。
            confirm.Command!.Execute(null);
            for (var attempt = 0; attempt < 100 && viewModel.Instances.GameDirectories.Count > 0; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }
            Assert.Empty(viewModel.Instances.GameDirectories);
            Assert.True(viewModel.ErrorMessage is null, viewModel.ErrorMessage);
        }
        finally
        {
            Close(window);
            await viewModel.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void Close(MainWindow window)
    {
        foreach (var dialog in window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray()) dialog.Close();
        typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
        window.Close();
    }
}
