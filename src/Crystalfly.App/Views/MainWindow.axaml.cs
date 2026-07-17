using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Crystalfly.App.ViewModels;

namespace Crystalfly.App.Views;

public partial class MainWindow : Window
{
    private bool closeAfterDispose;
    private Task? disposeBeforeCloseTask;

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
        if (closeAfterDispose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        disposeBeforeCloseTask ??= DisposeBeforeCloseAsync();
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

    private async void BrowseVersionRoot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = viewModel.Loc["SelectVersionRootTitle"],
            AllowMultiple = false
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            viewModel.VersionRoot = path;
            await viewModel.ApplyVersionRootCommand.ExecuteAsync(null);
        }
    }

    private async void BrowseLocalMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["SelectModPackageTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["ModFileType"])
                {
                    Patterns = ["*.zip", "*.dll"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            viewModel.LocalModPath = path;
        }
    }

    private async void BrowseLocalLoaderManifest(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["SelectLoaderManifestTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["LoaderManifestFileType"])
                {
                    Patterns = ["*.json"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            viewModel.LocalLoaderManifestPath = path;
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

    private async Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string target,
        MainViewModel viewModel,
        bool canConfirm = true,
        bool isDangerous = false)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = Icon
        };
        dialog.Classes.Add("cfp-window");

        var cancelButton = new Button
        {
            Content = viewModel.Loc["Cancel"],
            MinWidth = 96
        };
        cancelButton.Classes.Add("cfp-secondary");

        var confirmButton = new Button
        {
            Content = viewModel.Loc["Confirm"],
            MinWidth = 96,
            IsEnabled = canConfirm
        };
        ApplyConfirmationStyle(confirmButton, isDangerous);

        cancelButton.Click += (_, _) => dialog.Close(false);
        confirmButton.Click += (_, _) => dialog.Close(true);
        dialog.Opened += (_, _) => cancelButton.Focus(NavigationMethod.Tab, KeyModifiers.None);
        dialog.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                eventArgs.Handled = true;
                dialog.Close(false);
            }
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 20,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = target,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Opacity = 0.7
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, confirmButton }
                }
            }
        };

        return await dialog.ShowDialog<bool>(this);
    }

    internal static void ApplyConfirmationStyle(Button button, bool isDangerous)
        => button.Classes.Add(isDangerous ? "cfp-danger" : "cfp-primary");
}
