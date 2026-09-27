using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class SaveEditorRenderingTests
{
    [AvaloniaFact]
    public async Task Save_fields_show_chinese_labels_and_switch_language_without_reloading_values()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-save-localization-ui", Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(root)
        {
            CurrentPage = "Manage",
            CurrentManageTab = "Snapshots"
        };
        viewModel.Settings.SelectedLanguage = new(UiLanguage.SimplifiedChinese, "简体中文");
        var geo = new SaveEntryViewModel(new SaveEntry("playerData.geo", "1250", SaveEntry.KindNumber));
        var dash = new SaveEntryViewModel(new SaveEntry("playerData.hasDash", "true", SaveEntry.KindBoolean));
        var editor = new SaveEditorViewModel(
            new NamedSnapshotService(root, $"Crystalfly.SaveLocalizationUi.{Guid.NewGuid():N}"),
            "instance", null, "存档", viewModel.Loc)
        {
            Entries = [geo, dash],
            IsLoaded = true
        };
        editor.Slots.Add("user1.dat");
        viewModel.SaveEditor = editor;
        var window = new MainWindow { Width = 900, Height = 600, DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = window.GetVisualDescendants().OfType<ListBox>()
                .Single(control => control.Classes.Contains("cfp-save-entry-list"));
            Assert.Contains(list.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "吉欧");
            Assert.Contains(list.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "playerData.geo");
            var boolean = Assert.Single(list.GetVisualDescendants().OfType<CheckBox>(), control => control.IsVisible);
            Assert.Equal("蛾翼披风（冲刺）", AutomationProperties.GetName(boolean));
            Assert.Equal("是", boolean.Content);
            boolean.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("false", dash.Value);
            Assert.Equal("否", boolean.Content);

            viewModel.Settings.SelectedLanguage = new(UiLanguage.English, "English");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("playerData.hasDash", AutomationProperties.GetName(boolean));
            Assert.Equal("No", boolean.Content);
            Assert.Equal("playerData.geo", geo.DisplayName);
            Assert.Equal("1250", geo.Value);

            viewModel.Settings.SelectedLanguage = new(UiLanguage.SimplifiedChinese, "简体中文");
            Dispatcher.UIThread.RunJobs();
            var search = Assert.IsType<TextBox>(window.FindControl<TextBox>("SaveFieldSearchBox"));
            search.Text = "吉欧";
            Dispatcher.UIThread.RunJobs();
            Assert.Same(geo, Assert.Single(editor.VisibleEntries));
            Assert.Equal(2, editor.Entries.Count);
            Assert.Equal("false", dash.ToEntry().Value);
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
