using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Configuration;
using Lucide.Avalonia;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class UiUsabilityRegressionTests
{
    [AvaloniaTheory]
    [InlineData("confirm")]
    [InlineData("steam-risk")]
    [InlineData("steam-delete")]
    public async Task Long_confirmation_content_keeps_actions_inside_minimum_window(string kind)
    {
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        try
        {
            var options = new OverlayDialogOptions
            {
                TopLevelHashCode = window.GetHashCode(), CanLightDismiss = false,
                CanDragMove = false, IsCloseButtonVisible = true, CanResize = false
            };
            var message = string.Join("\n", Enumerable.Repeat("This operation changes the selected game files.", 50));
            var target = "C:\\Games\\" + new string('x', 180);
            var choices = new ThreeChoiceDialogViewModel("Game directory", message, target,
                "Remove managed files only", "Remove the entire directory", "Cancel", true);
            if (kind == "confirm")
                _ = OverlayDialog.ShowCustomAsync<ConfirmationDialogView, ConfirmationDialogViewModel, bool>(
                    new("Confirmation", message, target, "Confirm", "Cancel", true, true), MainWindow.OverlayHostId, options);
            else if (kind == "steam-risk")
                _ = OverlayDialog.ShowCustomAsync<SteamDirectoryRiskDialogView, ThreeChoiceDialogViewModel, ThreeChoiceDialogResult>(choices, MainWindow.OverlayHostId, options);
            else
                _ = OverlayDialog.ShowCustomAsync<SteamInstanceDeletionDialogView, ThreeChoiceDialogViewModel, ThreeChoiceDialogResult>(choices, MainWindow.OverlayHostId, options);
            CustomDialogControl? dialog = null;
            for (var i = 0; i < 50 && dialog is null; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialog = window.GetVisualDescendants().OfType<CustomDialogControl>().SingleOrDefault();
            }
            Assert.NotNull(dialog);
            var actions = dialog.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Command is not null).ToArray();
            Assert.Equal(kind == "confirm" ? 2 : 3, actions.Length);
            foreach (var button in actions)
            {
                var origin = Assert.IsType<Point>(button.TranslatePoint(default, window));
                Assert.InRange(origin.Y, 0, window.ClientSize.Height - button.Bounds.Height + 1);
                Assert.InRange(origin.X, 0, window.ClientSize.Width - button.Bounds.Width + 1);
            }
            Assert.Contains(dialog.GetVisualDescendants().OfType<ScrollViewer>(), s => s.Extent.Height > s.Viewport.Height);
        }
        finally { Close(window); }
    }

    [AvaloniaFact]
    public async Task Long_error_is_scrollable_dismissible_and_leaves_workspace_visible()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        await using var vm = new MainViewModel(root);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = vm;
        try
        {
            vm.ErrorMessage = string.Join("\n", Enumerable.Repeat("A recoverable error with details", 80));
            Dispatcher.UIThread.RunJobs();
            var error = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("cfp-error") && b.IsEffectivelyVisible);
            Assert.True(error.Bounds.Height <= 150, $"Error banner consumed {error.Bounds.Height}px.");
            var dismiss = Assert.Single(error.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("cfp-icon"));
            Assert.Contains(error.GetVisualDescendants().OfType<ScrollViewer>(), s => s.Extent.Height > s.Viewport.Height);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(dismiss)));
            dismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
        }
        finally { Close(window); }
    }

    [AvaloniaFact]
    public async Task Community_rail_reflows_when_resizing_without_hiding_links()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        await using var vm = new MainViewModel(root);
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = vm;
        try
        {
            foreach (var width in new[] { 1280, 900, 1100, 1920 })
            {
                window.Width = width;
                Dispatcher.UIThread.RunJobs();
                var rail = window.FindControl<Border>("CommunityRail")!;
                Assert.Equal(width < 1180 ? 1 : 2, Grid.GetColumn(rail));
                Assert.Equal(width < 1180 ? 1 : 0, Grid.GetRow(rail));
                Assert.True(rail.IsEffectivelyVisible);
                var communityButtons = rail.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("cfp-community-link")).ToArray();
                Assert.Equal(6, communityButtons.Length);
                Assert.All(communityButtons, b => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))));
                foreach (var button in communityButtons)
                {
                    var item = Assert.IsType<SpeedrunCommunityLinkItemViewModel>(button.DataContext);
                    var icon = Assert.Single(button.GetVisualDescendants().OfType<LucideIcon>());
                    Assert.Equal(item.Icon, icon.Kind);
                }
                var first = communityButtons[0];
                Assert.True(first.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                Assert.True(first.IsKeyboardFocusWithin);
                Assert.Equal(2, first.BorderThickness.Left);
                Assert.NotEqual(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(first.BorderBrush).Color);
            }
        }
        finally { Close(window); }
    }

    private static void Close(MainWindow window)
    {
        foreach (var dialog in window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray()) dialog.Close();
        typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.SimplifiedChinese)]
    public async Task Community_edit_form_keeps_invalid_input_open_and_saves_with_enter(UiLanguage language)
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var vm = new MainViewModel(root);
        vm.Loc.Apply(language);
        vm.SpeedrunCommunityLinks.Load([new() { Id = "custom", Name = "Original", Url = "https://example.com/old" }]);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = vm;
        try
        {
            Dispatcher.UIThread.RunJobs();
            var edit = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                b => AutomationProperties.GetName(b) == vm.Loc["SpeedrunCommunityEdit"]);
            edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 50 && !window.GetVisualDescendants().OfType<CommunityLinkDialogView>().Any(); i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            var view = Assert.Single(window.GetVisualDescendants().OfType<CommunityLinkDialogView>());
            var dialog = Assert.IsType<CommunityLinkDialogViewModel>(view.DataContext);
            var name = Assert.Single(view.GetVisualDescendants().OfType<TextBox>(),
                box => AutomationProperties.GetName(box) == vm.Loc["SpeedrunCommunityName"]);
            var url = Assert.Single(view.GetVisualDescendants().OfType<TextBox>(),
                box => AutomationProperties.GetName(box) == vm.Loc["SpeedrunCommunityUrl"]);
            Assert.Equal("Original", name.Text);
            name.Text = "Updated guide";
            url.Text = "http://example.com/unsafe";
            url.Focus();
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(vm.Loc["SpeedrunCommunityInvalidUrl"], dialog.ErrorMessage);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == dialog.ErrorMessage);
            Assert.Equal("Original", Assert.Single(vm.SpeedrunCommunityLinks.CustomLinks).Name);
            url.Text = "https://example.com/help#practice";
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            for (var i = 0; i < 100 && vm.SpeedrunCommunityLinks.CustomLinks.Single().Name != "Updated guide"; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            var updated = Assert.Single(vm.SpeedrunCommunityLinks.CustomLinks);
            Assert.Equal("Updated guide", updated.Name);
            Assert.Equal("https://example.com/help#practice", updated.Url);
        }
        finally
        {
            Close(window);
            await vm.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Failed_community_save_can_retry_and_persists_after_reopening_settings()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(path);
        try
        {
            await using var vm = new MainViewModel(root);
            var link = new SpeedrunCommunityLinkDefinition { Id = "custom", Name = "Guide", Url = "https://example.com/help#practice" };
            var error = await Record.ExceptionAsync(() => vm.SpeedrunCommunityLinks.AddAsync(link));
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "Expected the blocked settings write to fail.");
            Assert.Empty(vm.SpeedrunCommunityLinks.CustomLinks);
            Directory.Delete(path);
            Assert.True(await vm.SpeedrunCommunityLinks.AddAsync(link));
            var reloaded = await CrystalflySettingsStore.LoadAsync(path);
            Assert.Equal(link, Assert.Single(reloaded.SpeedrunCommunityLinks));
            Assert.True(await vm.SpeedrunCommunityLinks.UpdateAsync(link with { Name = "Updated guide" }));
            reloaded = await CrystalflySettingsStore.LoadAsync(path);
            Assert.Equal("Updated guide", Assert.Single(reloaded.SpeedrunCommunityLinks).Name);
            Assert.True(await vm.SpeedrunCommunityLinks.RemoveAsync(link.Id));
            Assert.Empty((await CrystalflySettingsStore.LoadAsync(path)).SpeedrunCommunityLinks);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
