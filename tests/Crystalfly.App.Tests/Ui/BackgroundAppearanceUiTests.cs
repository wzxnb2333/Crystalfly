using System.Xml.Linq;

namespace Crystalfly.App.Tests.Ui;

public sealed class BackgroundAppearanceUiTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    [Fact]
    public void Main_window_exposes_full_client_background_and_accessible_settings_card()
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "src", "Crystalfly.App", "Views", "MainWindow.axaml"));
        var window = Assert.Single(document.Elements(Avalonia + "Window"));
        Assert.Contains("HasActiveBackgroundImage", (string?)window.Attribute("Classes.background-active"), StringComparison.Ordinal);

        var background = document.Descendants(Avalonia + "Image").Single(element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name"
                && attribute.Value == "ClientBackgroundImage"));
        Assert.Equal("UniformToFill", (string?)background.Attribute("Stretch"));
        Assert.Equal("False", (string?)background.Attribute("IsHitTestVisible"));
        Assert.Contains("ActiveBackgroundImage", (string?)background.Attribute("Source"), StringComparison.Ordinal);
        Assert.Contains("ActiveBackgroundOpacity", (string?)background.Attribute("Opacity"), StringComparison.Ordinal);

        var card = document.Descendants(Avalonia + "Border").Single(element =>
            ((string?)element.Attribute("AutomationProperties.Name"))?.Contains("BackgroundImage", StringComparison.Ordinal) == true);
        var generalSettings = document.Descendants(Avalonia + "StackPanel").Single(element =>
            ((string?)element.Attribute("IsVisible"))?.Contains("IsGeneralSettingsSection", StringComparison.Ordinal) == true);
        Assert.Contains(card, generalSettings.Descendants(Avalonia + "Border"));
        Assert.Contains(card.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "OnSelectBackgroundImageClicked"
            && button.Attribute("AutomationProperties.Name") is not null
            && button.Attribute("ToolTip.Tip") is not null);
        Assert.Contains(card.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "OnRemoveBackgroundImageClicked");
        var slider = Assert.Single(card.Descendants(Avalonia + "Slider"));
        Assert.Equal("0", (string?)slider.Attribute("Minimum"));
        Assert.Equal("100", (string?)slider.Attribute("Maximum"));
        Assert.Contains("BackgroundOpacityPercent", (string?)slider.Attribute("Value"), StringComparison.Ordinal);
        Assert.NotNull(slider.Attribute("AutomationProperties.Name"));
    }

    [Fact]
    public void Theme_defines_light_and_dark_translucent_surfaces_only_for_active_backgrounds()
    {
        var root = FindRepositoryRoot();
        var theme = File.ReadAllText(Path.Combine(root, "src", "Crystalfly.App", "Styles", "CrystalflyTheme.axaml"));

        Assert.Equal(2, Count(theme, "x:Key=\"CfChromeTranslucentBrush\""));
        Assert.Equal(2, Count(theme, "x:Key=\"CfRailTranslucentBrush\""));
        Assert.Equal(2, Count(theme, "x:Key=\"CfSurfaceTranslucentBrush\""));
        Assert.Contains("Window.background-active Border.cfp-topbar", theme, StringComparison.Ordinal);
        Assert.Contains("Window.background-active Border.cfp-rail", theme, StringComparison.Ordinal);
        Assert.Contains("Window.background-active Border.cfp-card", theme, StringComparison.Ordinal);
        Assert.Contains("#D1F3F4F6", theme, StringComparison.Ordinal);
        Assert.Contains("#E0F1F1F3", theme, StringComparison.Ordinal);
        Assert.Contains("#D1202123", theme, StringComparison.Ordinal);
        Assert.Contains("#E0242628", theme, StringComparison.Ordinal);
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        for (var offset = 0; (offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0; offset += search.Length)
        {
            count++;
        }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Crystalfly.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
