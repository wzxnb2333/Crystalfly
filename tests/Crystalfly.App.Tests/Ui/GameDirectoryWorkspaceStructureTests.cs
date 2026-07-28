using System.Xml.Linq;

namespace Crystalfly.App.Tests.Ui;

public sealed class GameDirectoryWorkspaceStructureTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    [Fact]
    public void Instance_workspace_is_a_two_column_directory_and_instance_surface()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "Crystalfly.App", "Views", "MainWindow.axaml"));
        var page = document.Descendants(Avalonia + "Grid").Single(element =>
            ((string?)element.Attribute("IsVisible"))?.Contains("IsVersionsPage", StringComparison.Ordinal) == true);
        var workspace = page.Descendants(Avalonia + "Grid").Single(element =>
            HasClass(element, "cfp-game-directory-workspace"));

        Assert.Equal("280,*", (string?)workspace.Attribute("ColumnDefinitions"));
        var directoryList = workspace.Descendants(Avalonia + "ListBox").Single(element =>
            HasClass(element, "cfp-game-directory-list"));
        Assert.Contains("GameDirectories", (string?)directoryList.Attribute("ItemsSource"), StringComparison.Ordinal);
        Assert.Contains("SelectedGameDirectory", (string?)directoryList.Attribute("SelectedItem"), StringComparison.Ordinal);
        Assert.Contains(directoryList.Descendants(Avalonia + "TextBlock"), element =>
            HasBinding(element, "Text", "DisplayName"));
        Assert.Contains(directoryList.Descendants(Avalonia + "TextBlock"), element =>
            HasBinding(element, "Text", "Path"));
        Assert.Contains(directoryList.Descendants(Avalonia + "TextBlock"), element =>
            HasBinding(element, "Text", "InstanceCount"));
        Assert.Contains(directoryList.Descendants(Avalonia + "TextBlock"), element =>
            HasBinding(element, "Text", "ScanStatus"));

        Assert.Contains(workspace.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "RefreshGameDirectoriesCommand"));
        Assert.Contains(workspace.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "AddCustomGameDirectory");
        Assert.Contains(workspace.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "RemoveSelectedGameDirectoryCommand"));

        var instanceList = workspace.Descendants(Avalonia + "ListBox").Single(element =>
            HasClass(element, "cfp-instance-list"));
        var instanceFrame = instanceList.Ancestors(Avalonia + "Border").First(element =>
            HasClass(element, "cfp-list-frame"));
        Assert.Contains("GameDirectories.Count", (string?)instanceFrame.Attribute("IsVisible"), StringComparison.Ordinal);
        Assert.Contains("VisibleInstances", (string?)instanceList.Attribute("ItemsSource"), StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_no_longer_exposes_the_single_version_root_picker()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "Crystalfly.App", "Views", "MainWindow.axaml"));

        Assert.DoesNotContain(document.Descendants(), element =>
            HasBinding(element, "SelectedPathsText", "VersionRoot")
            || HasBinding(element, "Command", "ApplyVersionRootCommand"));
    }

    [Fact]
    public void Discovery_and_steam_risk_overlays_are_registered()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "Crystalfly.App", "Views", "MainWindow.axaml.cs"));

        Assert.Contains("GameDirectoryDiscoveryRequested", code, StringComparison.Ordinal);
        Assert.Contains("GameDirectoryDiscoveryDialogView", code, StringComparison.Ordinal);
        Assert.Contains("SteamDirectoryRiskDialogView", code, StringComparison.Ordinal);
        Assert.Contains("SteamInstanceDeletionDialogView", code, StringComparison.Ordinal);
    }

    private static bool HasBinding(XElement element, string attribute, string path) =>
        ((string?)element.Attribute(attribute))?.Contains(path, StringComparison.Ordinal) == true;

    private static bool HasClass(XElement element, string className) =>
        ((string?)element.Attribute("Classes"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className) == true;

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Crystalfly.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
