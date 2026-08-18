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
        var primaryNavigation = topbarGrid.Elements(Avalonia + "StackPanel")
            .Single(panel => (string?)panel.Attribute("Grid.Column") == "1");
        var chromeActions = topbarGrid.Elements(Avalonia + "StackPanel")
            .Single(panel => (string?)panel.Attribute("Grid.Column") == "2");
        Assert.Equal("Right", (string?)chromeActions.Attribute("HorizontalAlignment"));
        Assert.DoesNotContain(topbar.Descendants(Avalonia + "Button"), IsVersionsNavigationButton);
        var settingsTab = primaryNavigation.Elements(Avalonia + "Button").Single(button =>
            HasBinding(button, "Command", "SelectPageCommand")
            && (string?)button.Attribute("CommandParameter") == "Settings");
        Assert.Contains(settingsTab.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "NavSettings"));
        Assert.DoesNotContain(chromeActions.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "SelectPageCommand")
            && (string?)button.Attribute("CommandParameter") == "Settings");

        var launchGrid = FindSectionRoot(document, "IsLaunchPage");
        Assert.DoesNotContain(launchGrid.Descendants(Avalonia + "Button"), button => HasBinding(button, "Command", "ManageSelectedInstanceCommand"));
        Assert.Contains(launchGrid.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "SelectPageCommand")
            && (string?)button.Attribute("CommandParameter") == "Versions"
            && HasBinding(button, "Content", "SelectInstance"));
        Assert.Contains(launchGrid.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "OpenInstanceSettingsCommand")
            && HasBinding(button, "CommandParameter", "SelectedInstance"));

        var versionsGrid = FindSectionRoot(document, "IsVersionsPage");
        Assert.Contains(versionsGrid.Descendants(Avalonia + "TextBlock"), text =>
            HasClass(text, "cfp-section-title")
            && HasBinding(text, "Text", "SelectInstance"));
        var instanceList = versionsGrid.Descendants(Avalonia + "ListBox").Single(list => HasClass(list, "cfp-instance-list"));
        var instanceRow = instanceList.Descendants(Avalonia + "Grid").Single(grid => HasClass(grid, "cfp-instance-row"));
        var instanceMain = instanceRow.Elements(Avalonia + "Button").Single(button => HasClass(button, "cfp-instance-main"));
        var instanceSummary = instanceRow.Elements(Avalonia + "Grid").Single(grid => HasClass(grid, "cfp-instance-summary"));
        var instanceActions = instanceRow.Elements(Avalonia + "StackPanel").Single(panel => HasClass(panel, "cfp-instance-actions"));

        Assert.Equal("Stretch", (string?)instanceRow.Attribute("HorizontalAlignment"));
        Assert.Equal("2", (string?)instanceMain.Attribute("Grid.ColumnSpan"));
        Assert.Equal("Left", (string?)instanceSummary.Attribute("HorizontalAlignment"));
        Assert.Equal("126", (string?)instanceActions.Attribute("Width"));
        Assert.Contains(versionsGrid.Descendants(Avalonia + "Button"), button =>
            HasClass(button, "cfp-instance-action")
            && HasBinding(button, "Command", "ToggleFavoriteInstanceCommand"));
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
    public void Page_surfaces_use_pcl_style_translation_without_nested_scale_motion()
    {
        var document = LoadMainWindow();
        foreach (var page in new[]
                 {
                     "IsLaunchPage",
                     "IsVersionsPage",
                     "IsManagePage",
                     "IsSpeedrunPage",
                     "IsDownloadsPage",
                     "IsSettingsPage"
                 })
        {
            Assert.True(HasClass(FindSectionRoot(document, page), "cfp-page"), page);
        }

        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));
        Assert.Contains("motionCoordinator.Start();", code, StringComparison.Ordinal);
        var motionCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Services",
            "MotionCoordinator.cs"));
        Assert.Contains("SubscribeEntranceAnimations", motionCode, StringComparison.Ordinal);
        Assert.Contains("AreClientAreaAnimationsEnabled()", motionCode, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(350)", motionCode, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(100)", motionCode, StringComparison.Ordinal);
        Assert.Contains("PageEntranceOffset", motionCode, StringComparison.Ordinal);
        Assert.Contains("PageEntranceOffset = -16d", motionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("QueueVisibleDescendants", motionCode, StringComparison.Ordinal);
        Assert.Contains("IsOpacityEntranceTarget", motionCode, StringComparison.Ordinal);
        Assert.Contains("animation.Motion.Translate.Y", motionCode, StringComparison.Ordinal);
        Assert.Contains("EasePclWeakBack", motionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("animation.Motion.Scale", motionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("PageEntranceScale", motionCode, StringComparison.Ordinal);
        Assert.Contains("activeEntranceAnimations", motionCode, StringComparison.Ordinal);
        Assert.Contains("OnEntranceAnimationFrame", motionCode, StringComparison.Ordinal);
        Assert.Contains("RequestAnimationFrame(OnEntranceAnimationFrame)", motionCode, StringComparison.Ordinal);
        Assert.Contains("IsEntranceAnimationTarget", motionCode, StringComparison.Ordinal);
        Assert.Contains("ConfigureMicroInteractionTransitions", motionCode, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(80)", motionCode, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(220)", motionCode, StringComparison.Ordinal);
        Assert.Contains("Visual.OpacityProperty", motionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("QueueVisibleNavigationAnimations", motionCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Motion_system_exposes_a_persisted_preference_and_uses_press_only_scaling()
    {
        var document = LoadMainWindow();
        var settings = FindSectionRoot(document, "IsSettingsPage");
        Assert.Contains(settings.Descendants(Avalonia + "ComboBox"), comboBox =>
            HasBinding(comboBox, "ItemsSource", "MotionOptions")
            && HasBinding(comboBox, "SelectedItem", "SelectedMotionPreference"));

        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));
        Assert.Contains("UiMotionPreference", code, StringComparison.Ordinal);
        Assert.Contains("PointerPressed", code, StringComparison.Ordinal);
        Assert.Contains("UpdateMotionPreference", code, StringComparison.Ordinal);
        Assert.Contains("MainOverlayDialogHost.Children.CollectionChanged", code, StringComparison.Ordinal);
        Assert.Contains("RegisterToastMotionTargets", code, StringComparison.Ordinal);
        var motionCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Services",
            "MotionCoordinator.cs"));
        Assert.DoesNotContain("SpringEasing", motionCode, StringComparison.Ordinal);
        Assert.Contains("ScaleTransform", motionCode, StringComparison.Ordinal);
        Assert.Contains("PointerExited", motionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("OnMicroInteractionPointerEntered", motionCode, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource", motionCode, StringComparison.Ordinal);
        Assert.Contains("EnsureEntranceAnimationTimer", motionCode, StringComparison.Ordinal);
        Assert.Contains("CompleteEntranceAnimation", motionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("RunAsync(motion.Translate, cancellationToken)", motionCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_rails_stretch_navigation_to_their_inner_edges()
    {
        var document = LoadMainWindow();
        foreach (var section in new[] { "IsManagePage", "IsDownloadsPage", "IsSettingsPage" })
        {
            var rail = FindSectionRoot(document, section)
                .Descendants(Avalonia + "Border")
                .Single(border => HasClass(border, "cfp-rail"));
            var nav = rail.Descendants(Avalonia + "StackPanel")
                .Single(panel => HasClass(panel, "cfp-manage-nav"));

            Assert.Equal("8,16", (string?)rail.Attribute("Padding"));
            Assert.NotEmpty(nav.Elements(Avalonia + "Button"));
            Assert.All(nav.Elements(Avalonia + "Button"), button => Assert.True(HasClass(button, "cfp-local-nav")));
        }

        var theme = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Styles",
            "CrystalflyTheme.axaml"));
        Assert.Contains("StackPanel.cfp-manage-nav", theme, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.cfp-manage-nav", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("Border.cfp-download-rail Button.cfp-local-nav TextBlock", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void Speedrun_page_exposes_environment_and_activity_workspaces()
    {
        var document = LoadMainWindow();
        var speedrun = FindSectionRoot(document, "IsSpeedrunPage");
        var tabs = speedrun.Descendants(Avalonia + "Button")
            .Where(button => HasClass(button, "cfp-speedrun-tab-segment"))
            .ToArray();
        Assert.Equal(2, tabs.Length);
        Assert.Contains(tabs, button => HasBinding(button, "Command", "SelectSpeedrunTabCommand")
            && (string?)button.Attribute("CommandParameter") == "Environment"
            && HasBinding(button, "Classes.active", "IsSpeedrunEnvironmentTab"));
        Assert.Contains(tabs, button => HasBinding(button, "Command", "SelectSpeedrunTabCommand")
            && (string?)button.Attribute("CommandParameter") == "Activity"
            && HasBinding(button, "Classes.active", "IsSpeedrunActivityTab"));
        var tabSwitch = speedrun.Descendants(Avalonia + "Border")
            .Single(border => HasClass(border, "cfp-speedrun-tab-switch"));
        Assert.Null(tabSwitch.Attribute("Grid.Row"));
        Assert.Equal("Bottom", (string?)tabSwitch.Attribute("VerticalAlignment"));
        Assert.Equal("10", (string?)tabSwitch.Attribute("ZIndex"));
        Assert.Contains(tabSwitch.Descendants(Avalonia + "Border"), border =>
            HasClass(border, "cfp-speedrun-tab-indicator")
            && (string?)border.Attribute("Grid.Column") == "0"
            && border.Attribute("Width") is null);
        Assert.Contains(speedrun.Descendants(Avalonia + "Border"), border =>
            HasClass(border, "cfp-rail")
            && HasBinding(border, "IsVisible", "IsSpeedrunEnvironmentTab"));
        var reminder = document.Descendants(Avalonia + "Border")
            .Single(border => HasClass(border, "cfp-speedrun-reminder"));
        Assert.Contains(reminder.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "DismissSpeedrunReminderCommand"));
        Assert.Contains(speedrun.Descendants(Avalonia + "ScrollViewer"), scrollViewer =>
            (string?)scrollViewer.Attribute("PointerPressed") == "OnSpeedrunWorkspacePointerPressed"
            && (string?)scrollViewer.Attribute("PointerReleased") == "OnSpeedrunWorkspacePointerReleased");

        var activity = speedrun.Descendants(Avalonia + "Grid")
            .Single(grid => HasClass(grid, "cfp-speedrun-activity"));
        Assert.Equal(3, activity.Descendants(Avalonia + "Button")
            .Count(button => HasBinding(button, "Command", "SelectSpeedrunActivityFilterCommand")));
        Assert.Contains(activity.Descendants(Avalonia + "ItemsControl"), items =>
            HasBinding(items, "ItemsSource", "VisibleSpeedrunActivities"));
        Assert.Contains(activity.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "OpenSpeedrunRun"
            && HasBinding(button, "Tag", "Run.RunUrl"));

        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.SpeedrunHandlers.cs"));
        Assert.Contains("private void OpenSpeedrunRun", code, StringComparison.Ordinal);
        Assert.Contains("speedrun.com", code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_1578_save_state_selector_is_an_explicit_binary_choice()
    {
        var document = LoadMainWindow();
        var selectors = document.Descendants(Avalonia + "RadioButton")
            .Where(radio => (string?)radio.Attribute("GroupName") == "speedrun-save-states-mode")
            .ToArray();

        Assert.Equal(2, selectors.Length);
        Assert.Contains(selectors, radio =>
            (string?)radio.Attribute("Content") == "MiniSaveStates"
            && HasBinding(radio, "IsChecked", "RuntimePatchesMultiSaveStates")
            && ((string?)radio.Attribute("IsChecked"))!.Contains("BoolConverters.Not", StringComparison.Ordinal));
        Assert.Contains(selectors, radio =>
            (string?)radio.Attribute("Content") == "MultiSaveStates"
            && HasBinding(radio, "IsChecked", "RuntimePatchesMultiSaveStates"));
        Assert.DoesNotContain(document.Descendants(Avalonia + "ToggleSwitch"), toggle =>
            HasBinding(toggle, "IsChecked", "RuntimePatchesMultiSaveStates"));
    }

    [Fact]
    public void Main_window_requests_Windows_11_rounded_corners_after_opening()
    {
        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));

        Assert.Contains("ApplyWin11RoundedCorners();", code, StringComparison.Ordinal);
        Assert.Contains("DwmSetWindowAttribute", code, StringComparison.Ordinal);
        Assert.Contains("DwmwaWindowCornerPreference = 33", code, StringComparison.Ordinal);
        Assert.Contains("DwmwcpRound = 2", code, StringComparison.Ordinal);
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
            HasClass(row, "cfp-installed-mod-row")
            && HasBinding(row, "Classes.selected", "IsSelected")
            && (string?)row.Attribute("Background") == "Transparent");

        var quickActions = manageGrid.Descendants(Avalonia + "WrapPanel").Single(panel => HasClass(panel, "cfp-mod-quick-actions"));
        Assert.Equal(7, quickActions.Elements(Avalonia + "Button").Count());
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
    public void Mods_workspace_has_master_detail_panel_update_check_and_conflict_highlight()
    {
        var document = LoadMainWindow();
        var manageGrid = FindSectionRoot(document, "IsManagePage");
        var modsWorkspace = manageGrid.Descendants(Avalonia + "Grid")
            .Single(grid => HasClass(grid, "cfp-mods-workspace"));
        var modList = manageGrid.Descendants(Avalonia + "ListBox").Single(list => HasClass(list, "cfp-installed-mod-list"));

        var detailPanel = modsWorkspace.Descendants(Avalonia + "Border")
            .Single(border => HasClass(border, "cfp-mod-detail-panel"));
        Assert.True(HasBinding(detailPanel, "IsVisible", "ModManagement.SelectedInstalledMod"));
        Assert.Contains(detailPanel.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ModManagement.SelectedInstalledMod.DependenciesText"));
        Assert.Contains(detailPanel.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ModManagement.SelectedInstalledMod.AuthorsText"));
        Assert.Contains(detailPanel.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ModManagement.SelectedInstalledMod.RepositoryUrl"));
        Assert.Contains(detailPanel.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ModManagement.SelectedInstalledMod.LatestVersionText"));
        Assert.Contains(detailPanel.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ModManagement.SelectedInstalledMod.InstallDateText"));
        Assert.Contains(detailPanel.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ModManagement.SelectedInstalledMod.ModifiedFilesText"));
        Assert.Contains(detailPanel.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ModManagement.SelectedInstalledMod.ConflictWithText"));

        Assert.Contains(modsWorkspace.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "ModManagement.CheckForUpdatesCommand"));

        var conflictRow = modList.Descendants(Avalonia + "Grid")
            .Single(grid => HasClass(grid, "cfp-installed-mod-row"));
        Assert.True(HasBinding(conflictRow, "Classes.conflict", "HasConflicts"));
        Assert.Contains(modList.Descendants(Avalonia + "TextBlock"), text =>
            HasClass(text, "cfp-mod-conflict") && HasBinding(text, "Text", "ConflictWithText"));

        var graphFrame = manageGrid.Descendants(Avalonia + "Border")
            .Single(border => HasClass(border, "cfp-dependency-graph-frame"));
        // The graph frame hosts the DependencyGraphView directly — no title bar and no
        // expand/collapse buttons (they had no effect on the flat graph).
        Assert.Contains(graphFrame.Descendants(), element => element.Name.LocalName == "DependencyGraphView");
        Assert.DoesNotContain(graphFrame.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "DependencyGraph.ExpandNodeCommand"));
        Assert.DoesNotContain(graphFrame.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "DependencyGraph.CollapseNodeCommand"));

        var quickActions = manageGrid.Descendants(Avalonia + "WrapPanel")
            .Single(panel => HasClass(panel, "cfp-mod-quick-actions"));
        Assert.Equal(7, quickActions.Elements(Avalonia + "Button").Count());
    }

    [Fact]
    public void Installed_mod_dependency_graph_has_no_selected_component_scope_controls()
    {
        var document = LoadMainWindow();
        var manageGrid = FindSectionRoot(document, "IsManagePage");
        var graphFrame = manageGrid.Descendants(Avalonia + "Border")
            .Single(border => HasClass(border, "cfp-dependency-graph-frame"));

        Assert.DoesNotContain(graphFrame.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "ShowFocusedDependencyGraphCommand"));
        Assert.DoesNotContain(graphFrame.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "ShowAllDependencyGraphCommand"));
        Assert.DoesNotContain(graphFrame.Descendants(Avalonia + "StackPanel"), panel =>
            HasClass(panel, "cfp-graph-scope"));
    }

    [Fact]
    public void Download_center_exposes_batch_toolbar_and_overview_bindings()
    {
        var document = LoadMainWindow();
        var queueSection = FindDownloadQueueSection(document);

        Assert.Contains(queueSection.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "DownloadCenter.RetryAllCommand")
            && HasBinding(button, "IsEnabled", "DownloadCenter.CanRetryAll"));
        Assert.Contains(queueSection.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "DownloadCenter.PauseAllCommand")
            && HasBinding(button, "IsEnabled", "DownloadCenter.CanPauseAll"));
        Assert.Contains(queueSection.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "DownloadCenter.ResumeAllCommand")
            && HasBinding(button, "IsEnabled", "DownloadCenter.CanResumeAll"));
        Assert.Contains(queueSection.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "DownloadCenter.ClearCompletedCommand")
            && HasBinding(button, "IsEnabled", "DownloadCenter.CanClearCompleted"));
        Assert.Contains(queueSection.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "DownloadCenter.ActiveCountText"));
        Assert.Contains(queueSection.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "DownloadCenter.TotalSpeedText"));
        Assert.Contains(queueSection.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "DownloadCenter.OverallEtaText"));
    }

    [Fact]
    public void Download_queue_cards_expose_eta_duration_and_error_copy_bindings()
    {
        var document = LoadMainWindow();
        var queueSection = FindDownloadQueueSection(document);

        Assert.Contains(queueSection.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "EtaText"));
        Assert.Contains(queueSection.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "DurationText")
            && HasBinding(text, "IsVisible", "HasDuration"));
        Assert.Contains(queueSection.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "ErrorCategoryText"));
        Assert.Contains(queueSection.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "CopyErrorCommand"));
        Assert.Contains(queueSection.Descendants(Avalonia + "ItemsControl"), items =>
            HasBinding(items, "ItemsSource", "DownloadCenter.DownloadQueueGroups"));
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
        Assert.Contains(settingsGrid.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "GitHubGhProxyOrgLatency"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "GitHubGhProxyNetLatency"));
        Assert.Contains(settingsGrid.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "GitHubGhFastTopLatency"));
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
    public void Settings_use_a_category_drawer_and_include_project_about_information()
    {
        var document = LoadMainWindow();
        var settingsGrid = FindSectionRoot(document, "IsSettingsPage");
        var layout = settingsGrid.Elements(Avalonia + "Grid")
            .Single(grid => HasClass(grid, "cfp-settings-layout"));
        var drawer = layout.Elements(Avalonia + "Border")
            .Single(border => (string?)border.Attribute("Grid.Column") == "0" && HasClass(border, "cfp-rail"));
        var content = layout.Elements(Avalonia + "ScrollViewer")
            .Single(scrollViewer => (string?)scrollViewer.Attribute("Grid.Column") == "1");

        foreach (var section in new[] { "General", "Network", "Catalog", "Updates", "About" })
        {
            Assert.Contains(drawer.Descendants(Avalonia + "Button"), button =>
                HasBinding(button, "Command", "SelectSettingsSectionCommand")
                && (string?)button.Attribute("CommandParameter") == section
                && HasBinding(button, "Classes.active", $"Is{section}SettingsSection"));
            Assert.Contains(content.Descendants(Avalonia + "StackPanel"), panel =>
                ((string?)panel.Attribute("IsVisible"))?.Contains($"Is{section}SettingsSection", StringComparison.Ordinal) == true);
        }

        var about = content.Descendants(Avalonia + "StackPanel").Single(panel =>
            ((string?)panel.Attribute("IsVisible"))?.Contains("IsAboutSettingsSection", StringComparison.Ordinal) == true);
        Assert.Contains(about.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "AboutContributors"));
        Assert.Contains(about.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "OpenExternalUrl"
            && (string?)button.Attribute("Tag") == "https://github.com/wzxnb2333/Crystalfly"
            && HasBinding(button, "AutomationProperties.Name", "ProjectRepository"));
        Assert.Contains(about.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "LicenseName"));

        foreach (var heading in new[] { "AboutDesignReferences", "AboutOpenSourceComponents", "AboutModCatalogAndTranslations", "AboutSpeedrunComData" })
        {
            Assert.Contains(about.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", heading));
        }

        foreach (var sourceUrl in new[]
                 {
                     "https://github.com/Hex-Dragon/PCL2",
                     "https://github.com/TheMulhima/Lumafly",
                     "https://github.com/fifty-six/Scarab",
                     "https://github.com/AvaloniaUI/Avalonia",
                     "https://github.com/irihitech/Semi.Avalonia",
                     "https://github.com/irihitech/Ursa.Avalonia",
                     "https://github.com/SteamRE/SteamKit",
                     "https://github.com/dme-compunet/Lucide.Avalonia",
                     "https://github.com/Kryptos-FR/MarkView.Avalonia",
                     "https://github.com/hk-modding/modlinks",
                     "https://www.speedrun.com"
                 })
        {
            Assert.Contains(about.Descendants(Avalonia + "Button"), button =>
                (string?)button.Attribute("Click") == "OpenExternalUrl"
                && (string?)button.Attribute("Tag") == sourceUrl);
        }
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
        var modViewers = viewers.Where(viewer =>
            HasBinding(viewer, "Markdown", "SelectedModReadmeMarkdown")
            || HasBinding(viewer, "Markdown", "SelectedModReleaseNotesMarkdown")).ToArray();

        Assert.Equal(2, modViewers.Length);
        Assert.Contains(modViewers, viewer => HasBinding(viewer, "Markdown", "SelectedModReadmeMarkdown"));
        Assert.Contains(modViewers, viewer => HasBinding(viewer, "Markdown", "SelectedModReleaseNotesMarkdown"));
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
            (string?)button.Attribute("Click") == "ShareAndCopyPresetLink");
        Assert.DoesNotContain(presets.Descendants(Avalonia + "TextBlock"), text =>
            HasBinding(text, "Text", "LastPresetShareUrl"));

        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.PresetHandlers.cs"));
        Assert.Contains("private async void ConfirmApplyPreset", code, StringComparison.Ordinal);
        Assert.Contains("private async void ConfirmDeletePreset", code, StringComparison.Ordinal);
        Assert.Contains("private async void ImportPresetFile", code, StringComparison.Ordinal);
        Assert.Contains("private async void ExportSelectedPreset", code, StringComparison.Ordinal);
        Assert.Contains("private async void ShareAndCopyPresetLink", code, StringComparison.Ordinal);
        Assert.Contains("PresetApplySteps", code, StringComparison.Ordinal);
        Assert.Contains("FilePickerSaveOptions", code, StringComparison.Ordinal);
        Assert.Contains("Clipboard.SetTextAsync", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Mod_pack_workspace_uses_compact_master_detail_layout_and_subpage_markers()
    {
        var document = LoadMainWindow();
        var packs = document.Descendants(Avalonia + "StackPanel").Single(panel =>
            ((string?)panel.Attribute("IsVisible"))?.Contains("ConverterParameter=Presets", StringComparison.Ordinal) == true);

        Assert.True(HasClass(packs, "cfp-subpage"));
        Assert.Contains(packs.Descendants(Avalonia + "TextBlock"), text => HasBinding(text, "Text", "ModPacks"));
        Assert.Contains(packs.Descendants(Avalonia + "TextBox"), textBox => HasBinding(textBox, "Text", "ModPackSearchText"));
        Assert.Contains(packs.Descendants(Avalonia + "ListBox"), listBox => HasBinding(listBox, "ItemsSource", "VisibleModPacks"));
        Assert.Contains(packs.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ShowCreateModPackDialog");
        Assert.Contains(packs.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ImportPresetFile");
        Assert.Contains(packs.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ConfirmApplyPreset");
        Assert.Contains(packs.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ShowCopyModPackDialog");
        Assert.Contains(packs.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ShowImportSharedModPackDialog");
        Assert.Contains(packs.Descendants(Avalonia + "Button"), button =>
            HasBinding(button, "Command", "ToggleSelectedPresetEntriesCommand"));
        Assert.Contains(packs.Descendants(Avalonia + "ItemsControl"), items =>
            HasBinding(items, "ItemsSource", "VisibleSelectedModPackEntries"));

        // The downloads page keeps its section visibility markers on the section
        // scroll viewers (motion class rides the same element so the entrance
        // animation fires and the viewer drops out of hit testing when hidden).
        var downloadsPage = FindSectionRoot(document, "IsDownloadsPage");
        Assert.Contains(downloadsPage.Descendants(Avalonia + "ScrollViewer"), scroll =>
            HasClass(scroll, "cfp-subpage")
            && ((string?)scroll.Attribute("IsVisible"))?.Contains("IsGameVersionsDownloadSection", StringComparison.Ordinal) == true);
        Assert.Contains(downloadsPage.Descendants(Avalonia + "ScrollViewer"), scroll =>
            HasClass(scroll, "cfp-subpage")
            && ((string?)scroll.Attribute("IsVisible"))?.Contains("IsDownloadQueueSection", StringComparison.Ordinal) == true);
        var settingsPage = FindSectionRoot(document, "IsSettingsPage");
        Assert.Contains(settingsPage.Descendants(Avalonia + "StackPanel"), panel => HasClass(panel, "cfp-subpage"));

        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));
        Assert.Contains("DispatcherPriority.Render", code, StringComparison.Ordinal);
        var motionCode = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Services",
            "MotionCoordinator.cs"));
        Assert.Contains("entranceAnimationGeneration", motionCode, StringComparison.Ordinal);
        Assert.Contains("control.Classes.Contains(\"cfp-page\")", motionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("!control.GetVisualDescendants()", motionCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_page_exposes_confirmed_delete_action()
    {
        var document = LoadMainWindow();
        var snapshots = document.Descendants(Avalonia + "StackPanel").Single(panel =>
            ((string?)panel.Attribute("IsVisible"))?.Contains("ConverterParameter=Snapshots", StringComparison.Ordinal) == true);

        Assert.Contains(snapshots.Descendants(Avalonia + "Button"), button =>
            (string?)button.Attribute("Click") == "ConfirmDeleteSnapshot"
            && HasClass(button, "danger"));
    }

    private static XDocument LoadMainWindow() => XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "Crystalfly.App", "Views", "MainWindow.axaml"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static XElement FindDownloadQueueSection(XDocument document) =>
        FindSectionRoot(document, "IsDownloadsPage").Descendants()
            .Single(element =>
                ((string?)element.Attribute("IsVisible"))?.Contains(
                    "IsDownloadQueueSection",
                    StringComparison.Ordinal) == true);

    private static XElement FindSectionRoot(XDocument document, string visibilityProperty) => document.Descendants(Avalonia + "Grid").Single(element => ((string?)element.Attribute("IsVisible"))?.Contains(visibilityProperty, StringComparison.Ordinal) == true);
    private static bool HasClass(XElement element, string className) => ((string?)element.Attribute("Classes"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className) == true;
    private static bool HasBinding(XElement element, string attributeName, string path) => ((string?)element.Attribute(attributeName))?.Contains(path, StringComparison.Ordinal) == true;
    private static bool IsVersionsNavigationButton(XElement button) => HasBinding(button, "Command", "SelectPageCommand") && (string?)button.Attribute("CommandParameter") == "Versions";
}
