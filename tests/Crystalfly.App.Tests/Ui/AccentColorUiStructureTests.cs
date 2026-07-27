using System.Xml.Linq;

namespace Crystalfly.App.Tests.Ui;

public sealed class AccentColorUiStructureTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    [Fact]
    public void Settings_exposes_accessible_accent_swatches_and_picker_dialog()
    {
        var root = FindRepositoryRoot();
        var mainWindow = XDocument.Load(Path.Combine(root, "src", "Crystalfly.App", "Views", "MainWindow.axaml"));
        var swatches = mainWindow.Descendants(Avalonia + "ItemsControl").Single(control =>
            ((string?)control.Attribute("ItemsSource"))?.Contains("AccentColorOptions", StringComparison.Ordinal) == true);
        var button = swatches.Descendants(Avalonia + "Button").Single();
        Assert.Contains("cfp-accent-swatch", (string?)button.Attribute("Classes"), StringComparison.Ordinal);
        Assert.Contains("IsSelected", (string?)button.Attribute("Classes.selected"), StringComparison.Ordinal);
        Assert.Equal("OnAccentColorClicked", (string?)button.Attribute("Click"));
        Assert.Contains("Name", (string?)button.Attribute("AutomationProperties.Name"), StringComparison.Ordinal);
        Assert.Contains("Name", (string?)button.Attribute("ToolTip.Tip"), StringComparison.Ordinal);

        var dialog = XDocument.Load(Path.Combine(
            root,
            "src",
            "Crystalfly.App",
            "Views",
            "Dialogs",
            "AccentColorDialogView.axaml"));
        var colorView = dialog.Descendants().Single(element => element.Name.LocalName == "ColorView");
        Assert.Contains("SelectedColor", (string?)colorView.Attribute("Color"), StringComparison.Ordinal);
        Assert.Equal("False", (string?)colorView.Attribute("IsAlphaEnabled"));
        Assert.Contains(dialog.Descendants(Avalonia + "TextBox"), textBox =>
            ((string?)textBox.Attribute("Text"))?.Contains("HexText", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void App_registers_matching_Semi_color_picker_theme_and_package()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "Crystalfly.App", "App.axaml"));
        var project = File.ReadAllText(Path.Combine(root, "src", "Crystalfly.App", "Crystalfly.App.csproj"));
        var packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        Assert.Contains("ColorPickerSemiTheme", app, StringComparison.Ordinal);
        Assert.Contains("Semi.Avalonia.ColorPicker", project, StringComparison.Ordinal);
        Assert.Contains("Semi.Avalonia.ColorPicker\" Version=\"12.1.0", packages, StringComparison.Ordinal);
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
