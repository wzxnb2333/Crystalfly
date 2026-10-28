using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaFact]
    public async Task Speedrun_board_entries_select_all_and_individual_boards_at_minimum_width()
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.SpeedrunActivity);
        fixture.ViewModel.Loc.Apply(UiLanguage.English);
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = fixture.ViewModel;
        fixture.ViewModel.Settings.SelectedMotionPreference = new(UiMotionPreference.Off, fixture.ViewModel.Loc["MotionOff"]);
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();

        var filters = fixture.Window.FindControl<Grid>("SpeedrunBoardFilters")!;
        var buttons = filters.Children.OfType<Button>().ToArray();
        Assert.Equal(new[] { "All", "HollowKnight", "HollowKnightExtensions", "Silksong", "SilksongExtensions" },
            buttons.Select(button => Assert.IsType<string>(button.CommandParameter)));
        Assert.Same(buttons[0], Assert.Single(buttons, candidate => candidate.Classes.Contains("active")));
        foreach (var button in buttons)
        {
            Assert.True(button.IsEffectivelyVisible);
            var origin = Assert.IsType<Point>(button.TranslatePoint(default, fixture.Window));
            Assert.InRange(origin.X, 0, fixture.Window.ClientSize.Width - button.Bounds.Width);
            Assert.Equal(buttons[0].Bounds.Width, button.Bounds.Width, 1);
            var center = origin + new Point(button.Bounds.Width / 2, button.Bounds.Height / 2);
            fixture.Window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            fixture.Window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(button.CommandParameter, fixture.ViewModel.SpeedrunActivityFilter);
            Assert.Same(button, Assert.Single(buttons, candidate => candidate.Classes.Contains("active")));
            foreach (var text in button.GetVisualDescendants().OfType<TextBlock>())
                Assert.True(text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 0.5, text.Text);
        }

        fixture.ViewModel.IsSpeedrunActivityLoading = true;
        fixture.ViewModel.SpeedrunActivityStatus = fixture.ViewModel.Loc["SpeedrunActivityLoading"];
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        var workspace = fixture.Window.FindControl<Grid>("SpeedrunActivityWorkspace")!;
        var list = fixture.Window.FindControl<ScrollViewer>("SpeedrunActivityList")!;
        var footer = fixture.Window.FindControl<Grid>("SpeedrunActivityFooter")!;
        var footerBottom = footer.TranslatePoint(new Point(0, footer.Bounds.Height), workspace)!.Value.Y;
        Assert.InRange(workspace.Bounds.Height - footerBottom, 0, 16);
        Assert.True(footer.TranslatePoint(default, fixture.Window)!.Value.Y > fixture.Window.ClientSize.Height - 150);
        var originalListHeight = list.Bounds.Height;
        fixture.Window.Width = 1100;
        fixture.Window.Height = 720;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        Assert.Equal(120, list.Bounds.Height - originalListHeight, 1);
        var footerOrigin = footer.TranslatePoint(default, fixture.Window)!.Value;
        var tabSwitch = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Border>(),
            border => border.Classes.Contains("cfp-speedrun-tab-switch"));
        Assert.True(footerOrigin.Y >= tabSwitch.TranslatePoint(default, fixture.Window)!.Value.Y);
        var board = new SpeedrunBoardDescriptor(SpeedrunGame.SilksongExtensions, "layout", "Layout", null, null, []);
        for (var index = 0; index < 40; index++)
        {
            var run = new SpeedrunPodiumEntry($"layout-{index}", 1, "Layout test runner", "PT1M", 60, null, null);
            fixture.ViewModel.VisibleSpeedrunActivities.Add(new(new(run.RunId, SpeedrunActivityKind.WorldRecord,
                board, run, DateTimeOffset.UtcNow), "World record", board.DisplayName));
        }
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        Assert.True(list.Extent.Height > list.Viewport.Height);
        list.Offset = new Vector(0, list.Extent.Height);
        fixture.Window.UpdateLayout();
        Assert.True(list.Offset.Y > 0);
        Assert.Equal(footerOrigin, footer.TranslatePoint(default, fixture.Window)!.Value);
        fixture.ViewModel.VisibleSpeedrunActivities.Clear();
        fixture.ViewModel.SelectSpeedrunActivityFilterCommand.Execute("All");
        fixture.Window.UpdateLayout();
        fixture.Window.MouseMove(new Point(1, 1), RawInputModifiers.None);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            using var frame = fixture.Window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(output, "SpeedrunFourBoards.png"), new PngBitmapEncoderOptions());
            fixture.ViewModel.Loc.Apply(UiLanguage.SimplifiedChinese);
            fixture.ViewModel.SpeedrunActivityStatus = fixture.ViewModel.Loc["SpeedrunActivityLoading"];
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            using var chineseFrame = fixture.Window.GetLastRenderedFrame();
            Assert.NotNull(chineseFrame);
            chineseFrame.Save(Path.Combine(output, "SpeedrunFourBoards.zh-CN.png"), new PngBitmapEncoderOptions());
        }
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.English)]
    public async Task Save_actions_are_visible_without_scrolling_at_minimum_size(UiLanguage language)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.SaveEditor);
        fixture.ViewModel.Loc.Apply(language);
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = fixture.ViewModel;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        foreach (var command in new[] { fixture.ViewModel.SaveEditor!.SaveCommand, fixture.ViewModel.SaveEditor.ResetCommand })
        {
            var button = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, command));
            var origin = Assert.IsType<Point>(button.TranslatePoint(default, fixture.Window));
            Assert.InRange(origin.X, 0, fixture.Window.ClientSize.Width - button.Bounds.Width);
            Assert.InRange(origin.Y, 0, fixture.Window.ClientSize.Height - button.Bounds.Height);
        }
    }

    [AvaloniaTheory]
    [Trait("Category", "VisualScan")]
    [InlineData(1100, 720, UiLanguage.SimplifiedChinese)]
    [InlineData(900, 600, UiLanguage.English)]
    public async Task Workspace_pages_render_for_ui_scan(int width, int height, UiLanguage language)
    {
        foreach (var state in new[]
        {
            ScreenshotState.Launch, ScreenshotState.InstanceSelection, ScreenshotState.InstanceDetail,
            ScreenshotState.InstalledModHealth, ScreenshotState.ModPresets, ScreenshotState.SaveEditor,
            ScreenshotState.MarketList, ScreenshotState.MarketDetail, ScreenshotState.Speedrun,
            ScreenshotState.SpeedrunActivity, ScreenshotState.GameVersions, ScreenshotState.Settings
        })
        {
            await using var fixture = CreateFixture();
            await fixture.PrepareAsync(state);
            fixture.ViewModel.Loc.Apply(language);
            fixture.Window.Width = width;
            fixture.Window.Height = height;
            fixture.Window.Show();
            fixture.Window.DataContext = fixture.ViewModel;
            Capture(state.ToString());
            if (state == ScreenshotState.Settings)
            {
                fixture.ViewModel.CurrentSettingsSection = "Network";
                Capture("SettingsNetwork");
            }
            if (state == ScreenshotState.GameVersions)
            {
                fixture.ViewModel.CurrentDownloadSection = "DownloadQueue";
                Capture("DownloadQueueEmpty");
                var commands = new[] { fixture.ViewModel.DownloadCenter.RetryAllCommand,
                    fixture.ViewModel.DownloadCenter.PauseAllCommand, fixture.ViewModel.DownloadCenter.ResumeAllCommand,
                    fixture.ViewModel.DownloadCenter.ClearCompletedCommand };
                var actions = fixture.Window.GetVisualDescendants().OfType<Button>()
                    .Where(button => commands.Contains(button.Command)).ToArray();
                Assert.Equal(4, actions.Length);
                Assert.All(actions, button => Assert.False(button.IsEffectivelyVisible));
                fixture.ViewModel.DownloadCenter.DownloadQueueGroups.Add(new(new DownloadQueueGroup
                {
                    Id = "ui-scan", Name = "DebugMod", TargetInstanceName = fixture.Instance.Name,
                    State = DownloadQueueGroupState.Completed
                }, fixture.ViewModel.Loc));
                Capture("DownloadQueue");
                Assert.All(actions, button =>
                {
                    Assert.True(button.IsEffectivelyVisible);
                    Assert.Equal(AutomationProperties.GetName(button), ToolTip.GetTip(button));
                });
            }

            void Capture(string name)
            {
                Dispatcher.UIThread.RunJobs();
                fixture.Window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                using var frame = fixture.Window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is { Length: > 0 } output)
                {
                    if (width == 900) output = Path.Combine(output, "900x600-English");
                    Directory.CreateDirectory(output);
                    frame.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
                }
            }
        }
    }
}
