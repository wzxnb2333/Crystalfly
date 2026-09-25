using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(900, 600, UiLanguage.SimplifiedChinese)]
    [InlineData(900, 600, UiLanguage.English)]
    [InlineData(1280, 720, UiLanguage.English)]
    [InlineData(1920, 1080, UiLanguage.SimplifiedChinese)]
    public async Task Speedrun_switch_stays_below_scrolling_content_in_both_tabs(int width, int height, UiLanguage language)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.Speedrun);
        fixture.ViewModel.Loc.Apply(language);
        fixture.Window.Width = width;
        fixture.Window.Height = height;
        fixture.Window.Show();
        fixture.Window.DataContext = fixture.ViewModel;
        Dispatcher.UIThread.RunJobs();

        var tabSwitch = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Border>(),
            border => border.Classes.Contains("cfp-speedrun-tab-switch"));
        var layout = Assert.IsType<Grid>(tabSwitch.Parent);
        var workspace = fixture.Window.FindControl<ScrollViewer>("SpeedrunWorkspace")!;
        var content = Assert.IsType<Grid>(workspace.Parent);

        foreach (var tab in new[] { "Environment", "Activity", "Environment" })
        {
            var button = Assert.Single(tabSwitch.GetVisualDescendants().OfType<Button>(),
                candidate => Equals(candidate.CommandParameter, tab));
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = Assert.IsType<Point>(button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
            fixture.Window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            fixture.Window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Assert.Equal(tab, fixture.ViewModel.CurrentSpeedrunTab);
            Assert.Contains("active", button.Classes);
            Assert.True(tabSwitch.IsEffectivelyVisible);

            var origin = Assert.IsType<Point>(tabSwitch.TranslatePoint(default, layout));
            var contentOrigin = Assert.IsType<Point>(content.TranslatePoint(default, layout));
            Assert.True(content.Bounds.Height > 0);
            Assert.True(contentOrigin.Y + content.Bounds.Height <= origin.Y);
            Assert.InRange(origin.Y + tabSwitch.Bounds.Height, layout.Bounds.Height - 5, layout.Bounds.Height);
            Assert.InRange(origin.X + tabSwitch.Bounds.Width / 2, layout.Bounds.Width / 2 - 1, layout.Bounds.Width / 2 + 1);

            workspace.Offset = new Vector(0, workspace.Extent.Height);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(origin, tabSwitch.TranslatePoint(default, layout));
        }
    }

    [AvaloniaTheory]
    [InlineData(900, 600, UiLanguage.SimplifiedChinese)]
    [InlineData(900, 600, UiLanguage.English)]
    [InlineData(1280, 720, UiLanguage.English)]
    [InlineData(1920, 1080, UiLanguage.SimplifiedChinese)]
    public async Task Populated_pages_keep_untrimmed_text_inside_its_available_width(int width, int height, UiLanguage language)
    {
        ScreenshotState[] states =
        [
            ScreenshotState.Launch, ScreenshotState.LaunchIssues, ScreenshotState.InstanceSelection,
            ScreenshotState.GameVersions, ScreenshotState.MarketList, ScreenshotState.MarketDetail,
            ScreenshotState.InstanceDetail, ScreenshotState.InstalledModHealth, ScreenshotState.ModPresets,
            ScreenshotState.InstanceConfig, ScreenshotState.SaveEditor, ScreenshotState.Settings,
            ScreenshotState.Speedrun, ScreenshotState.SpeedrunActivity
        ];
        var failures = new List<string>();
        var cases = states.Select(state => (State: state, Section: "General")).Concat(
            new[] { "Network", "Catalog", "Updates", "About" }.Select(section => (ScreenshotState.Settings, section)));
        foreach (var (state, section) in cases)
        {
            await using var fixture = CreateFixture();
            await fixture.PrepareAsync(state);
            if (state == ScreenshotState.Settings) fixture.ViewModel.CurrentSettingsSection = section;
            fixture.ViewModel.Loc.Apply(language);
            Application.Current!.RequestedThemeVariant = language == UiLanguage.English ? ThemeVariant.Light : ThemeVariant.Dark;
            fixture.Window.Width = width;
            fixture.Window.Height = height;
            fixture.Window.Show();
            fixture.Window.DataContext = fixture.ViewModel;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            foreach (var button in fixture.Window.GetVisualDescendants().OfType<Button>()
                         .Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Classes.Any(c => c.StartsWith("cfp-", StringComparison.Ordinal))))
            {
                var name = new ButtonAutomationPeer(button).GetName();
                if (string.IsNullOrWhiteSpace(name))
                    failures.Add($"{state}/{section}: unnamed button with classes {string.Join(' ', button.Classes)}.");
            }
            foreach (var text in fixture.Window.GetVisualDescendants().OfType<TextBlock>())
            {
                if (!text.IsEffectivelyVisible || text.Bounds.Width <= 0 || string.IsNullOrWhiteSpace(text.Text)
                    || text.TextTrimming != TextTrimming.None
                    || text.FindAncestorOfType<TextBox>() is not null)
                {
                    continue;
                }
                if (text.TextLayout.WidthIncludingTrailingWhitespace > text.Bounds.Width + 1)
                {
                    failures.Add($"{state}: '{text.Text}' needs {text.TextLayout.WidthIncludingTrailingWhitespace:F1}px, has {text.Bounds.Width:F1}px.");
                }
                if (text.FindAncestorOfType<Button>() is { } button
                    && text.TranslatePoint(default, button) is { } origin
                    && (origin.X < -1 || origin.X + text.Bounds.Width > button.Bounds.Width + 1))
                {
                    failures.Add($"{state}: button text '{text.Text}' extends past its button ({origin.X:F1} + {text.Bounds.Width:F1} > {button.Bounds.Width:F1}).");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct().Take(60)));
    }
}
