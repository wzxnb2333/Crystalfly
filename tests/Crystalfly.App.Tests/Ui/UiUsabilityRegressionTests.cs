using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
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
    public async Task Long_error_is_dismissible_top_notification_and_leaves_workspace_visible()
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
            var card = Assert.Single(window.GetVisualDescendants().OfType<ToastCard>());
            Assert.Equal(NotificationType.Error, card.NotificationType);
            Assert.True(card.Bounds.Width <= window.ClientSize.Width,
                $"Error notification is {card.Bounds.Width}px wide in a {window.ClientSize.Width}px window.");
            var dismiss = Assert.Single(card.GetVisualDescendants().OfType<Button>(),
                button => button.Name == "PART_CloseButton" && button.IsEffectivelyVisible);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(dismiss)));
            dismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var attempt = 0; attempt < 100 && window.GetVisualDescendants().OfType<ToastCard>().Any(); attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Assert.Empty(window.GetVisualDescendants().OfType<ToastCard>());
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(),
                button => button.IsEffectivelyVisible && button.Classes.Contains("cfp-nav"));
        }
        finally { Close(window); }
    }

    [AvaloniaFact]
    public async Task Error_notification_centers_its_message_vertically()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        await using var vm = new MainViewModel(root);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = vm;
        try
        {
            vm.ErrorMessage = "No Hollow Knight installation was found in that directory.";
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var card = Assert.Single(window.GetVisualDescendants().OfType<ToastCard>());
            // 入场动画作用在模板根节点的 RenderTransform 上，TranslatePoint 会把它算进去，
            // 因此以卡片内容边框为基准测量，避免入场位移影响断言。
            var rootBorder = Assert.Single(card.GetVisualDescendants().OfType<Border>(),
                border => border.Name == "PART_RootBorder");
            var message = Assert.Single(rootBorder.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == vm.ErrorMessage);
            var origin = Assert.IsType<Point>(message.TranslatePoint(default, rootBorder));
            var messageCenter = origin.Y + message.Bounds.Height / 2;
            var cardCenter = rootBorder.Bounds.Height / 2;
            Assert.InRange(
                System.Math.Abs(messageCenter - cardCenter),
                0,
                2);
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
