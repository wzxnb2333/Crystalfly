using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaFact]
    public async Task Visual_acceptance_checks_supported_layouts_and_writes_screenshots_only_when_requested()
    {
        var writeScreenshots = string.Equals(
            Environment.GetEnvironmentVariable("CRYSTALFLY_UPDATE_VISUAL_ACCEPTANCE"),
            "1",
            StringComparison.Ordinal);
        var output = Path.Combine(FindRepositoryRoot(), "artifacts", "visual-acceptance");
        if (writeScreenshots)
        {
            Directory.CreateDirectory(output);
        }
        var captures = new[]
        {
            ("speedrun-900-zh-dark", ScreenshotState.Speedrun, 900, 600, 1d, false, false, false),
            ("speedrun-900-en-light", ScreenshotState.Speedrun, 900, 600, 1d, true, true, false),
            ("speedrun-1280-zh-light", ScreenshotState.Speedrun, 1280, 720, 1d, false, true, false),
            ("speedrun-1920-zh-dark-150dpi", ScreenshotState.Speedrun, 1920, 1080, 1.5d, false, false, false),
            ("activity-empty-900-zh-dark", ScreenshotState.SpeedrunActivity, 900, 600, 1d, false, false, false),
            ("activity-900-zh-dark", ScreenshotState.SpeedrunActivity, 900, 600, 1d, false, false, true),
            ("activity-900-en-light", ScreenshotState.SpeedrunActivity, 900, 600, 1d, true, true, true),
            ("activity-1280-zh-light", ScreenshotState.SpeedrunActivity, 1280, 720, 1d, false, true, true),
            ("activity-1920-zh-dark-150dpi", ScreenshotState.SpeedrunActivity, 1920, 1080, 1.5d, false, false, true),
            ("launch-900-zh-dark", ScreenshotState.Launch, 900, 600, 1d, false, false, false),
            ("launch-1280-zh-light", ScreenshotState.Launch, 1280, 720, 1d, false, true, false),
            ("downloads-900-zh-light", ScreenshotState.GameVersions, 900, 600, 1d, false, true, false),
            ("settings-900-en-light", ScreenshotState.Settings, 900, 600, 1d, true, true, false),
            ("market-900-zh-dark", ScreenshotState.MarketDetail, 900, 600, 1d, false, false, false),
            ("instance-900-zh-light", ScreenshotState.InstanceDetail, 900, 600, 1d, false, true, false),
            ("save-editor-900-zh-dark", ScreenshotState.SaveEditor, 900, 600, 1d, false, false, false),
        };
        foreach (var (name, state, width, height, scaling, english, light, populated) in captures)
        {
            await using var fixture = CreateFixture();
            fixture.ViewModel.Loc.Apply(english ? UiLanguage.English : UiLanguage.SimplifiedChinese);
            InvokeRebuildSettingOptions(fixture.ViewModel);
            fixture.ViewModel.SelectedMotionPreference = fixture.ViewModel.Settings.MotionOptions.Single(option => option.Value == UiMotionPreference.Off);
            Application.Current!.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
            await fixture.PrepareAsync(state);
            if (populated)
            {
                var board = new SpeedrunBoardDescriptor(SpeedrunGame.HollowKnight, "any", "Any%", null, null, []);
                for (var i = 0; i < 30; i++)
                {
                    var run = new SpeedrunPodiumEntry($"run-{i}", 2, $"Player {i + 1}", "PT35M", 2100, DateTimeOffset.UnixEpoch, "https://example.invalid/run");
                    var entry = new SpeedrunActivityEntry(run.RunId, SpeedrunActivityKind.SecondPlace, board, run, DateTimeOffset.UnixEpoch);
                    fixture.ViewModel.SpeedrunActivities.Add(new SpeedrunActivityItemViewModel(entry, "#2", board.DisplayName));
                }
                fixture.ViewModel.SelectSpeedrunActivityFilterCommand.Execute("HollowKnight");
                fixture.ViewModel.SelectSpeedrunActivityFilterCommand.Execute("All");
            }
            fixture.Window.Width = width / scaling;
            fixture.Window.Height = height / scaling;
            fixture.Window.Show();
            fixture.Window.DataContext = fixture.ViewModel;
            fixture.Window.SetRenderScaling(scaling);
            Dispatcher.UIThread.RunJobs();
            if (state == ScreenshotState.SpeedrunActivity)
            {
                fixture.ViewModel.SelectSpeedrunTabCommand.Execute("Environment");
                fixture.ViewModel.SelectSpeedrunTabCommand.Execute("Activity");
            }
            await Task.Delay(400);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            AssertVisibleTextHasGlyphs(fixture.Window, name);
            using (var frame = Assert.IsType<WriteableBitmap>(fixture.Window.GetLastRenderedFrame()))
            {
                Assert.Equal(width, frame.PixelSize.Width);
                Assert.Equal(height, frame.PixelSize.Height);
                if (writeScreenshots)
                {
                    frame.Save(Path.Combine(output, name + ".jpg"), new JpegBitmapEncoderOptions { Quality = 96 });
                }
            }
            if (state is ScreenshotState.Speedrun or ScreenshotState.SpeedrunActivity)
            {
                var workspace = fixture.Window.FindControl<ScrollViewer>("SpeedrunWorkspace")!;
                workspace.Offset = new Vector(0, workspace.Extent.Height);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                AssertVisibleTextHasGlyphs(fixture.Window, name + "-bottom");
                using var frame = Assert.IsType<WriteableBitmap>(fixture.Window.GetLastRenderedFrame());
                Assert.Equal(width, frame.PixelSize.Width);
                Assert.Equal(height, frame.PixelSize.Height);
                if (writeScreenshots)
                {
                    frame.Save(Path.Combine(output, name + "-bottom.jpg"), new JpegBitmapEncoderOptions { Quality = 96 });
                }
            }
        }
    }

    private static void AssertVisibleTextHasGlyphs(Window window, string scenario)
    {
        var visibleText = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(text.Text))
            .ToArray();
        Assert.NotEmpty(visibleText);
        foreach (var text in visibleText)
        {
            var missing = text.TextLayout.TextLines.SelectMany(line => line.TextRuns)
                .OfType<ShapedTextRun>()
                .Where(run => run.GlyphRun.GlyphInfos.Any(glyph => glyph.GlyphIndex == 0))
                .Select(run => $"{scenario}: '{run.Text}' has a missing glyph in {run.GlyphRun.GlyphTypeface.FamilyName}.");
            Assert.True(!missing.Any(), string.Join(Environment.NewLine, missing));
        }
    }
}
