using System.Xml.Linq;

namespace Crystalfly.App.Tests.Ui;

public sealed class MainWindowStructureTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    [Fact]
    public void Main_navigation_and_instance_actions_follow_compact_workspace_contract()
    {
        var document = LoadMainWindow();
        var topbar = document.Descendants(Avalonia + "Border").Single(element => HasClass(element, "cfp-topbar"));
        var topbarGrid = topbar.Elements(Avalonia + "Grid").Single();

        Assert.Equal("*,Auto,*", (string?)topbarGrid.Attribute("ColumnDefinitions"));
        var chromeActions = topbarGrid.Elements(Avalonia + "StackPanel")
            .Single(panel => (string?)panel.Attribute("Grid.Column") == "2");
        Assert.Equal("Right", (string?)chromeActions.Attribute("HorizontalAlignment"));
        Assert.DoesNotContain(topbar.Descendants(Avalonia + "Button"), IsVersionsNavigationButton);

        var launchGrid = FindSectionRoot(document, "IsLaunchPage");
        Assert.DoesNotContain(launchGrid.Descendants(Avalonia + "Button"), button => HasBinding(button, "Command", "ManageSelectedInstanceCommand"));
        Assert.Contains(launchGrid.Descendants(Avalonia + "Button"), button => HasBinding(button, "Command", "SelectPageCommand") && (string?)button.Attribute("CommandParameter") == "Versions");
        Assert.Contains(launchGrid.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "OpenInstanceSettingsCommand")
            && HasBinding(button, "CommandParameter", "SelectedInstance"));

        var versionsGrid = FindSectionRoot(document, "IsVersionsPage");
        var instanceList = versionsGrid.Descendants(Avalonia + "ListBox").Single(list => HasClass(list, "cfp-instance-list"));
        var instanceRow = instanceList.Descendants(Avalonia + "Grid").Single(grid => HasClass(grid, "cfp-instance-row"));
        var instanceMain = instanceRow.Elements(Avalonia + "Button").Single(button => HasClass(button, "cfp-instance-main"));
        var instanceSummary = instanceRow.Elements(Avalonia + "Grid").Single(grid => HasClass(grid, "cfp-instance-summary"));
        var instanceActions = instanceRow.Elements(Avalonia + "StackPanel").Single(panel => HasClass(panel, "cfp-instance-actions"));

        Assert.Equal("Stretch", (string?)instanceRow.Attribute("HorizontalAlignment"));
        Assert.Equal("2", (string?)instanceMain.Attribute("Grid.ColumnSpan"));
        Assert.Equal("Left", (string?)instanceSummary.Attribute("HorizontalAlignment"));
        Assert.Equal("94", (string?)instanceActions.Attribute("Width"));
        Assert.Contains(versionsGrid.Descendants(Avalonia + "Button"), button => HasClass(button, "cfp-instance-action") && (string?)button.Attribute("Click") == "ConfirmDeleteInstance");
        Assert.Contains(versionsGrid.Descendants(Avalonia + "Button"), button => HasClass(button, "cfp-instance-action") && (string?)button.Attribute("Click") == "CloneInstanceWithName");
        Assert.Contains(versionsGrid.Descendants(Avalonia + "Button"), button => HasClass(button, "cfp-instance-action") && HasBinding(button, "Command", "OpenInstanceSettingsCommand"));
        Assert.DoesNotContain(versionsGrid.Descendants(Avalonia + "TextBox"), textBox => HasBinding(textBox, "Text", "CloneInstanceName"));

        var theme = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Styles",
            "CrystalflyTheme.axaml"));
        Assert.Contains("ListBox.cfp-instance-list > ListBoxItem:pointerover StackPanel.cfp-instance-actions", theme, StringComparison.Ordinal);
        Assert.Contains("ListBox.cfp-instance-list > ListBoxItem:selected StackPanel.cfp-instance-actions", theme, StringComparison.Ordinal);
        Assert.Contains("Button.cfp-instance-main:pointerover /template/ ContentPresenter#PART_ContentPresenter", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.cfp-instance-row:pointerover StackPanel.cfp-instance-actions", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void Installed_mods_use_compact_rows_hover_actions_and_bottom_bulk_bar()
    {
        var document = LoadMainWindow();
        var manageGrid = FindSectionRoot(document, "IsManagePage");
        var modsWorkspace = manageGrid.Descendants(Avalonia + "Grid")
            .Single(grid => HasClass(grid, "cfp-mods-workspace"));
        var modList = manageGrid.Descendants(Avalonia + "ListBox").Single(list => HasClass(list, "cfp-installed-mod-list"));
        var modListFrame = modList.Parent!;
        var bulkBar = manageGrid.Descendants(Avalonia + "Border").Single(border => HasClass(border, "cfp-mod-bulk-bar"));

        Assert.Equal("Auto,Auto,Auto,*,Auto", (string?)modsWorkspace.Attribute("RowDefinitions"));
        Assert.Empty(modsWorkspace.Ancestors(Avalonia + "ScrollViewer"));
        Assert.Equal("3", (string?)modListFrame.Attribute("Grid.Row"));
        Assert.Equal("4", (string?)bulkBar.Attribute("Grid.Row"));
        Assert.Contains(modList.Descendants(Avalonia + "Border"), border => HasClass(border, "cfp-installed-mod-accent"));
        Assert.Empty(modList.Descendants(Avalonia + "CheckBox"));
        Assert.Contains(modList.Descendants(Avalonia + "TextBlock"), text => HasClass(text, "cfp-installed-mod-name") && HasBinding(text, "Classes.disabled", "IsEnabled"));
        Assert.Contains(modList.Descendants(Avalonia + "StackPanel"), panel => HasClass(panel, "cfp-installed-mod-actions"));
        Assert.Contains(modList.Descendants(Avalonia + "Grid"), row =>
            HasClass(row, "cfp-installed-mod-row") && HasBinding(row, "Classes.selected", "IsSelected"));

        var quickActions = manageGrid.Descendants(Avalonia + "WrapPanel").Single(panel => HasClass(panel, "cfp-mod-quick-actions"));
        Assert.Equal(4, quickActions.Elements(Avalonia + "Button").Count());
        Assert.DoesNotContain(manageGrid.Descendants(Avalonia + "Border"), border => HasClass(border, "cfp-mod-bulk-card"));

        var theme = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Styles",
            "CrystalflyTheme.axaml"));
        Assert.Contains("Grid.cfp-installed-mod-row.selected Border.cfp-installed-mod-accent", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("ListBox.cfp-installed-mod-list > ListBoxItem:selected Border.cfp-installed-mod-accent", theme, StringComparison.Ordinal);
        Assert.Contains("ListBox.cfp-installed-mod-list > ListBoxItem:focus-within StackPanel.cfp-installed-mod-actions", theme, StringComparison.Ordinal);

        var iconOnlyButtons = document.Descendants(Avalonia + "Button")
            .Where(button => ((string?)button.Attribute("Content"))?.Contains("LucideIconContent", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.NotEmpty(iconOnlyButtons);
        Assert.All(iconOnlyButtons, button => Assert.False(
            string.IsNullOrWhiteSpace((string?)button.Attribute("AutomationProperties.Name")),
            $"Icon-only button is missing an automation name: {button}"));
    }

    [Fact]
    public void Download_fab_and_github_latency_controls_match_layout_contract()
    {
        var document = LoadMainWindow();
        var downloadButton = document.Descendants(Avalonia + "Button").Single(button => HasClass(button, "cfp-download-fab"));

        Assert.Equal("44", (string?)downloadButton.Attribute("Width"));
        Assert.Equal("44", (string?)downloadButton.Attribute("Height"));
        Assert.Contains("Download", (string?)downloadButton.Attribute("Content"));
        Assert.Empty(downloadButton.Descendants(Avalonia + "TextBlock"));
        Assert.True(HasBinding(downloadButton, "ToolTip.Tip", "ActiveDownloadSummary"));

        var settingsGrid = FindSectionRoot(document, "IsSettingsPage");
        Assert.Contains(settingsGrid.Descendants(Avalonia + "Button"), button => HasBinding(button, "Command", "TestGitHubLatencyCommand"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "GitHubDirectLatency"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "GitHubMirrorLatency"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "TextBox"), textBox =>
            HasBinding(textBox, "Text", "CustomModLinksUrl"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "ComboBox"), comboBox =>
            HasBinding(comboBox, "ItemsSource", "CustomModLinksBuildOptions"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "ComboBox"), comboBox =>
            HasBinding(comboBox, "ItemsSource", "CustomModLinksLoaderOptions"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "SaveCustomModLinksCommand"));
    }

    [Fact]
    public void Launch_integrity_state_stays_visible_and_offline_mode_is_global()
    {
        var document = LoadMainWindow();
        var launchGrid = FindSectionRoot(document, "IsLaunchPage");

        Assert.Contains(launchGrid.Descendants(Avalonia + "Border"), border =>
            HasClass(border, "cfp-launch-issue-frame")
            && HasBinding(border, "IsVisible", "HasLaunchIssues"));
        Assert.Contains(launchGrid.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "LaunchIssueCountText"));
        Assert.Contains(launchGrid.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ConfirmLaunch"
            && HasBinding(button, "IsEnabled", "CanAttemptLaunch"));
        Assert.Contains(launchGrid.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ShowLaunchIssues");

        var settingsGrid = FindSectionRoot(document, "IsSettingsPage");
        Assert.Contains(settingsGrid.Descendants(Avalonia + "CheckBox"), checkBox =>
            HasBinding(checkBox, "IsChecked", "IsOfflineMode")
            && HasBinding(checkBox, "Content", "OfflineMode"));
    }

    [Fact]
    public void Mod_detail_uses_sanitized_markdown_viewers_for_cached_repository_content()
    {
        var app = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "App.axaml"));
        Assert.Contains(app.Descendants(Avalonia + "StyleInclude"), style =>
            string.Equals(
                (string?)style.Attribute("Source"),
                "avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml",
                StringComparison.Ordinal));

        var document = LoadMainWindow();
        var viewers = document.Descendants().Where(element =>
            string.Equals(element.Name.LocalName, "MarkdownViewer", StringComparison.Ordinal)).ToArray();

        Assert.Equal(2, viewers.Length);
        Assert.Contains(viewers, viewer => HasBinding(viewer, "Markdown", "SelectedModReadmeMarkdown"));
        Assert.Contains(viewers, viewer => HasBinding(viewer, "Markdown", "SelectedModReleaseNotesMarkdown"));
        Assert.Contains(document.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "SelectedModContentError"));
        Assert.Contains(document.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "RepairSelectedMarketModCommand"));
        Assert.Contains(document.Descendants(Avalonia + "Button"), button =>
            string.Equals((string?)button.Attribute("Click"), "OpenSelectedMarketModFolder", StringComparison.Ordinal));
        Assert.Contains(document.Descendants(Avalonia + "Button"), button =>
            string.Equals((string?)button.Attribute("Click"), "ConfirmDeleteSelectedModGlobalSettings", StringComparison.Ordinal));
    }

    [Fact]
    public void Preset_page_exposes_confirmed_file_and_share_interactions()
    {
        var document = LoadMainWindow();
        var presets = document.Descendants(Avalonia + "StackPanel").Single(panel =>
            ((string?)panel.Attribute("IsVisible"))?.Contains("ConverterParameter=Presets", StringComparison.Ordinal) == true);

        Assert.Contains(presets.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ConfirmApplyPreset");
        Assert.Contains(presets.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ConfirmDeletePreset");
        Assert.Contains(presets.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ImportPresetFile");
        Assert.Contains(presets.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ExportSelectedPreset");
        Assert.Contains(presets.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "CopyPresetShareLink"
                && HasBinding(button, "IsVisible", "HasLastPresetShare") == false);

        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));
        Assert.Contains("private async void ConfirmApplyPreset", code, StringComparison.Ordinal);
        Assert.Contains("private async void ConfirmDeletePreset", code, StringComparison.Ordinal);
        Assert.Contains("private async void ImportPresetFile", code, StringComparison.Ordinal);
        Assert.Contains("private async void ExportSelectedPreset", code, StringComparison.Ordinal);
        Assert.Contains("private async void CopyPresetShareLink", code, StringComparison.Ordinal);
        Assert.Contains("PresetApplySteps", code, StringComparison.Ordinal);
        Assert.Contains("FilePickerSaveOptions", code, StringComparison.Ordinal);
        Assert.Contains("Clipboard.SetTextAsync", code, StringComparison.Ordinal);
    }

    private static XDocument LoadMainWindow() => XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "Crystalfly.App", "Views", "MainWindow.axaml"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static XElement FindSectionRoot(XDocument document, string visibilityProperty) => document.Descendants(Avalonia + "Grid").Single(element => ((string?)element.Attribute("IsVisible"))?.Contains(visibilityProperty, StringComparison.Ordinal) == true);
    private static bool HasClass(XElement element, string className) => ((string?)element.Attribute("Classes"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className) == true;
    private static bool HasBinding(XElement element, string attributeName, string path) => ((string?)element.Attribute(attributeName))?.Contains(path, StringComparison.Ordinal) == true;
    private static bool IsVersionsNavigationButton(XElement button) => HasBinding(button, "Command", "SelectPageCommand") && (string?)button.Attribute("CommandParameter") == "Versions";
}
