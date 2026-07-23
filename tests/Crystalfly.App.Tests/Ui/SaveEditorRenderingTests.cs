using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class SaveEditorRenderingTests
{
    [AvaloniaFact]
    public async Task Config_tab_exposes_accessibility_and_save_controls()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-config-ui", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(root, "AppConfig.ini");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            configPath,
            "[Accessibility]\nReducedCameraShake=0.25\nReducedControllerRumble=0.5");
        var config = new GameConfigViewModel(configPath);
        await config.LoadAsync();
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            CurrentPage = "Manage",
            CurrentManageTab = "Config",
            GameConfig = config
        };
        var window = new MainWindow
        {
            Width = 1280,
            Height = 720,
            DataContext = viewModel
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var panel = Assert.IsType<StackPanel>(
                window.FindControl<Control>("GameConfigPanel"));
            Assert.True(panel.IsEffectivelyVisible);
            Assert.Equal(2, panel.GetVisualDescendants().OfType<Slider>().Count());
            Assert.NotNull(window.FindControl<Button>("ConfigSaveButton"));
            Assert.NotNull(window.FindControl<Button>("ConfigResetButton"));
        }
        finally
        {
            window.Close();
            await viewModel.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Knight_loading_indicators_are_larger_and_centered_in_their_hosts()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-knight-loader", Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(root)
        {
            CurrentPage = "Manage",
            CurrentManageTab = "Snapshots",
            SaveEditor = new SaveEditorViewModel(
                new NamedSnapshotService(root, $"Crystalfly.KnightLoaderTests.{Guid.NewGuid():N}"),
                "instance",
                null,
                "Loading save")
        };
        var window = new MainWindow
        {
            Width = 1280,
            Height = 720,
            DataContext = viewModel
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var saveHost = Assert.IsType<LoadingContainer>(
                window.FindControl<Control>("SaveEditorLoadingHost"));
            var saveViewport = Assert.IsType<Border>(
                window.FindControl<Control>("SaveEditorKnightLoadingViewport"));

            Assert.Equal(45, saveViewport.Bounds.Width);
            Assert.Equal(72, saveViewport.Bounds.Height);
            AssertCentered(saveViewport, saveHost);

            viewModel.IsBusy = true;
            Dispatcher.UIThread.RunJobs();

            var globalHost = Assert.IsType<LoadingContainer>(
                window.FindControl<Control>("GlobalLoadingHost"));
            var globalViewport = Assert.IsType<Border>(
                window.FindControl<Control>("GlobalKnightLoadingViewport"));

            Assert.Equal(45, globalViewport.Bounds.Width);
            Assert.Equal(72, globalViewport.Bounds.Height);
            AssertCentered(globalViewport, globalHost);
        }
        finally
        {
            viewModel.IsBusy = false;
            window.Close();
            await viewModel.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Large_save_realizes_only_visible_editor_rows()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-save-editor", Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(root)
        {
            CurrentPage = "Manage",
            CurrentManageTab = "Snapshots",
            SaveEditor = new SaveEditorViewModel(
                new NamedSnapshotService(root, $"Crystalfly.SaveEditorTests.{Guid.NewGuid():N}"),
                "instance",
                null,
                "Large save")
            {
                Entries = Enumerable.Range(0, 12_360)
                    .Select(index => new SaveEntryViewModel(new SaveEntry(
                        $"playerData.entry{index}",
                        index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        SaveEntry.KindNumber)))
                    .ToArray(),
                IsLoaded = true
            }
        };
        var window = new MainWindow
        {
            Width = 1280,
            Height = 720,
            DataContext = viewModel
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var list = window.GetVisualDescendants().OfType<ListBox>()
                .Single(control => control.Classes.Contains("cfp-save-entry-list"));
            var realizedEditors = list.GetVisualDescendants().OfType<TextBox>().Count();

            Assert.InRange(realizedEditors, 1, 100);
        }
        finally
        {
            window.Close();
            await viewModel.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void AssertCentered(Control indicator, Control host)
    {
        var center = indicator.TranslatePoint(
            new Point(indicator.Bounds.Width / 2, indicator.Bounds.Height / 2),
            host);

        Assert.NotNull(center);
        Assert.InRange(Math.Abs(center.Value.X - host.Bounds.Width / 2), 0, 0.5);
        Assert.InRange(Math.Abs(center.Value.Y - host.Bounds.Height / 2), 0, 0.5);
    }
}
