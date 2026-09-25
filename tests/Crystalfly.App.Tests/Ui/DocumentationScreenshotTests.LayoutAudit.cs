using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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
