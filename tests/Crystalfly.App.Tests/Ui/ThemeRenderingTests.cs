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
    public void Application_registers_Semi_and_Ursa_themes_without_Fluent()
    {
        var styleTypes = Application.Current!.Styles
            .Select(style => style.GetType().Name)
            .ToArray();

        Assert.Contains("SemiTheme", styleTypes);
        Assert.Contains("UrsaSemiTheme", styleTypes);
        Assert.DoesNotContain("FluentTheme", styleTypes);
    }

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
    public void Dark_danger_hover_keeps_white_text_readable()
    {
        Assert.True(Application.Current!.TryGetResource(
            "CfDangerHoverBrush",
            ThemeVariant.Dark,
            out var background));
        Assert.True(Application.Current.TryGetResource(
            "CfOnDangerBrush",
            ThemeVariant.Dark,
            out var foreground));

        var ratio = ContrastRatio(ColorOf((IBrush)foreground!), ColorOf((IBrush)background!));

        Assert.True(ratio >= 4.5, $"Dark danger hover contrast was {ratio:F2}:1.");
    }

    [AvaloniaFact]
    public async Task Main_window_binds_global_loading_and_instance_skeleton()
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
            var skeleton = Assert.Single(window.GetLogicalDescendants().OfType<Skeleton>());
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
            Assert.True(skeleton.IsLoading);
            Assert.True(skeleton.IsActive);
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

    [AvaloniaFact]
    public async Task Settings_catalog_error_uses_error_foreground()
    {
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationDataRoot) { CurrentPage = "Settings" };
        SetOfficialCatalogResult(viewModel, new(
            Crystalfly.Core.Catalog.OfficialCatalogLoadStatus.Failed,
            new Crystalfly.Core.Models.GameCatalog(),
            null,
            0,
            "catalog failed"));
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var error = Assert.Single(
                window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "catalog failed" && text.IsEffectivelyVisible);
            Assert.True(Application.Current!.TryGetResource(
                "CfErrorBrush",
                Application.Current.ActualThemeVariant,
                out var errorBrush));
            Assert.Equal(ColorOf((IBrush)errorBrush!), ColorOf(error.Foreground));
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
    public async Task Standard_confirmation_overlay_uses_primary_confirm_button()
    {
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationDataRoot);
        var result = window.ShowConfirmationAsync("Restore", "Restore snapshot?", "Snapshot", viewModel);
        Dispatcher.UIThread.RunJobs();

        try
        {
            var dialog = Assert.Single(window.GetVisualDescendants().OfType<CustomDialogControl>());
            var confirm = dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["Confirm"]);
            Assert.Contains("cfp-primary", confirm.Classes);

            confirm.Command!.Execute(confirm.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.True(result.IsCompleted);
            Assert.True(await result.WaitAsync(TimeSpan.FromSeconds(1)));
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
    public async Task Main_window_uses_two_Ursa_path_pickers_and_a_quick_mod_import_action()
    {
        var applicationDataRoot = Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N"));
        var selectedRoot = Path.Combine(applicationDataRoot, "versions");
        Directory.CreateDirectory(selectedRoot);
        var viewModel = new MainViewModel(applicationDataRoot)
        {
            CurrentPage = "Settings"
        };
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var pickers = window.GetLogicalDescendants().OfType<PathPicker>().ToArray();
            Assert.Equal(2, pickers.Length);
            var folder = Assert.Single(pickers, picker => picker.UsePickerType == UsePickerTypes.OpenFolder);
            var file = Assert.Single(pickers, picker => picker.UsePickerType == UsePickerTypes.OpenFile);
            Assert.Same(viewModel.ApplyVersionRootCommand, folder.Command);
            Assert.Contains("*.json", file.FileFilter, StringComparison.Ordinal);
            var modImport = Assert.Single(window.GetLogicalDescendants().OfType<Button>(), button =>
                AutomationProperties.GetName(button) == viewModel.Loc["ImportLocalMod"]);
            Assert.Contains("cfp-quick-action", modImport.Classes);
            Assert.All(pickers, picker => Assert.True(picker.IsOmitCommandOnCancel));
            Assert.All(pickers, picker =>
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(picker))));

            folder.SelectedPathsText = selectedRoot;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(selectedRoot, viewModel.VersionRoot);

            folder.Command!.Execute(Array.Empty<Avalonia.Platform.Storage.IStorageItem>());
            for (var attempt = 0; attempt < 100 && viewModel.ApplyVersionRootCommand.IsRunning; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.False(viewModel.ApplyVersionRootCommand.IsRunning);
            Assert.Null(viewModel.ErrorMessage);
            var settings = await CrystalflySettingsStore.LoadAsync(
                Path.Combine(applicationDataRoot, "settings.json"));
            Assert.Equal(Path.GetFullPath(selectedRoot), settings.VersionRoot);
        }
        finally
        {
            window.DataContext = null;
            window.Close();
            await viewModel.DisposeAsync();
            if (Directory.Exists(applicationDataRoot))
            {
                Directory.Delete(applicationDataRoot, recursive: true);
            }
        }
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
            Assert.Equal(13, body.FontSize);
            Assert.Equal(20, pageTitle.FontSize);
            Assert.Equal(16, sectionTitle.FontSize);
            Assert.Equal(12, meta.FontSize);
            Assert.Equal(11, caption.FontSize);
            Assert.Equal(40, primaryButton.MinHeight);
            Assert.Equal(36, secondaryButton.MinHeight);
            Assert.Equal(36, iconButton.Width);
            Assert.Equal(36, iconButton.Height);
            Assert.Equal(36, input.MinHeight);
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
