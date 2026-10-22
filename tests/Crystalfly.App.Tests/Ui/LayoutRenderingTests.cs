using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class LayoutRenderingTests
{
    [AvaloniaFact]
    public async Task Application_update_dialog_fits_the_minimum_window()
    {
        var localization = new LocalizationViewModel();
        var dialogViewModel = new ApplicationUpdateDialogViewModel(
            localization,
            "1.1.4",
            "# Release notes\n\nA concise summary of the available update.",
            (_, _) => Task.FromResult(false));
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            _ = OverlayDialog.ShowCustomAsync<
                ApplicationUpdateDialogView,
                ApplicationUpdateDialogViewModel,
                ApplicationUpdateDialogResult>(
                dialogViewModel,
                MainWindow.OverlayHostId,
                new OverlayDialogOptions
                {
                    TopLevelHashCode = window.GetHashCode(),
                    CanLightDismiss = false,
                    CanDragMove = false,
                    IsCloseButtonVisible = true,
                    CanResize = false
                });

            CustomDialogControl[] dialogs = [];
            for (var attempt = 0; attempt < 50 && dialogs.Length == 0; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialogs = window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray();
            }

            var dialog = Assert.Single(dialogs);
            var view = Assert.Single(dialog.GetVisualDescendants().OfType<ApplicationUpdateDialogView>());
            Assert.True(view.Bounds.Height <= 520.5, $"Dialog content was {view.Bounds.Height:F1}px tall.");
            Assert.Contains(dialog.GetVisualDescendants().OfType<Button>(), button =>
                Equals(button.Content, localization["UpdateNow"]));
            dialog.Close();
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public async Task Onboarding_dialog_keeps_actions_visible_at_minimum_window_size()
    {
        var tasks = Enumerable.Range(1, 9)
            .Select(index => new OnboardingTaskItemViewModel(
                index.ToString(),
                $"Task {index}",
                "A long localized description that must wrap without pushing the dialog actions outside the window.",
                index < 4 ? "Done" : "Next",
                "Open",
                "Open",
                index < 4 ? OnboardingTaskState.Done : OnboardingTaskState.Current))
            .ToArray();
        var dialogViewModel = new OnboardingDialogViewModel(key => key, tasks);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            _ = OverlayDialog.ShowCustomAsync<OnboardingDialogView, OnboardingDialogViewModel, bool>(
                dialogViewModel,
                MainWindow.OverlayHostId,
                new OverlayDialogOptions
                {
                    TopLevelHashCode = window.GetHashCode(),
                    CanLightDismiss = false,
                    CanDragMove = false,
                    IsCloseButtonVisible = true,
                    CanResize = false
                });

            CustomDialogControl[] dialogs = [];
            for (var attempt = 0; attempt < 50 && dialogs.Length == 0; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialogs = window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray();
            }

            var dialog = Assert.Single(dialogs);
            var view = Assert.Single(dialog.GetVisualDescendants().OfType<OnboardingDialogView>());
            var complete = dialog.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, "OnboardingFinish"));
            var completeOrigin = Assert.IsType<Point>(complete.TranslatePoint(default, window));

            Assert.True(view.Bounds.Height <= 520.5, $"Dialog content was {view.Bounds.Height:F1}px tall.");
            Assert.True(completeOrigin.Y + complete.Bounds.Height <= window.ClientSize.Height,
                $"Complete action ended at {completeOrigin.Y + complete.Bounds.Height:F1}px in a {window.ClientSize.Height:F1}px window.");
            dialog.Close();
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public async Task Installed_mod_list_uses_remaining_height_and_keeps_bulk_actions_visible_at_900x600()
    {
        var applicationData = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(applicationData)
        {
            CurrentPage = "Manage",
            CurrentManageTab = "Mods"
        };
        var item = new InstalledModItemViewModel(
            new InstalledModReceipt
            {
                Id = "debugmod",
                Name = "Debug Mod",
                Version = "1.0.0",
                LoaderId = "modding-api",
                InstallRoot = "Mods/DebugMod",
                Enabled = true,
                IsLocal = true
            },
            null,
            static () => { })
        {
            IsSelected = true
        };
        viewModel.ModManagement.InstalledMods.Add(item);
        viewModel.ModManagement.VisibleInstalledMods.Add(item);

        var window = new MainWindow { Width = 900, Height = 600, DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var list = window.GetVisualDescendants().OfType<ListBox>()
                .Single(control => control.Classes.Contains("cfp-installed-mod-list") && control.IsEffectivelyVisible);
            var bulkBar = window.GetVisualDescendants().OfType<Border>()
                .Single(control => control.Classes.Contains("cfp-mod-bulk-bar") && control.IsEffectivelyVisible);
            var listOrigin = Assert.IsType<Point>(list.TranslatePoint(default, window));
            var bulkOrigin = Assert.IsType<Point>(bulkBar.TranslatePoint(default, window));

            Assert.True(list.Bounds.Height >= 200, $"Installed Mod list height was only {list.Bounds.Height:F1}px.");
            Assert.True(listOrigin.Y + list.Bounds.Height <= bulkOrigin.Y + 1,
                "The fixed bulk action bar must follow the bounded Mod list.");
            Assert.True(bulkOrigin.Y + bulkBar.Bounds.Height <= window.ClientSize.Height + 1,
                "The bulk action bar must remain inside the 900x600 viewport.");
        }
        finally
        {
            CloseImmediately(window);
            await viewModel.DisposeAsync();
            if (Directory.Exists(applicationData))
            {
                Directory.Delete(applicationData, recursive: true);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData("Settings")]
    [InlineData("Downloads")]
    public void Visible_inputs_and_selectors_have_accessible_names(string page)
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = page
        };
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var controls = window.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && control is TextBox or ComboBox)
                .ToArray();
            Assert.NotEmpty(controls);
            Assert.All(controls, control =>
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                    $"{page}: {control.GetType().Name} has no accessible name."));
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Top_navigation_follows_visual_keyboard_order()
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")));
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var buttons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible
                    && button.Classes.Contains("cfp-nav")
                    && button.GetVisualAncestors().OfType<Border>().Any(border => border.Classes.Contains("cfp-topbar")))
                .ToArray();
            Assert.Equal(4, buttons.Length);
            Assert.True(buttons[0].Focus(NavigationMethod.Tab, KeyModifiers.None));

            foreach (var expected in buttons.Skip(1))
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(expected, window.FocusManager?.GetFocusedElement());
            }
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Button_labels_and_icons_are_optically_centered_together()
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = "Downloads"
        };
        var window = new MainWindow { Width = 1280, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var iconAndTextButtons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible)
                .Where(button => !button.Classes.Contains("cfp-local-nav"))
                .Select(button => new
                {
                    Button = button,
                    Icon = button.GetVisualDescendants()
                        .OfType<Control>()
                        .FirstOrDefault(control => control.IsEffectivelyVisible && control.GetType().Name == "LucideIcon"),
                    Text = button.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .FirstOrDefault(text => text.IsEffectivelyVisible)
                })
                .Where(item => item.Icon is not null && item.Text is not null)
                .ToArray();
            Assert.NotEmpty(iconAndTextButtons);

            foreach (var item in iconAndTextButtons)
            {
                var icon = Assert.IsAssignableFrom<Control>(item.Icon);
                var text = Assert.IsAssignableFrom<TextBlock>(item.Text);
                var iconOrigin = Assert.IsType<Point>(icon.TranslatePoint(default, item.Button));
                var textOrigin = Assert.IsType<Point>(text.TranslatePoint(default, item.Button));
                var iconCenter = iconOrigin.Y + icon.Bounds.Height / 2;
                var textCenter = textOrigin.Y + text.Bounds.Height / 2;
                Assert.InRange(Math.Abs(iconCenter - textCenter), 0, 0.5);
                Assert.InRange(Math.Abs(textCenter - (item.Button.Bounds.Height / 2 + 1)), 0, 0.5);

                var contentLeft = Math.Min(iconOrigin.X, textOrigin.X);
                var contentRight = Math.Max(iconOrigin.X + icon.Bounds.Width, textOrigin.X + text.Bounds.Width);
                Assert.InRange(Math.Abs((contentLeft + contentRight) / 2 - item.Button.Bounds.Width / 2), 0, 0.5);
            }
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Instance_rows_keep_summary_left_and_selected_actions_visible()
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = "Versions"
        };
        var instances = Enumerable.Range(1, 3)
            .Select(index => new InstanceItemViewModel(
                new InstanceRecord
                {
                    Id = $"instance-{index}",
                    Name = $"Hollow Knight {index}",
                    RootPath = Path.Combine(Path.GetTempPath(), $"hollow-knight-{index}"),
                    BuildId = "1.5.78.11833",
                    CreatedAt = DateTimeOffset.UtcNow
                },
                "1.5.78.11833",
                "modding-api-77",
                index))
            .ToArray();
        var gameDirectory = new GameDirectoryItemViewModel(new GameDirectoryRegistration
        {
            Path = Path.GetTempPath(),
            DisplayName = "Test versions",
            Source = GameDirectorySourceKind.Managed
        })
        {
            InstanceCount = instances.Length,
            ScanStatus = viewModel.Loc["ScanReady"]
        };
        viewModel.Instances.GameDirectories.Add(gameDirectory);
        viewModel.Instances.SelectedGameDirectory = gameDirectory;
        foreach (var instance in instances)
        {
            viewModel.Instances.Instances.Add(instance);
            viewModel.Instances.VisibleInstances.Add(instance);
        }
        viewModel.SelectedInstance = instances[1];

        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);

        try
        {
            var rows = window.GetVisualDescendants()
                .OfType<Grid>()
                .Where(grid => grid.Classes.Contains("cfp-instance-row"))
                .ToArray();
            Assert.Equal(3, rows.Length);

            foreach (var row in rows)
            {
                var summary = row.Children.OfType<Grid>().Single(grid => grid.Classes.Contains("cfp-instance-summary"));
                var actions = row.Children.OfType<StackPanel>().Single(panel => panel.Classes.Contains("cfp-instance-actions"));
                var main = row.Children.OfType<Button>().Single(button => button.Classes.Contains("cfp-instance-main"));
                var summaryOrigin = Assert.IsType<Point>(summary.TranslatePoint(default, row));
                var actionsOrigin = Assert.IsType<Point>(actions.TranslatePoint(default, row));

                Assert.InRange(summaryOrigin.X, 0, 0.5);
                Assert.InRange(actionsOrigin.X, row.Bounds.Width - 126.5, row.Bounds.Width - 125.5);
                Assert.InRange(Math.Abs(main.Bounds.Width - row.Bounds.Width), 0, 0.5);
            }

            var selectedRow = rows.Single(row => ReferenceEquals(row.DataContext, instances[1]));
            var selectedActions = selectedRow.Children.OfType<StackPanel>()
                .Single(panel => panel.Classes.Contains("cfp-instance-actions"));
            Assert.Equal(1, selectedActions.Opacity);
            Assert.True(selectedActions.IsHitTestVisible);
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    public static TheoryData<double, string, string?> DpiCases
    {
        get
        {
            var data = new TheoryData<double, string, string?>();
            foreach (var scaling in new[] { 1d, 2d })
            {
                data.Add(scaling, "Settings", null);
                data.Add(scaling, "Downloads", null);
                data.Add(scaling, "Manage", "Mods");
                data.Add(scaling, "Manage", "Presets");
            }

            return data;
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(DpiCases))]
    public void Button_labels_and_icons_are_not_clipped_at_common_dpi_scaling(
        double renderScaling,
        string page,
        string? manageTab)
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = page,
            CurrentManageTab = manageTab ?? "Overview"
        };
        viewModel.Loc.Apply(UiLanguage.English);

        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        window.SetRenderScaling(renderScaling);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);

        try
        {
            using var frame = Assert.IsType<Avalonia.Media.Imaging.WriteableBitmap>(window.GetLastRenderedFrame());
            Assert.Equal((int)Math.Round(window.Width * renderScaling), frame.PixelSize.Width);
            Assert.Equal((int)Math.Round(window.Height * renderScaling), frame.PixelSize.Height);

            var buttons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible
                    && button.Bounds.Width > 0
                    && button.Classes.Any(buttonClass => buttonClass.StartsWith("cfp-", StringComparison.Ordinal)))
                .ToArray();
            Assert.NotEmpty(buttons);

            foreach (var button in buttons)
            {
                if (!button.Classes.Any(buttonClass => buttonClass is
                    "cfp-icon" or
                    "cfp-mod-action" or
                    "cfp-quick-action" or
                    "cfp-instance-action" or
                    "cfp-download-fab"))
                {
                    // Link actions use the compact 32-DIP button template; full actions remain 36 DIP.
                    var minimumHeight = button.Classes.Contains("cfp-link") ? 31.5 : 35.5;
                    Assert.True(button.Bounds.Height >= minimumHeight,
                        $"{page}/{manageTab}: button [{string.Join(' ', button.Classes)}] '{button.Content}' height {button.Bounds.Height:F1} at {renderScaling:P0} scaling.");
                }
                var contentControls = button.GetVisualDescendants()
                    .OfType<Control>()
                    .Where(control => control.IsEffectivelyVisible
                        && (control is TextBlock || control.GetType().Name == "LucideIcon"));
                foreach (var content in contentControls)
                {
                    var origin = content.TranslatePoint(default, button);
                    Assert.NotNull(origin);
                    Assert.True(origin.Value.X >= -0.5 && origin.Value.Y >= -0.5,
                        $"{page}/{manageTab}: {content.GetType().Name} starts outside its button at {renderScaling:P0} scaling.");
                    Assert.True(origin.Value.X + content.Bounds.Width <= button.Bounds.Width + 0.5,
                        $"{page}/{manageTab}: {content.GetType().Name} '{(content as TextBlock)?.Text}' in button '{button.Content}' is clipped horizontally at {renderScaling:P0} scaling. "
                        + $"Origin={origin.Value.X:F1}, content={content.Bounds.Width:F1}, button={button.Bounds.Width:F1}.");
                    Assert.True(origin.Value.Y + content.Bounds.Height <= button.Bounds.Height + 0.5,
                        $"{page}/{manageTab}: {content.GetType().Name} is clipped vertically at {renderScaling:P0} scaling.");
                }
            }
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    private static void CloseImmediately(MainWindow window)
    {
        typeof(MainWindow)
            .GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, true);
        window.Close();
    }
}
