using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using System.Reflection;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class ThemeRenderingTests
{

    [AvaloniaFact]
    public void Main_window_uses_custom_chrome_with_native_resize_border()
    {
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        try
        {
            Assert.Equal(WindowDecorations.BorderOnly, window.WindowDecorations);
            Assert.True(window.CanResize);
            var minimize = Assert.IsType<Button>(FindChromeButton(window, "cfp-window-minimize"));
            var maximize = Assert.IsType<Button>(FindChromeButton(window, "cfp-window-maximize"));
            var close = Assert.IsType<Button>(FindChromeButton(window, "cfp-window-close"));

            maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(WindowState.Maximized, window.WindowState);
            maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(WindowState.Normal, window.WindowState);
            minimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(WindowState.Minimized, window.WindowState);
            window.WindowState = WindowState.Normal;
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void External_protocol_message_restores_a_minimized_window_before_confirmation()
    {
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        try
        {
            window.WindowState = WindowState.Minimized;

            window.ActivateForExternalCommand();

            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Window_stops_accepting_external_commands_when_shutdown_begins()
    {
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.ResumeExternalCommands();
        Assert.True(window.IsExternalCommandReady);

        window.Close();
        window.ResumeExternalCommands();

        Assert.False(window.IsExternalCommandReady);
    }

    [AvaloniaFact]
    public async Task Applying_language_refreshes_Semi_and_Ursa_locale_resources()
    {
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationDataRoot);
        try
        {
            InvokeApplyLanguage(viewModel, UiLanguage.English);
            Assert.Equal("en-US", viewModel.Loc.Culture.Name);
            Assert.Equal("Copy", ResourceString("STRING_MENU_COPY"));
            Assert.Equal("Yes", ResourceString("STRING_MENU_DIALOG_YES"));

            InvokeApplyLanguage(viewModel, UiLanguage.SimplifiedChinese);
            Assert.Equal("zh-CN", viewModel.Loc.Culture.Name);
            Assert.Equal("复制", ResourceString("STRING_MENU_COPY"));
            Assert.Equal("是", ResourceString("STRING_MENU_DIALOG_YES"));
        }
        finally
        {
            InvokeApplyLanguage(viewModel, UiLanguage.SimplifiedChinese);
            await viewModel.DisposeAsync();
            if (Directory.Exists(applicationDataRoot))
            {
                Directory.Delete(applicationDataRoot, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Dangerous_confirmation_overlay_disables_confirm_and_cancel_returns_false()
    {
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationDataRoot);
        var result = window.ShowConfirmationAsync(
            "Remove mod",
            "This cannot be undone.",
            "Sample Mod",
            viewModel,
            canConfirm: false,
            isDangerous: true);
        Dispatcher.UIThread.RunJobs();

        try
        {
            var dialog = Assert.Single(window.GetVisualDescendants().OfType<CustomDialogControl>());
            var confirm = dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["Confirm"]);
            var cancel = dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["Cancel"]);

            Assert.Contains("cfp-danger", confirm.Classes);
            Assert.False(confirm.IsEnabled);

            window.MouseDown(new Point(4, 4), MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(new Point(4, 4), MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(result.IsCompleted);
            Assert.Same(dialog, Assert.Single(
                window.GetVisualDescendants().OfType<CustomDialogControl>()));

            Assert.NotNull(cancel.Command);
            cancel.Command.Execute(cancel.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.True(result.IsCompleted);
            Assert.False(await result.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Empty(window.GetVisualDescendants().OfType<CustomDialogControl>());
        }
        finally
        {
            window.Close();
            await viewModel.DisposeAsync();
            if (Directory.Exists(applicationDataRoot))
            {
                Directory.Delete(applicationDataRoot, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Confirmation_overlay_targets_the_window_that_opened_it()
    {
        var firstWindow = new MainWindow { Width = 900, Height = 600 };
        var secondWindow = new MainWindow { Width = 900, Height = 600 };
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(applicationDataRoot);
        firstWindow.Show();
        secondWindow.Show();
        var result = secondWindow.ShowConfirmationAsync(
            "Restore",
            "Restore snapshot?",
            "Snapshot",
            viewModel);
        Dispatcher.UIThread.RunJobs();

        try
        {
            Assert.Empty(firstWindow.GetVisualDescendants().OfType<CustomDialogControl>());
            var dialog = Assert.Single(secondWindow.GetVisualDescendants().OfType<CustomDialogControl>());
            var cancel = dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["Cancel"]);
            cancel.Command!.Execute(cancel.CommandParameter);
            Assert.False(await result.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            if (firstWindow.IsVisible)
            {
                await CloseWindowAsync(firstWindow);
            }
            if (secondWindow.IsVisible)
            {
                await CloseWindowAsync(secondWindow);
            }
            if (Directory.Exists(applicationDataRoot))
            {
                Directory.Delete(applicationDataRoot, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Main_window_binds_global_loading_without_replacing_instance_status_card()
    {
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationDataRoot);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var loading = Assert.IsType<LoadingContainer>(
                window.FindControl<Control>("GlobalLoadingHost"));
            var loadingMessage = Assert.IsType<TextBlock>(
                window.FindControl<Control>("GlobalLoadingMessage"));
            Assert.Empty(window.GetLogicalDescendants().OfType<Skeleton>());
            Assert.Equal(HorizontalAlignment.Stretch, loading.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Stretch, loading.VerticalContentAlignment);

            var root = Assert.IsType<Grid>(window.Content);
            var overlay = Assert.Single(root.Children.OfType<OverlayDialogHost>());
            Assert.Same(overlay, root.Children[^1]);
            Assert.DoesNotContain(overlay, loading.GetLogicalDescendants());

            viewModel.StatusMessage = "Working";
            viewModel.IsBusy = true;
            viewModel.IsLoadingInstanceDetails = true;
            viewModel.DownloadProgress = 0.4;
            Dispatcher.UIThread.RunJobs();

            Assert.True(loading.IsLoading);
            Assert.Equal("Working", loadingMessage.Text);
            var downloadProgress = Assert.IsType<ProgressBar>(
                window.FindControl<ProgressBar>("SteamDownloadProgress"));
            Assert.Equal(0.4, downloadProgress.Value);
        }
        finally
        {
            viewModel.IsBusy = false;
            await CloseWindowAsync(window);
            await viewModel.DisposeAsync();
            if (Directory.Exists(applicationDataRoot))
            {
                Directory.Delete(applicationDataRoot, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Main_window_shows_each_new_non_empty_error_and_keeps_inline_state()
    {
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationDataRoot);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            viewModel.ErrorMessage = "";
            viewModel.ErrorMessage = "   ";
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(window.GetVisualDescendants().OfType<ToastCard>());

            viewModel.ErrorMessage = "first error";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(
                NotificationType.Error,
                Assert.Single(window.GetVisualDescendants().OfType<ToastCard>()).NotificationType);

            viewModel.ErrorMessage = "second error";
            Dispatcher.UIThread.RunJobs();
            var cards = window.GetVisualDescendants().OfType<ToastCard>().ToArray();
            Assert.Equal(2, cards.Length);
            Assert.All(cards, card => Assert.Equal(NotificationType.Error, card.NotificationType));
            Assert.Equal("second error", viewModel.ErrorMessage);
            Assert.Contains(
                window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "second error" && text.IsEffectivelyVisible);
        }
        finally
        {
            viewModel.IsBusy = false;
            await CloseWindowAsync(window);
            await viewModel.DisposeAsync();
            if (Directory.Exists(applicationDataRoot))
            {
                Directory.Delete(applicationDataRoot, recursive: true);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese, false)]
    [InlineData(UiLanguage.SimplifiedChinese, true)]
    [InlineData(UiLanguage.English, false)]
    [InlineData(UiLanguage.English, true)]
    public async Task Toast_close_button_is_localized_and_dismisses_only_the_notification(
        UiLanguage language, bool success)
    {
        var applicationDataRoot = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationDataRoot);
        viewModel.Loc.Apply(language);
        viewModel.SelectedMotionPreference = new(UiMotionPreference.Off, "Off");
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        try
        {
            if (success)
            {
                Assert.True(TryInvokeToastRequested(viewModel, "completed operation"));
            }
            else
            {
                viewModel.ErrorMessage = "operation error";
            }
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var card = Assert.Single(window.GetVisualDescendants().OfType<ToastCard>());
            var close = Assert.Single(card.GetVisualDescendants().OfType<Button>(), button =>
                button.Name == "PART_CloseButton");
            Assert.Equal(viewModel.Loc["WindowClose"],
                new Avalonia.Automation.Peers.ButtonAutomationPeer(close).GetName());
            Assert.Equal(viewModel.Loc["WindowClose"], ToolTip.GetTip(close));
            viewModel.Loc.Apply(language == UiLanguage.English ? UiLanguage.SimplifiedChinese : UiLanguage.English);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(viewModel.Loc["WindowClose"],
                new Avalonia.Automation.Peers.ButtonAutomationPeer(close).GetName());
            Assert.Equal(viewModel.Loc["WindowClose"], ToolTip.GetTip(close));
            Point center = default;
            Control? hit = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                center = Assert.IsType<Point>(close.TranslatePoint(
                    new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window));
                window.MouseMove(center, RawInputModifiers.None);
                hit = window.InputHitTest(center) as Control;
                if (hit == close || hit?.GetVisualAncestors().Contains(close) == true)
                {
                    break;
                }
                await Task.Delay(10);
            }
            Assert.True(hit == close || hit?.GetVisualAncestors().Contains(close) == true,
                $"The notification close button must be hit-testable at {center}; hit {hit?.GetType().Name}.");
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            for (var attempt = 0; attempt < 100 && window.GetVisualDescendants().Contains(card); attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            }
            Assert.DoesNotContain(card, window.GetVisualDescendants());
            Assert.True(window.IsVisible);
            Assert.Equal(success ? null : "operation error", viewModel.ErrorMessage);
        }
        finally
        {
            await CloseWindowAsync(window);
            await viewModel.DisposeAsync();
            if (Directory.Exists(applicationDataRoot))
            {
                Directory.Delete(applicationDataRoot, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Main_window_toast_manager_tracks_DataContext_and_uninstalls_on_close()
    {
        var firstRoot = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var secondRoot = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var first = new MainViewModel(firstRoot);
        var second = new MainViewModel(secondRoot);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = first;
        Dispatcher.UIThread.RunJobs();

        try
        {
            Assert.True(WindowToastManager.TryGetToastManager(window, out var manager));
            var toastManager = Assert.IsType<WindowToastManager>(manager);
            Assert.Equal(3, toastManager.MaxItems);

            Assert.True(TryInvokeToastRequested(first, "first request"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(
                NotificationType.Success,
                Assert.Single(window.GetVisualDescendants().OfType<ToastCard>()).NotificationType);

            Assert.True(TryInvokeToastRequested(first, "queued stale request"));
            window.DataContext = second;
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "queued stale request");

            Assert.False(TryInvokeToastRequested(first, "stale request"));
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "stale request");

            Assert.True(TryInvokeToastRequested(second, "current request"));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(
                window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "current request" && text.IsEffectivelyVisible);

            toastManager.CloseAll();
            Dispatcher.UIThread.RunJobs();
            await Task.Run(() => second.ErrorMessage = "background error");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(
                window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "background error" && text.IsEffectivelyVisible);

            await CloseWindowAsync(window);

            Assert.Null(toastManager.Parent);
            Assert.False(WindowToastManager.TryGetToastManager(window, out _));
        }
        finally
        {
            if (window.IsVisible)
            {
                await CloseWindowAsync(window);
            }
            await first.DisposeAsync();
            await second.DisposeAsync();
            foreach (var root in new[] { firstRoot, secondRoot })
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(false, "cfp-primary")]
    [InlineData(false, "cfp-secondary")]
    [InlineData(true, "cfp-accent")]
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

    private static Button? FindChromeButton(MainWindow window, string className) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.Classes.Contains(className));

    private static async Task CloseWindowAsync(MainWindow window)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object? sender, EventArgs eventArgs) => closed.TrySetResult();
        window.Closed += OnClosed;
        try
        {
            window.Close();
            for (var attempt = 0; attempt < 200 && !closed.Task.IsCompleted; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            var disposeTask = typeof(MainWindow).GetField(
                "disposeBeforeCloseTask",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window) as Task;
            Assert.True(
                closed.Task.IsCompleted,
                $"Window did not close; dispose task status: {disposeTask?.Status}, exception: {disposeTask?.Exception}");
            await closed.Task;
        }
        finally
        {
            window.Closed -= OnClosed;
        }
    }

    private static void InvokeApplyLanguage(MainViewModel viewModel, UiLanguage language)
    {
        var method = typeof(MainViewModel).GetMethod(
            "ApplyLanguage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, [language]);
    }

    private static bool TryInvokeToastRequested(MainViewModel viewModel, string message)
    {
        var field = typeof(MainViewModel).GetField(
            "ToastRequested",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(ViewModelBase).GetField(
                "ToastRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        if (field.GetValue(viewModel) is not Action<string> handler)
        {
            return false;
        }
        handler(message);
        return true;
    }

    private static string ResourceString(string key)
    {
        Assert.True(Application.Current!.TryGetResource(key, null, out var value));
        return Assert.IsType<string>(value);
    }

    private static void SetOfficialCatalogResult(
        MainViewModel viewModel,
        Crystalfly.Core.Catalog.OfficialCatalogLoadResult result)
    {
        var field = typeof(MainViewModel).GetField(
            "officialCatalogResult",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, result);
    }

    private static Button DependencyNode(string state)
    {
        var node = new Button { Content = state };
        node.Classes.Add("cfp-dependency-node");
        node.Classes.Add(state);
        return node;
    }

    private static Color ResourceColor(string key, ThemeVariant variant)
    {
        Assert.True(Application.Current!.TryGetResource(key, variant, out var value));
        return ColorOf((IBrush)value!);
    }

    private static Color ColorOf(IBrush? brush)
        => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

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
