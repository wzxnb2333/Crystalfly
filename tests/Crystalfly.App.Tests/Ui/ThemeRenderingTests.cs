using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Crystalfly.App.Views;

namespace Crystalfly.App.Tests.Ui;

public sealed class ThemeRenderingTests
{
    [AvaloniaFact]
    public void Application_registers_Semi_and_Ursa_themes_without_Fluent()
    {
        var styleTypes = Application.Current!.Styles
            .Select(style => style.GetType().Name)
            .ToArray();

        Assert.Contains("SemiTheme", styleTypes);
        Assert.Contains("UrsaSemiTheme", styleTypes);
        Assert.DoesNotContain("FluentTheme", styleTypes);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Primary_button_text_inherits_a_readable_foreground(bool darkTheme)
    {
        Application.Current!.RequestedThemeVariant = darkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var label = new TextBlock { Text = "Launch game" };
        var button = new Button { Content = label };
        button.Classes.Add("cfp-primary");

        var window = ShowInWindow(button);
        try
        {
            var foreground = ColorOf(label.Foreground);
            var background = ColorOf(button.Background);
            Assert.Equal(ColorOf(button.Foreground), foreground);
            Assert.True(ContrastRatio(foreground, background) >= 4.5,
                $"Expected 4.5:1 contrast, got {ContrastRatio(foreground, background):F2}:1.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_list_item_uses_the_application_selection_brush(bool darkTheme)
    {
        Application.Current!.RequestedThemeVariant = darkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var list = new ListBox
        {
            ItemsSource = new[] { "Selected item" },
            SelectedIndex = 0
        };
        list.Classes.Add("cfp-list");

        var window = ShowInWindow(list);
        try
        {
            var item = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
            Assert.True(Application.Current.TryGetResource(
                "CfSurfaceSelectedBrush",
                darkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
                out var selectedBrush));
            Assert.Equal(ColorOf((IBrush)selectedBrush!), ColorOf(item.Background));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false, "cfp-primary")]
    [InlineData(true, "cfp-danger")]
    public void Confirmation_button_style_matches_operation_risk(bool isDangerous, string expectedClass)
    {
        var button = new Button();

        MainWindow.ApplyConfirmationStyle(button, isDangerous);

        Assert.Contains(expectedClass, button.Classes);
    }

    [AvaloniaTheory]
    [InlineData(false, "cfp-primary")]
    [InlineData(false, "cfp-accent")]
    [InlineData(false, "cfp-secondary")]
    [InlineData(false, "cfp-danger")]
    [InlineData(true, "cfp-primary")]
    [InlineData(true, "cfp-accent")]
    [InlineData(true, "cfp-secondary")]
    [InlineData(true, "cfp-danger")]
    public void Button_focus_and_disabled_states_remain_visible(bool darkTheme, string buttonClass)
    {
        Application.Current!.RequestedThemeVariant = darkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var label = new TextBlock { Text = "Action" };
        var button = new Button { Content = label };
        button.Classes.Add(buttonClass);
        var window = ShowInWindow(button);
        try
        {
            button.Focus(NavigationMethod.Tab, KeyModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(button.BorderBrush);
            Assert.True(button.BorderThickness.Left >= 2);

            button.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            var ratio = ContrastRatio(ColorOf(label.Foreground), ColorOf(button.Background));
            Assert.True(ratio >= 4.5, $"{buttonClass} disabled contrast was {ratio:F2}:1.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Application_type_and_control_scale_remains_readable()
    {
        var body = new TextBlock { Text = "Body" };
        var pageTitle = new TextBlock { Text = "Page" };
        pageTitle.Classes.Add("cfp-page-title");
        var sectionTitle = new TextBlock { Text = "Section" };
        sectionTitle.Classes.Add("cfp-section-title");
        var meta = new TextBlock { Text = "Metadata" };
        meta.Classes.Add("cfp-meta");
        var caption = new TextBlock { Text = "Caption" };
        caption.Classes.Add("cfp-caption");
        var primaryButton = new Button { Content = "Primary" };
        primaryButton.Classes.Add("cfp-primary");
        var secondaryButton = new Button { Content = "Secondary" };
        secondaryButton.Classes.Add("cfp-secondary");
        var iconButton = new Button { Content = "I" };
        iconButton.Classes.Add("cfp-icon");
        var input = new TextBox();
        input.Classes.Add("cfp-input");

        var window = ShowInWindow(new StackPanel
        {
            Children =
            {
                body,
                pageTitle,
                sectionTitle,
                meta,
                caption,
                primaryButton,
                secondaryButton,
                iconButton,
                input
            }
        });
        try
        {
            Assert.Equal(16, body.FontSize);
            Assert.Equal(24, pageTitle.FontSize);
            Assert.Equal(18, sectionTitle.FontSize);
            Assert.Equal(14, meta.FontSize);
            Assert.Equal(13, caption.FontSize);
            Assert.Equal(48, primaryButton.MinHeight);
            Assert.Equal(44, secondaryButton.MinHeight);
            Assert.Equal(44, iconButton.Width);
            Assert.Equal(44, iconButton.Height);
            Assert.Equal(44, input.MinHeight);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window ShowInWindow(Control content)
    {
        var window = new Window
        {
            Width = 480,
            Height = 240,
            Content = content
        };
        window.Classes.Add("cfp-window");
        window.Show();
        return window;
    }

    private static Color ColorOf(IBrush? brush)
        => Assert.IsType<SolidColorBrush>(brush).Color;

    private static double ContrastRatio(Color foreground, Color background)
    {
        static double Luminance(Color color)
        {
            static double Linear(byte channel)
            {
                var value = channel / 255d;
                return value <= 0.04045
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Linear(color.R))
                + (0.7152 * Linear(color.G))
                + (0.0722 * Linear(color.B));
        }

        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }
}
