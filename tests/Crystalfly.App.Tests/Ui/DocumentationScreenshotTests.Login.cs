using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.English)]
    public async Task Password_login_panel_can_be_opened_and_closed_at_minimum_window_size(UiLanguage language)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.GameVersions);
        fixture.ViewModel.Loc.Apply(language);
        fixture.ViewModel.IsSteamLoggedIn = false;
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = fixture.ViewModel;
        Dispatcher.UIThread.RunJobs();
        var open = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            ReferenceEquals(button.Command, fixture.ViewModel.TogglePasswordLoginCommand)
            && button.IsEffectivelyVisible);
        Click(open);
        Assert.True(fixture.ViewModel.IsPasswordLoginVisible);

        var password = Assert.Single(fixture.Window.GetVisualDescendants().OfType<TextBox>(), box =>
            AutomationProperties.GetName(box) == fixture.ViewModel.Loc["SteamPassword"]);
        Assert.True(password.IsEffectivelyVisible);
        Assert.Equal('●', password.PasswordChar);
        password.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        var origin = Assert.IsType<Point>(password.TranslatePoint(default, fixture.Window));
        Assert.InRange(origin.X, 0, fixture.Window.Bounds.Width - password.Bounds.Width);
        Assert.InRange(origin.Y, 0, fixture.Window.Bounds.Height - password.Bounds.Height);
        Assert.True(password.Focus());
        fixture.ViewModel.SteamPassword = "temporary-password";
        var close = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            ReferenceEquals(button.Command, fixture.ViewModel.TogglePasswordLoginCommand)
            && new ButtonAutomationPeer(button).GetName() == fixture.ViewModel.Loc["SwitchToQrLogin"]);
        foreach (var text in close.GetVisualDescendants().OfType<TextBlock>())
        {
            Assert.True(text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 1);
        }
        Click(close);
        Assert.False(fixture.ViewModel.IsPasswordLoginVisible);
        Assert.False(password.IsEffectivelyVisible);
        Assert.Empty(fixture.ViewModel.SteamPassword);

        void Click(Button button)
        {
            button.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = Assert.IsType<Point>(button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
            Assert.InRange(center.Y, 0, fixture.Window.Bounds.Height);
            fixture.Window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            fixture.Window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
