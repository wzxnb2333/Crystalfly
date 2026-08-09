using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed class InstalledModSelectionInteractionTests
{
    [AvaloniaFact]
    public async Task Installed_mod_row_press_selects_from_any_non_interactive_area()
    {
        var (window, viewModel) = CreateWindowWithMods("mod-0", "mod-1", "mod-2");

        try
        {
            foreach (var (id, position) in new[]
                     {
                         ("mod-0", RowPosition.NameText),
                         ("mod-0", RowPosition.VersionText),
                         ("mod-0", RowPosition.RowGap),
                         ("mod-0", RowPosition.RowGapWhenSelected)
                     })
            {
                ResetSelection(viewModel);
                PressRow(window, viewModel, id, position, RawInputModifiers.None);

                var item = viewModel.ModManagement.InstalledMods.Single(mod => mod.Id == id);
                Assert.True(item.IsSelected, $"{position} on {id} should select the row.");
                Assert.Same(item, viewModel.ModManagement.SelectedInstalledMod);
            }
        }
        finally
        {
            CloseImmediately(window);
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Installed_mod_action_buttons_do_not_select_the_row()
    {
        var (window, viewModel) = CreateWindowWithMods("mod-0");

        try
        {
            var item = viewModel.ModManagement.InstalledMods[0];
            var (row, pinButton) = FindRowAndPinButton(window, "mod-0", viewModel);
            var clicked = false;
            pinButton.Click += (_, _) => clicked = true;

            // 悬停在行内可命中区域激活动作区,再渲染若干帧让按钮进入命中树。
            var name = row.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Classes.Contains("cfp-installed-mod-name"));
            var nameCenter = CenterInWindow(name, window);
            window.MouseMove(nameCenter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            RenderFrames(40);
            var point = CenterInWindow(pinButton, window);
            window.MouseMove(point, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            RenderFrames(40);

            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            for (var attempt = 0; attempt < 100 && !clicked; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.True(clicked, "The action button click itself must have been handled.");
            Assert.False(item.IsSelected, "Action buttons must not select the row.");
            // 按钮动作(如固定)会自行设置 SelectedInstalledMod,但绝不应触发行选择。
            Assert.Same(item, viewModel.ModManagement.SelectedInstalledMod);
        }
        finally
        {
            CloseImmediately(window);
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Installed_mod_shift_click_selects_a_range_from_any_press_target()
    {
        var (window, viewModel) = CreateWindowWithMods("mod-0", "mod-1", "mod-2");

        try
        {
            PressRow(window, viewModel, "mod-0", RowPosition.NameText, RawInputModifiers.None);
            PressRow(window, viewModel, "mod-2", RowPosition.RowGap, RawInputModifiers.Shift);

            Assert.All(viewModel.ModManagement.InstalledMods, mod => Assert.True(mod.IsSelected));
            Assert.Same(viewModel.ModManagement.InstalledMods[2], viewModel.ModManagement.SelectedInstalledMod);
        }
        finally
        {
            CloseImmediately(window);
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Installed_mod_control_click_toggles_without_losing_the_selection()
    {
        var (window, viewModel) = CreateWindowWithMods("mod-0", "mod-1", "mod-2");

        try
        {
            PressRow(window, viewModel, "mod-0", RowPosition.RowGap, RawInputModifiers.None);
            PressRow(window, viewModel, "mod-2", RowPosition.VersionText, RawInputModifiers.Control);

            Assert.True(viewModel.ModManagement.InstalledMods[0].IsSelected);
            Assert.False(viewModel.ModManagement.InstalledMods[1].IsSelected);
            Assert.True(viewModel.ModManagement.InstalledMods[2].IsSelected);

            // 重新建立锚点后,shift 应扩展到整个范围。
            PressRow(window, viewModel, "mod-0", RowPosition.NameText, RawInputModifiers.None);
            PressRow(window, viewModel, "mod-2", RowPosition.RowGap, RawInputModifiers.Shift);
            Assert.All(viewModel.ModManagement.InstalledMods, mod => Assert.True(mod.IsSelected));
        }
        finally
        {
            CloseImmediately(window);
            await viewModel.DisposeAsync();
        }
    }

    private enum RowPosition
    {
        NameText,
        VersionText,
        RowGap,
        RowGapWhenSelected
    }

    private static (MainWindow Window, MainViewModel ViewModel) CreateWindowWithMods(params string[] ids)
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = "Manage",
            CurrentManageTab = "Mods"
        };
        foreach (var id in ids)
        {
            var item = new InstalledModItemViewModel(
                new InstalledModReceipt
                {
                    Id = id,
                    Name = $"Mod {id}",
                    Version = "1.0.0",
                    LoaderId = "modding-api",
                    InstallRoot = $"Mods/{id}",
                    Enabled = true
                },
                null,
                static () => { });
            viewModel.ModManagement.InstalledMods.Add(item);
            viewModel.ModManagement.VisibleInstalledMods.Add(item);
        }
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        return (window, viewModel);
    }

    private static void PressRow(
        MainWindow window,
        MainViewModel viewModel,
        string id,
        RowPosition position,
        RawInputModifiers modifiers)
    {
        var row = FindRow(window, id);
        var point = ResolvePressPoint(row, position, window);
        window.MouseMove(point, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static Point ResolvePressPoint(Grid row, RowPosition position, MainWindow window)
    {
        var origin = row.TranslatePoint(default, window);
        Assert.NotNull(origin);
        var center = new Point(origin.Value.X + row.Bounds.Width / 2, origin.Value.Y + row.Bounds.Height / 2);
        return position switch
        {
            RowPosition.NameText => CenterInWindow(
                row.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("cfp-installed-mod-name")),
                window),
            RowPosition.VersionText => CenterInWindow(
                row.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("cfp-meta") && text.Text == "1.0.0"),
                window),
            RowPosition.RowGap or RowPosition.RowGapWhenSelected =>
                new Point(origin.Value.X + 170, center.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
    }

    private static Grid FindRow(MainWindow window, string id) =>
        window.GetVisualDescendants().OfType<Grid>()
            .Single(grid => grid.Classes.Contains("cfp-installed-mod-row")
                && grid.IsEffectivelyVisible
                && grid.DataContext is InstalledModItemViewModel item
                && item.Id == id);

    private static (Grid Row, Button PinButton) FindRowAndPinButton(
        MainWindow window,
        string id,
        MainViewModel viewModel)
    {
        var row = FindRow(window, id);
        var pin = row.GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["PinMod"]);
        return (row, pin);
    }

    private static Point CenterInWindow(Control control, Window window)
    {
        var origin = control.TranslatePoint(default, window);
        Assert.NotNull(origin);
        return origin.Value + new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
    }

    private static void RenderFrames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void ResetSelection(MainViewModel viewModel)
    {
        foreach (var item in viewModel.ModManagement.InstalledMods)
        {
            item.IsSelected = false;
        }
        viewModel.ModManagement.SelectedInstalledMod = null;
        typeof(ModManagementViewModel)
            .GetField("installedModSelectionAnchorId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel.ModManagement, null);
    }

    private static void CloseImmediately(MainWindow window)
    {
        typeof(MainWindow)
            .GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, true);
        window.Close();
    }
}
