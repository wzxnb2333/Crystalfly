using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed class LayoutRenderingTests
{
    public static TheoryData<int, int, bool, bool, string, string?> LayoutCases
    {
        get
        {
            var data = new TheoryData<int, int, bool, bool, string, string?>();
            foreach (var (width, height) in new[]
                     {
                         (900, 600),
                         (1280, 720),
                         (1920, 1080),
                         (2560, 1440)
                     })
            {
                foreach (var darkTheme in new[] { false, true })
                {
                    foreach (var english in new[] { false, true })
                    {
                        data.Add(width, height, darkTheme, english, "Launch", null);
                        data.Add(width, height, darkTheme, english, "Settings", null);
                    }
                }
            }

            foreach (var darkTheme in new[] { false, true })
            {
                foreach (var english in new[] { false, true })
                {
                    data.Add(900, 600, darkTheme, english, "Manage", "Mods");
                    data.Add(900, 600, darkTheme, english, "Manage", "Logs");
                    data.Add(900, 600, darkTheme, english, "Downloads", null);
                    foreach (var tab in new[] { "Overview", "Loader", "Mods", "Snapshots", "Logs" })
                    {
                        data.Add(1280, 720, darkTheme, english, "Manage", tab);
                    }
                    data.Add(1280, 720, darkTheme, english, "Versions", null);
                    data.Add(1280, 720, darkTheme, english, "Speedrun", null);
                }
            }

            return data;
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(LayoutCases))]
    public void Workspaces_are_centered_and_interactive_controls_do_not_overflow(
        int width,
        int height,
        bool darkTheme,
        bool english,
        string page,
        string? manageTab)
    {
        Application.Current!.RequestedThemeVariant = darkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var applicationData = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationData)
        {
            CurrentPage = page,
            CurrentManageTab = manageTab ?? "Overview"
        };
        viewModel.Loc.Apply(english ? UiLanguage.English : UiLanguage.SimplifiedChinese);

        var window = new MainWindow
        {
            Width = width,
            Height = height
        };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var workspaces = window.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && control.Classes.Contains("cfp-workspace"))
                .ToArray();
            Assert.NotEmpty(workspaces);

            foreach (var workspace in workspaces)
            {
                var maximumWidth = workspace.Classes.Contains("cfp-settings-workspace") ? 1200d : 1920d;
                Assert.True(workspace.Bounds.Width <= maximumWidth + 0.5,
                    $"{page}/{manageTab}: workspace width {workspace.Bounds.Width:F1} exceeded {maximumWidth:F0}.");
                var scrollViewer = workspace.FindAncestorOfType<ScrollViewer>();
                Assert.NotNull(scrollViewer);
                var availableWidth = scrollViewer.Bounds.Width - workspace.Margin.Left - workspace.Margin.Right;
                var expectedWidth = Math.Min(maximumWidth, availableWidth);
                Assert.InRange(Math.Abs(workspace.Bounds.Width - expectedWidth), 0, 1.5);
                var workspaceOrigin = workspace.TranslatePoint(default, window);
                var scrollOrigin = scrollViewer.TranslatePoint(default, window);
                Assert.NotNull(workspaceOrigin);
                Assert.NotNull(scrollOrigin);
                var leftGap = workspaceOrigin.Value.X - scrollOrigin.Value.X;
                var rightGap = scrollOrigin.Value.X + scrollViewer.Bounds.Width
                    - workspaceOrigin.Value.X - workspace.Bounds.Width;
                Assert.InRange(Math.Abs(leftGap - rightGap), 0, 1.5);
            }

            var controls = window.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible
                    && control.Bounds.Width > 0
                    && control is Button or TextBox or ComboBox)
                .ToArray();
            foreach (var control in controls)
            {
                var origin = control.TranslatePoint(default, window);
                Assert.NotNull(origin);
                Assert.True(origin.Value.X >= -0.5,
                    $"{page}/{manageTab}: {control.GetType().Name} starts at {origin.Value.X:F1}.");
                Assert.True(origin.Value.X + control.Bounds.Width <= width + 0.5,
                    $"{page}/{manageTab}: {control.GetType().Name} ends at {origin.Value.X + control.Bounds.Width:F1} of {width}.");
            }
        }
        finally
        {
            CloseImmediately(window);
            if (Directory.Exists(applicationData))
            {
                Directory.Delete(applicationData, recursive: true);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData("Settings")]
    [InlineData("Downloads")]
    public void Visible_inputs_and_selectors_have_accessible_names(string page)
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = page
        };
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var controls = window.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && control is TextBox or ComboBox)
                .ToArray();
            Assert.NotEmpty(controls);
            Assert.All(controls, control =>
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                    $"{page}: {control.GetType().Name} has no accessible name."));
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaTheory]
    [InlineData("Versions")]
    [InlineData("Manage")]
    [InlineData("Speedrun")]
    [InlineData("Downloads")]
    [InlineData("Settings")]
    public void Secondary_page_headers_are_removed(string page)
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = page
        };
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            Assert.DoesNotContain(window.GetVisualDescendants()
                .OfType<Border>(),
                border => border.IsEffectivelyVisible && border.Classes.Contains("cfp-page-header"));
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Top_navigation_follows_visual_keyboard_order()
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")));
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var buttons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible
                    && button.Classes.Contains("cfp-nav")
                    && button.FindAncestorOfType<Border>()?.Classes.Contains("cfp-topbar") == true)
                .ToArray();
            Assert.Equal(5, buttons.Length);
            Assert.True(buttons[0].Focus(NavigationMethod.Tab, KeyModifiers.None));

            foreach (var expected in buttons.Skip(1))
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(expected, window.FocusManager?.GetFocusedElement());
            }
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Button_labels_and_icons_are_optically_centered_together()
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = "Downloads"
        };
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var iconAndTextButtons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible)
                .Select(button => new
                {
                    Button = button,
                    Icon = button.GetVisualDescendants()
                        .OfType<Control>()
                        .FirstOrDefault(control => control.IsEffectivelyVisible && control.GetType().Name == "LucideIcon"),
                    Text = button.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .FirstOrDefault(text => text.IsEffectivelyVisible)
                })
                .Where(item => item.Icon is not null && item.Text is not null)
                .ToArray();
            Assert.NotEmpty(iconAndTextButtons);

            foreach (var item in iconAndTextButtons)
            {
                var icon = Assert.IsAssignableFrom<Control>(item.Icon);
                var text = Assert.IsAssignableFrom<TextBlock>(item.Text);
                var iconOrigin = Assert.IsType<Point>(icon.TranslatePoint(default, item.Button));
                var textOrigin = Assert.IsType<Point>(text.TranslatePoint(default, item.Button));
                var iconCenter = iconOrigin.Y + icon.Bounds.Height / 2;
                var textCenter = textOrigin.Y + text.Bounds.Height / 2;
                Assert.InRange(Math.Abs(iconCenter - textCenter), 0, 0.5);
                Assert.InRange(Math.Abs(textCenter - (item.Button.Bounds.Height / 2 + 1)), 0, 0.5);

                var contentLeft = Math.Min(iconOrigin.X, textOrigin.X);
                var contentRight = Math.Max(iconOrigin.X + icon.Bounds.Width, textOrigin.X + text.Bounds.Width);
                Assert.InRange(Math.Abs((contentLeft + contentRight) / 2 - item.Button.Bounds.Width / 2), 0, 0.5);
            }
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Top_navigation_uses_a_text_brand_without_a_logo()
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")));
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var topbar = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("cfp-topbar"));
            var brand = topbar.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(text => text.Classes.Contains("cfp-brand"));
            var brandGrid = Assert.IsType<Grid>(brand.Parent);
            Assert.Equal(0, Grid.GetColumn(brand));
            Assert.DoesNotContain(brandGrid.Children, child => child is Border);
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Launch_rail_actions_are_vertical_equal_width_buttons()
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = "Launch"
        };
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var rail = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.IsEffectivelyVisible && border.Classes.Contains("cfp-rail"));
            var buttons = rail.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible)
                .ToArray();
            Assert.Equal(3, buttons.Length);

            var origins = buttons
                .Select(button => Assert.IsType<Point>(button.TranslatePoint(default, rail)))
                .ToArray();
            Assert.All(buttons, button => Assert.True(button.Bounds.Height >= 39.5));
            Assert.All(buttons, button => Assert.Equal(HorizontalAlignment.Center, button.HorizontalContentAlignment));
            Assert.All(buttons, button => Assert.InRange(Math.Abs(button.Bounds.Width - buttons[0].Bounds.Width), 0, 0.5));
            Assert.True(origins[1].Y >= origins[0].Y + buttons[0].Bounds.Height + 7.5);
            Assert.True(origins[2].Y >= origins[1].Y + buttons[1].Bounds.Height + 7.5);
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    public static TheoryData<double, string, string?> DpiCases
    {
        get
        {
            var data = new TheoryData<double, string, string?>();
            foreach (var scaling in new[] { 1d, 1.5d, 2d })
            {
                data.Add(scaling, "Settings", null);
                data.Add(scaling, "Downloads", null);
                data.Add(scaling, "Manage", "Mods");
            }

            return data;
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(DpiCases))]
    public void Button_labels_and_icons_are_not_clipped_at_common_dpi_scaling(
        double renderScaling,
        string page,
        string? manageTab)
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = page,
            CurrentManageTab = manageTab ?? "Overview"
        };
        viewModel.Loc.Apply(UiLanguage.English);

        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        window.SetRenderScaling(renderScaling);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);

        try
        {
            using var frame = Assert.IsType<Avalonia.Media.Imaging.WriteableBitmap>(window.GetLastRenderedFrame());
            Assert.Equal((int)Math.Round(window.Width * renderScaling), frame.PixelSize.Width);
            Assert.Equal((int)Math.Round(window.Height * renderScaling), frame.PixelSize.Height);

            var buttons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible
                    && button.Bounds.Width > 0
                    && button.Classes.Any(buttonClass => buttonClass.StartsWith("cfp-", StringComparison.Ordinal)))
                .ToArray();
            Assert.NotEmpty(buttons);

            foreach (var button in buttons)
            {
                Assert.True(button.Bounds.Height >= 35.5,
                    $"{page}/{manageTab}: button height {button.Bounds.Height:F1} at {renderScaling:P0} scaling.");
                var contentControls = button.GetVisualDescendants()
                    .OfType<Control>()
                    .Where(control => control.IsEffectivelyVisible
                        && (control is TextBlock || control.GetType().Name == "LucideIcon"));
                foreach (var content in contentControls)
                {
                    var origin = content.TranslatePoint(default, button);
                    Assert.NotNull(origin);
                    Assert.True(origin.Value.X >= -0.5 && origin.Value.Y >= -0.5,
                        $"{page}/{manageTab}: {content.GetType().Name} starts outside its button at {renderScaling:P0} scaling.");
                    Assert.True(origin.Value.X + content.Bounds.Width <= button.Bounds.Width + 0.5,
                        $"{page}/{manageTab}: {content.GetType().Name} '{(content as TextBlock)?.Text}' in button '{button.Content}' is clipped horizontally at {renderScaling:P0} scaling. "
                        + $"Origin={origin.Value.X:F1}, content={content.Bounds.Width:F1}, button={button.Bounds.Width:F1}.");
                    Assert.True(origin.Value.Y + content.Bounds.Height <= button.Bounds.Height + 0.5,
                        $"{page}/{manageTab}: {content.GetType().Name} is clipped vertically at {renderScaling:P0} scaling.");
                }
            }
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    private static void CloseImmediately(MainWindow window)
    {
        typeof(MainWindow)
            .GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, true);
        window.Close();
    }
}
