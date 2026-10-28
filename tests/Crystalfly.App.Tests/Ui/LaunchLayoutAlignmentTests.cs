using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Tests.Ui;

public sealed class LaunchLayoutAlignmentTests
{
    [AvaloniaTheory]
    [InlineData(900, 600, UiLanguage.SimplifiedChinese)]
    [InlineData(1280, 720, UiLanguage.English)]
    public async Task Empty_launch_shows_one_hint_and_no_status_badge(int width, int height, UiLanguage language)
    {
        await using var fixture = await Fixture.CreateAsync(width, height, language, true);
        var model = fixture.Model;
        var window = fixture.Window;
        model.SelectedInstance = null;
        model.CompleteOnboardingFromUiCommand.Execute(null);
        model.StatusMessage = model.Loc["ChooseRoot"];
        Fixture.Layout(window);

        if (Environment.GetEnvironmentVariable("CRYSTALFLY_EMPTY_LAUNCH_PREVIEW_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            using var frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(output, $"launch-empty-{width}x{height}-{language}.png"), new PngBitmapEncoderOptions());
        }

        var hint = Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == model.LaunchReadinessHint);
        Assert.Contains(Find<Border>(window, "LaunchHeader"), hint.GetVisualAncestors());
        Assert.False(Find<Border>(window, "LaunchStatusBadge").IsEffectivelyVisible);
        Assert.False(Find<Button>(window, "LaunchIssuesButton").IsEffectivelyVisible);
        Assert.True(Find<Border>(window, "CommunityRail").IsEffectivelyVisible);
    }

    [AvaloniaTheory]
    [InlineData(900, 600, UiLanguage.SimplifiedChinese, true)]
    [InlineData(900, 600, UiLanguage.English, false)]
    [InlineData(1100, 720, UiLanguage.English, false)]
    [InlineData(1180, 720, UiLanguage.English, true)]
    public async Task Launch_cards_actions_and_community_align_without_overlapping(
        int width, int height, UiLanguage language, bool dark)
    {
        await using var fixture = await Fixture.CreateAsync(width, height, language, dark);
        var window = fixture.Window;
        var workspace = Find<Grid>(window, "LaunchWorkspaceGrid");
        var scroll = Find<ScrollViewer>(window, "LaunchWorkspaceScroll");
        var onboarding = Find<Border>(window, "LaunchOnboardingCard");
        var instance = Find<Border>(window, "LaunchInstanceCard");
        var community = Find<Border>(window, "CommunityRail");
        var compact = width < 1180;

        Assert.True(onboarding.IsEffectivelyVisible);
        Assert.True(instance.IsEffectivelyVisible);
        Assert.Same(scroll, community.GetVisualAncestors().OfType<ScrollViewer>().First());
        Assert.Empty(community.GetVisualDescendants().OfType<ScrollViewer>());
        AssertClose(Origin(onboarding, workspace).X, Origin(instance, workspace).X);
        AssertClose(onboarding.Bounds.Width, instance.Bounds.Width);
        AssertClose(Origin(instance, workspace).Y - Bottom(onboarding, workspace), 12);
        Assert.All(new[] { onboarding, instance, community }, card => Assert.Equal(new Thickness(12), card.Padding));

        if (compact)
        {
            AssertClose(Origin(community, workspace).X, Origin(instance, workspace).X);
            AssertClose(community.Bounds.Width, instance.Bounds.Width);
            AssertClose(Origin(community, workspace).Y - Bottom(instance, workspace), 12);
        }
        else
        {
            AssertClose(Origin(community, workspace).Y, Origin(onboarding, workspace).Y);
            AssertClose(Origin(community, workspace).X - Right(instance, workspace), 12);
            AssertClose(community.Bounds.Width, 272);
        }

        var actions = Find<StackPanel>(window, "LaunchOnboardingActions").Children.OfType<Button>().ToArray();
        Assert.Equal(3, actions.Length);
        var details = Find<Button>(window, "LaunchDetailsButton");
        var issues = Find<Button>(window, "LaunchIssuesButton");
        Assert.All(actions.Concat([details, issues]), button =>
        {
            Assert.True(button.IsEffectivelyVisible);
            AssertClose(button.Bounds.Width, 160);
            AssertClose(button.Bounds.Height, 36);
            Assert.Equal(HorizontalAlignment.Center, button.HorizontalContentAlignment);
            AssertButtonTextFits(button);
        });
        Assert.All(actions, button => AssertClose(Right(button, workspace), Right(details, workspace)));
        if (compact) AssertClose(Right(issues, workspace), Right(details, workspace));
        Assert.False(Find<Border>(window, "LaunchStatusBadge").IsVisible);

        var railButtons = Find<StackPanel>(window, "LaunchPrimaryActions").Children.OfType<Button>().ToArray();
        Assert.Equal(3, railButtons.Length);
        Assert.All(railButtons, button =>
        {
            AssertClose(button.Bounds.Height, 44);
            AssertClose(button.Bounds.Width, railButtons[0].Bounds.Width);
            AssertButtonTextFits(button);
        });

        var checks = Find<StackPanel>(window, "LaunchChecks");
        var statuses = checks.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Classes.Contains("cfp-meta")).ToArray();
        Assert.Equal(4, statuses.Length);
        Assert.All(statuses, status =>
        {
            AssertClose(status.Bounds.Width, 160);
            AssertClose(Right(status, workspace), Right(details, workspace));
        });

