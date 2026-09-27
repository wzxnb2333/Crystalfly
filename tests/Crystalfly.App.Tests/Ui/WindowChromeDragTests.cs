using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Views;

namespace Crystalfly.App.Tests.Ui;

public sealed class WindowChromeDragTests
{
    [AvaloniaFact]
    public void Title_bar_blank_areas_and_brand_use_native_drag_hit_testing()
    {
        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        try
        {
            var brand = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(control => control.Classes.Contains("cfp-brand"));
            brand.Text = "Crystalfly";
            Dispatcher.UIThread.RunJobs();
            var titleBar = window.GetVisualDescendants().OfType<Border>()
                .Single(control => control.Classes.Contains("cfp-topbar"));
            var navigation = window.GetVisualDescendants().OfType<Border>()
                .Single(control => control.Classes.Contains("cfp-nav-group"));

            Assert.Equal(WindowDecorationsElementRole.TitleBar, HitTestChrome(window, brand, new Rect(brand.Bounds.Size).Center));
            Assert.Equal(WindowDecorationsElementRole.TitleBar, HitTestChrome(window, titleBar, new Point(180, titleBar.Bounds.Height / 2)));
            Assert.Equal(WindowDecorationsElementRole.TitleBar, HitTestChrome(window, navigation, new Point(1, navigation.Bounds.Height / 2)));
            Assert.Equal(WindowDecorationsElementRole.TitleBar, HitTestChrome(window, titleBar, new Point(titleBar.Bounds.Width - 175, titleBar.Bounds.Height / 2)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Title_bar_buttons_remain_clickable_without_starting_native_drag()
    {
        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var buttons = window.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Classes.Contains("cfp-nav") || button.Classes.Contains("cfp-window-button"))
                .ToArray();
            Assert.Equal(7, buttons.Length);
            foreach (var button in buttons)
            {
                Assert.Equal(WindowDecorationsElementRole.User, HitTestChrome(window, button, new Rect(button.Bounds.Size).Center));
            }

            var navigation = buttons.First(button => button.Classes.Contains("cfp-nav"));
            var clicked = 0;
            navigation.Click += (_, _) => clicked++;
            var center = navigation.TranslatePoint(new Rect(navigation.Bounds.Size).Center, window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Assert.Equal(1, clicked);
        }
        finally
        {
            window.Close();
        }
    }

    private static WindowDecorationsElementRole? HitTestChrome(Window window, Visual visual, Point localPoint)
    {
        // Exercise the same Avalonia hit-test callback used by the Win32 non-client handler.
        var inputRoot = typeof(TopLevel).GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!;
        var hitTest = typeof(IInputRoot).GetMethod("HitTestChromeElement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var point = visual.TranslatePoint(localPoint, window)!.Value;
        return (WindowDecorationsElementRole?)hitTest.Invoke(inputRoot, [point]);
    }
}
