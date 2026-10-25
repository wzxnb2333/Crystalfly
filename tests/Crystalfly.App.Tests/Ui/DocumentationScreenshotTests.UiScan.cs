using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
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