        var hk = Find<StackPanel>(window, "HollowKnightCommunityGroup");
        var silk = Find<StackPanel>(window, "SilksongCommunityGroup");
        AssertClose(hk.Bounds.Width, silk.Bounds.Width);
        if (compact)
        {
            AssertClose(Origin(hk, community).Y, Origin(silk, community).Y);
            AssertClose(Origin(silk, community).X - Right(hk, community), 12);
        }
        else AssertClose(Origin(silk, community).Y - Bottom(hk, community), 12);
        var links = community.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("cfp-community-link")).ToArray();
        Assert.NotEmpty(links);
        Assert.Equal(fixture.Model.SpeedrunCommunityLinks.Links.Count, links.Length);
        Assert.All(links, link =>
        {
            AssertClose(link.Bounds.Width, links[0].Bounds.Width);
            AssertClose(link.Bounds.Height, links[0].Bounds.Height);
        });

        scroll.ScrollToEnd();
        Fixture.Layout(window);
        Assert.True(Bottom(community, scroll) <= scroll.Viewport.Height + 0.5);
        Assert.True(Bottom(instance, scroll) <= scroll.Viewport.Height + 0.5);
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5);
    }

    [AvaloniaFact]
    public async Task Resize_and_dismiss_keep_community_in_the_same_scroll_flow()
    {
        await using var fixture = await Fixture.CreateAsync(1100, 720, UiLanguage.SimplifiedChinese, true);
        var window = fixture.Window;
        var community = Find<Border>(window, "CommunityRail");
        var parent = Assert.IsType<Grid>(community.Parent);
        foreach (var width in new[] { 1280, 900, 1600, 1100 })
        {
            window.Width = width;
            Fixture.Layout(window);
            Assert.Same(parent, community.Parent);
            Assert.Equal(width < 1180 ? 2 : 1, Grid.GetRow(community));
            Assert.Equal(width < 1180 ? 0 : 1, Grid.GetColumn(community));
        }

        fixture.Model.CompleteOnboardingFromUiCommand.Execute(null);
        fixture.Model.LaunchPreflight = new(true, true, true, true);
        Fixture.Layout(window);
        Assert.False(Find<Border>(window, "LaunchOnboardingCard").IsVisible);
        Assert.False(Find<Button>(window, "LaunchIssuesButton").IsVisible);
        Assert.True(Find<Border>(window, "LaunchStatusBadge").IsVisible);
        var instance = Find<Border>(window, "LaunchInstanceCard");
        // Avalonia rounds the compact row spacing together with the one-DIP card border.
        Assert.InRange(Math.Abs((Origin(community, parent!).Y - Bottom(instance, parent!)) - 12), 0, 1.5);
    }

    private static T Find<T>(MainWindow window, string name) where T : Control => window.FindControl<T>(name)!;
    private static Point Origin(Control control, Visual relativeTo) => control.TranslatePoint(default, relativeTo)!.Value;
    private static double Right(Control control, Visual relativeTo) => Origin(control, relativeTo).X + control.Bounds.Width;
    private static double Bottom(Control control, Visual relativeTo) => Origin(control, relativeTo).Y + control.Bounds.Height;
    private static void AssertClose(double actual, double expected) => Assert.InRange(Math.Abs(actual - expected), 0, 0.5);

    private static void AssertButtonTextFits(Button button)
    {
        foreach (var text in button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible))
        {
            var origin = Origin(text, button);
            Assert.True(origin.X >= -0.5 && origin.Y >= -0.5);
            Assert.True(Right(text, button) <= button.Bounds.Width + 0.5, text.Text);
            Assert.True(Bottom(text, button) <= button.Bounds.Height + 0.5, text.Text);
            Assert.True(text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 0.5, text.Text);
        }
    }

    private sealed class Fixture(string root, MainViewModel model, MainWindow window) : IAsyncDisposable
    {
        public MainViewModel Model { get; } = model;
        public MainWindow Window { get; } = window;

        public static async Task<Fixture> CreateAsync(int width, int height, UiLanguage language, bool dark)
        {
            var root = Path.Combine(Path.GetTempPath(), "Crystalfly.LaunchAlignment", Guid.NewGuid().ToString("N"));
            await CrystalflySettingsStore.SaveAsync(Path.Combine(root, "settings.json"), new()
            {
                OfflineMode = true, CheckForUpdates = false, OnboardingCompleted = false,
                ErrorReportingEnabled = false, MotionPreference = UiMotionPreference.Off, Language = language
            });
            var model = new MainViewModel(root);
            await model.InitializeAsync();
            model.SelectedInstance = new InstanceItemViewModel(new InstanceRecord
            {
                Id = "alignment-fixture", GameId = GameId.Silksong, Name = "Silksong old",
                RootPath = Path.Combine(root, "game-library", "silksong old"), BuildId = "unknown",
                CreatedAt = DateTimeOffset.UtcNow
            }, model.Loc["UnknownVersion"], "bepinex-5.4.23.2 · 未验证", 0);
            model.LaunchPreflight = new(true, true, false, true,
            [
                new LaunchPreflightIssue
                {
                    Code = LaunchIssueCode.MissingDependency, Severity = LaunchIssueSeverity.Forceable,
                    Arguments = ["test-mod", "test-dependency"]
                }
            ]);
            model.StatusMessage = model.Loc["Ready"];
            var window = new MainWindow
            {
                Width = width, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            window.DataContext = model;
            model.CurrentPage = "Launch";
            Layout(window);
            return new Fixture(root, model, window);
        }

        public static void Layout(MainWindow window)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }

        public async ValueTask DisposeAsync()
        {
            typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Window, true);
            Window.Close();
            await Model.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
