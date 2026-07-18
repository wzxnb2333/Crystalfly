using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class ModMarketRenderingTests
{
    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese, "实例", "Mod 市场", "已安装 Mod", "存档快照")]
    [InlineData(UiLanguage.English, "Instances", "Mod Market", "Installed Mods", "Save Snapshots")]
    public void Navigation_and_new_surfaces_are_localized(
        UiLanguage language,
        string instances,
        string modMarket,
        string installedMods,
        string saveSnapshots)
    {
        var localization = new LocalizationViewModel();
        localization.Apply(language);

        Assert.Equal(instances, localization["NavVersions"]);
        Assert.Equal(modMarket, localization["ModMarket"]);
        Assert.Equal(installedMods, localization["InstalledMods"]);
        Assert.Equal(saveSnapshots, localization["Snapshots"]);
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese, "远程目录", "已缓存", "加载失败")]
    [InlineData(UiLanguage.English, "Remote", "Cached", "Failed")]
    public void Catalog_statuses_are_localized(
        UiLanguage language,
        string remote,
        string cached,
        string failed)
    {
        var localization = new LocalizationViewModel();
        localization.Apply(language);

        Assert.Equal(remote, localization["CatalogRemote"]);
        Assert.Equal(cached, localization["CatalogCached"]);
        Assert.Equal(failed, localization["CatalogFailed"]);
    }

    [AvaloniaFact]
    public void Downloads_use_a_176_pixel_rail_with_game_and_market_sections()
    {
        var (window, _) = Show(page: "Downloads");
        try
        {
            var rail = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(control => control.IsEffectivelyVisible && control.Classes.Contains("cfp-download-rail"));
            Assert.InRange(rail.Bounds.Width, 175.5, 176.5);

            var sectionButtons = rail.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && button.Classes.Contains("cfp-local-nav"))
                .ToArray();
            Assert.Equal(2, sectionButtons.Length);
            Assert.All(sectionButtons, button => Assert.True(button.Bounds.Height >= 43.5));
            Assert.All(sectionButtons, button =>
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))));
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaTheory]
    [InlineData(900, 600)]
    [InlineData(1100, 720)]
    public void English_download_rail_shows_full_game_versions_label(int width, int height)
    {
        var (window, _) = Show(page: "Downloads", width: width, height: height, language: UiLanguage.English);
        try
        {
            var rail = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(control => control.IsEffectivelyVisible && control.Classes.Contains("cfp-download-rail"));
            var label = rail.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(text => text.IsEffectivelyVisible && text.Text == "Game versions");

            Assert.Equal(Avalonia.Media.TextTrimming.None, label.TextTrimming);
            Assert.True(label.Bounds.Width >= 109,
                $"Game versions received only {label.Bounds.Width:F1}px at {width}x{height}.");
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Market_is_a_single_virtualized_list_with_focusable_item_buttons()
    {
        var (window, viewModel) = Show(page: "Downloads", downloadSection: "ModMarket");
        viewModel.VisibleMarketMods.Add(new ModManifest
        {
            Id = "sample-mod",
            Name = "Sample Mod",
            Version = "1.0.0",
            DownloadUrl = "https://example.invalid/sample.zip",
            Sha256 = new string('0', 64),
            LoaderId = "sample-loader"
        });
        Dispatcher.UIThread.RunJobs();

        try
        {
            var list = window.GetVisualDescendants()
                .OfType<ListBox>()
                .Single(control => control.IsEffectivelyVisible && control.Classes.Contains("cfp-market-list"));
            Assert.NotNull(list.ItemsPanel);

            var itemButton = list.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.IsEffectivelyVisible);
            Assert.True(itemButton.Focusable);
            Assert.Equal("Sample Mod", AutomationProperties.GetName(itemButton));
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Instance_mods_surface_only_shows_installed_mods()
    {
        var (window, viewModel) = Show(page: "Manage", manageTab: "Mods");
        try
        {
            var visibleText = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.IsEffectivelyVisible)
                .ToArray();
            Assert.Contains(visibleText, text =>
                text.Classes.Contains("cfp-page-title") && text.Text == viewModel.Loc["InstalledMods"]);
            Assert.DoesNotContain(visibleText, text => text.Text == viewModel.Loc["AvailableMods"]);
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Snapshot_surface_uses_save_snapshot_title()
    {
        var (window, viewModel) = Show(page: "Manage", manageTab: "Snapshots");
        try
        {
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.IsEffectivelyVisible
                && text.Classes.Contains("cfp-page-title")
                && text.Text == viewModel.Loc["Snapshots"]
                && text.Text == "存档快照");
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public void Settings_show_read_only_hk_modlinks_status()
    {
        var (window, viewModel) = Show(page: "Settings");
        try
        {
            var visibleText = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.IsEffectivelyVisible)
                .ToArray();
            Assert.Contains(visibleText, text => text.Text == viewModel.Loc["HKModLinks"]);
            Assert.Contains(visibleText, text => text.Text == viewModel.Loc["HKModLinksHint"]);
            Assert.Contains(visibleText, text => text.Text == viewModel.Loc["CatalogFailed"]);
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public async Task Market_install_dialog_lists_targets_and_disables_blocked_instances()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var normalRoot = Path.Combine(root, "versions", "practice");
        var speedrunRoot = Path.Combine(root, "versions", "race");
        Directory.CreateDirectory(normalRoot);
        Directory.CreateDirectory(speedrunRoot);
        var mod = new ModManifest
        {
            Id = "sample-mod",
            Name = "Sample Mod",
            Version = "1.0.0",
            DownloadUrl = "https://example.invalid/sample.zip",
            Sha256 = new string('0', 64),
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            CurrentPage = "Downloads",
            CurrentDownloadSection = "ModMarket",
            VersionRoot = Path.Combine(root, "versions"),
            SelectedMarketMod = mod
        };
        typeof(MainViewModel).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new GameCatalog
            {
                Loaders =
                [
                    new LoaderManifest
                    {
                        Id = "modding-api-77",
                        Name = "Modding API v77",
                        Version = "77",
                        DownloadUrl = "https://example.invalid/loader.zip",
                        Sha256 = new string('1', 64),
                        SupportedBuildIds = ["1.5.78.11833"]
                    }
                ],
                Mods = [mod]
            });
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("practice", "Practice", normalRoot),
            "1.5.78.11833",
            "Vanilla",
            0));
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("race", "Race", speedrunRoot) with { Purpose = InstancePurpose.OfficialSpeedrun },
            "1.5.78.11833",
            "Vanilla",
            0));
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var install = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.IsEffectivelyVisible
                    && AutomationProperties.GetName(button) == viewModel.Loc["InstallModTitle"]);
            install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            CustomDialogControl[] dialogs = [];
            for (var attempt = 0; attempt < 50 && dialogs.Length == 0; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialogs = window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray();
            }

            var dialog = Assert.Single(dialogs);
            var targetButtons = dialog.GetVisualDescendants().OfType<RadioButton>().ToArray();
            Assert.Equal(2, targetButtons.Length);
            var availableTarget = Assert.Single(targetButtons, target => target.IsEnabled);
            Assert.Single(targetButtons, target => !target.IsEnabled);
            Assert.Equal("Practice", AutomationProperties.GetName(availableTarget));
            Assert.Contains(targetButtons, target => AutomationProperties.GetName(target) == "Race");
            Assert.True(availableTarget.IsChecked);
            Assert.True(availableTarget.IsFocused);
            Assert.Contains(dialog.GetVisualDescendants().OfType<Button>(), button =>
                AutomationProperties.GetName(button) == viewModel.Loc["InstallSelectedMod"]);
            Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.Text?.Contains("Modding API v77", StringComparison.OrdinalIgnoreCase) == true);
            dialog.Close();
        }
        finally
        {
            CloseImmediately(window);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Market_install_dialog_does_not_open_after_market_selection_changes(bool selectAnotherMod)
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var instanceRoot = Path.Combine(root, "versions", "slow");
        var managedRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed");
        var stateRoot = Path.Combine(root, "versions", ".crystalfly", "instances", "slow");
        Directory.CreateDirectory(managedRoot);
        Directory.CreateDirectory(stateRoot);
        var hookPath = Path.Combine(managedRoot, "MMHOOK_Assembly-CSharp.dll");
        await using (var hook = new FileStream(hookPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            hook.SetLength(64L * 1024 * 1024);
        }
        await Crystalfly.Core.Serialization.AtomicJsonStore.WriteAsync(
            Path.Combine(stateRoot, "loader.json"),
            new InstalledPackageReceipt
            {
                PackageId = "modding-api-77",
                LoaderState = LoaderState.ModdingApi,
                Files =
                [
                    new InstalledFileReceipt
                    {
                        RelativePath = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll",
                        Sha256 = new string('0', 64)
                    }
                ]
            });
        var first = new ModManifest
        {
            Id = "first-mod",
            Name = "First Mod",
            Version = "1.0.0",
            DownloadUrl = "https://example.invalid/first.zip",
            Sha256 = new string('1', 64),
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var second = first with { Id = "second-mod", Name = "Second Mod" };
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            CurrentPage = "Downloads",
            CurrentDownloadSection = "ModMarket",
            VersionRoot = Path.Combine(root, "versions"),
            SelectedMarketMod = first
        };
        typeof(MainViewModel).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new GameCatalog { Mods = [first, second] });
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("slow", "Slow", instanceRoot),
            "1.5.78.11833",
            "Modding API v77",
            0));
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var install = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.IsEffectivelyVisible
                    && AutomationProperties.GetName(button) == viewModel.Loc["InstallModTitle"]);
            install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(viewModel.PrepareMarketInstallTargetsCommand.IsRunning);

            viewModel.SelectedMarketMod = selectAnotherMod ? second : null;
            for (var attempt = 0;
                 attempt < 100 && viewModel.PrepareMarketInstallTargetsCommand.IsRunning;
                 attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.PrepareMarketInstallTargetsCommand.IsRunning);
            Assert.Empty(GetDialogs(window));
        }
        finally
        {
            foreach (var dialog in GetDialogs(window))
            {
                dialog.Close();
            }
            CloseImmediately(window);
            await viewModel.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Market_install_dialog_allows_only_one_preparation_and_one_dialog()
    {
        var context = await ShowSlowMarketInstallAsync();
        try
        {
            context.InstallButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(context.ViewModel.PrepareMarketInstallTargetsCommand.IsRunning);

            context.InstallButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var attempt = 0;
                 attempt < 100 && context.ViewModel.PrepareMarketInstallTargetsCommand.IsRunning;
                 attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Dispatcher.UIThread.RunJobs();

            Assert.False(context.ViewModel.PrepareMarketInstallTargetsCommand.IsRunning);
            var dialog = Assert.Single(GetDialogs(context.Window));
            context.InstallButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Same(dialog, Assert.Single(GetDialogs(context.Window)));
            dialog.Close();
        }
        finally
        {
            await CloseSlowMarketInstallAsync(context);
        }
    }

    [AvaloniaFact]
    public async Task Market_install_success_closes_overlay()
    {
        var context = await ShowReadyMarketInstallAsync();
        try
        {
            var install = context.Dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button)
                    == context.ViewModel.Loc["InstallSelectedMod"]);

            install.Command!.Execute(install.CommandParameter);
            for (var attempt = 0; attempt < 100 && GetDialogs(context.Window).Length > 0; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.Empty(GetDialogs(context.Window));
            Assert.Null(context.ViewModel.ErrorMessage);
            Assert.True(File.Exists(context.InstalledModPath));
        }
        finally
        {
            await CloseReadyMarketInstallAsync(context);
        }
    }

    [AvaloniaFact]
    public async Task Market_install_failure_keeps_overlay_and_shows_error()
    {
        var context = await ShowReadyMarketInstallAsync();
        try
        {
            var dialogViewModel = Assert.IsType<MarketInstallDialogViewModel>(context.Dialog.DataContext);
            context.ViewModel.Instances.Clear();
            var install = context.Dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button)
                    == context.ViewModel.Loc["InstallSelectedMod"]);

            install.Command!.Execute(install.CommandParameter);
            for (var attempt = 0; attempt < 100 && context.ViewModel.InstallMarketModCommand.IsRunning; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(context.Dialog, GetDialogs(context.Window));
            Assert.Equal(context.ViewModel.Loc["NoInstance"], context.ViewModel.ErrorMessage);
            Assert.Equal(context.ViewModel.Loc["NoInstance"], dialogViewModel.DialogError);
            Assert.Contains(context.Dialog.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.IsEffectivelyVisible && text.Text == context.ViewModel.Loc["NoInstance"]);
        }
        finally
        {
            await CloseReadyMarketInstallAsync(context);
        }
    }

    [AvaloniaFact]
    public async Task Closing_main_window_during_market_target_preparation_is_handled()
    {
        var context = await ShowSlowMarketInstallAsync();
        try
        {
            context.InstallButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(context.ViewModel.PrepareMarketInstallTargetsCommand.IsRunning);

            context.Window.Close();
            for (var attempt = 0;
                 attempt < 100 && context.ViewModel.PrepareMarketInstallTargetsCommand.IsRunning;
                 attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Dispatcher.UIThread.RunJobs();

            Assert.False(context.ViewModel.PrepareMarketInstallTargetsCommand.IsRunning);
            Assert.False(context.Window.IsVisible);
            Assert.Empty(GetDialogs(context.Window));
        }
        finally
        {
            await CloseSlowMarketInstallAsync(context);
        }
    }

    [AvaloniaFact]
    public async Task Market_install_dialog_cannot_close_while_an_install_is_busy()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var mod = new ModManifest
        {
            Id = "sample-mod",
            Name = "Sample Mod",
            Version = "1.0.0",
            DownloadUrl = "https://example.invalid/sample.zip",
            Sha256 = new string('0', 64),
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            CurrentPage = "Downloads",
            CurrentDownloadSection = "ModMarket",
            VersionRoot = Path.Combine(root, "versions"),
            SelectedMarketMod = mod
        };
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var install = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.IsEffectivelyVisible
                    && AutomationProperties.GetName(button) == viewModel.Loc["InstallModTitle"]);
            install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            CustomDialogControl[] dialogs = [];
            for (var attempt = 0; attempt < 50 && dialogs.Length == 0; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialogs = GetDialogs(window);
            }

            var dialog = Assert.Single(dialogs);
            var cancel = dialog.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["Cancel"]);
            viewModel.IsBusy = true;

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(dialog, GetDialogs(window));

            Assert.NotNull(cancel.Command);
            cancel.Command.Execute(cancel.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(dialog, GetDialogs(window));

            dialog.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(dialog, GetDialogs(window));

            viewModel.IsBusy = false;
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(dialog, GetDialogs(window));
        }
        finally
        {
            viewModel.IsBusy = false;
            foreach (var dialog in GetDialogs(window))
            {
                dialog.Close();
            }
            CloseImmediately(window);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Main_window_cannot_close_while_an_install_is_busy()
    {
        var (window, viewModel) = Show(page: "Downloads", downloadSection: "ModMarket");
        try
        {
            viewModel.IsBusy = true;
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.IsVisible);

            viewModel.IsBusy = false;
            window.Close();
            for (var attempt = 0; attempt < 50 && window.IsVisible; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.False(window.IsVisible);
        }
        finally
        {
            viewModel.IsBusy = false;
            if (window.IsVisible)
            {
                CloseImmediately(window);
            }
        }
    }

    private static InstanceRecord Instance(string id, string name, string rootPath) => new()
    {
        Id = id,
        Name = name,
        RootPath = rootPath,
        BuildId = "1.5.78.11833",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static async Task<ReadyMarketInstallContext> ShowReadyMarketInstallAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var versionRoot = Path.Combine(root, "versions");
        var instanceRoot = Path.Combine(versionRoot, "practice");
        var managedRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed");
        var stateRoot = Path.Combine(versionRoot, ".crystalfly", "instances", "practice");
        var cacheRoot = Path.Combine(versionRoot, ".crystalfly", "packages");
        Directory.CreateDirectory(managedRoot);
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(cacheRoot);

        var loaderPath = Path.Combine(managedRoot, "MMHOOK_Assembly-CSharp.dll");
        await File.WriteAllTextAsync(loaderPath, "loader");
        await AtomicJsonStore.WriteAsync(
            Path.Combine(stateRoot, "loader.json"),
            new InstalledPackageReceipt
            {
                PackageId = "modding-api-77",
                LoaderState = LoaderState.ModdingApi,
                IsVerified = true,
                Files =
                [
                    new InstalledFileReceipt
                    {
                        RelativePath = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll",
                        Sha256 = FileSha256(loaderPath)
                    }
                ]
            });

        var packagePath = Path.Combine(root, "mod.zip");
        CreateZip(packagePath, "mod.dll", "mod");
        var packageHash = FileSha256(packagePath);
        File.Copy(packagePath, Path.Combine(cacheRoot, $"{packageHash}.zip"));
        var mod = new ModManifest
        {
            Id = "sample-mod",
            Name = "Sample Mod",
            Version = "1.0.0",
            DownloadUrl = "https://example.invalid/sample.zip",
            SizeBytes = new FileInfo(packagePath).Length,
            Sha256 = packageHash,
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            CurrentPage = "Downloads",
            CurrentDownloadSection = "ModMarket",
            VersionRoot = versionRoot,
            SelectedMarketMod = mod
        };
        typeof(MainViewModel).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new GameCatalog { Mods = [mod] });
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("practice", "Practice", instanceRoot),
            "1.5.78.11833",
            "Modding API v77",
            0));
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        var openDialog = window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible
                && AutomationProperties.GetName(button) == viewModel.Loc["InstallModTitle"]);
        openDialog.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        CustomDialogControl[] dialogs = [];
        for (var attempt = 0; attempt < 100 && dialogs.Length == 0; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
            dialogs = GetDialogs(window);
        }

        return new ReadyMarketInstallContext(
            root,
            window,
            viewModel,
            Assert.Single(dialogs),
            Path.Combine(managedRoot, "Mods", "Sample Mod", "mod.dll"));
    }

    private static async Task CloseReadyMarketInstallAsync(ReadyMarketInstallContext context)
    {
        context.ViewModel.IsBusy = false;
        foreach (var dialog in GetDialogs(context.Window))
        {
            dialog.Close();
        }
        CloseImmediately(context.Window);
        await context.ViewModel.DisposeAsync();
        if (Directory.Exists(context.Root))
        {
            Directory.Delete(context.Root, recursive: true);
        }
    }

    private static void CreateZip(string path, string entryName, string contents)
    {
        using var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
        writer.Write(contents);
    }

    private static string FileSha256(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record ReadyMarketInstallContext(
        string Root,
        MainWindow Window,
        MainViewModel ViewModel,
        CustomDialogControl Dialog,
        string InstalledModPath);

    private static async Task<SlowMarketInstallContext> ShowSlowMarketInstallAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        var instanceRoot = Path.Combine(root, "versions", "slow");
        var managedRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed");
        var stateRoot = Path.Combine(root, "versions", ".crystalfly", "instances", "slow");
        Directory.CreateDirectory(managedRoot);
        Directory.CreateDirectory(stateRoot);
        var hookPath = Path.Combine(managedRoot, "MMHOOK_Assembly-CSharp.dll");
        await using (var hook = new FileStream(hookPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            hook.SetLength(64L * 1024 * 1024);
        }
        await Crystalfly.Core.Serialization.AtomicJsonStore.WriteAsync(
            Path.Combine(stateRoot, "loader.json"),
            new InstalledPackageReceipt
            {
                PackageId = "modding-api-77",
                LoaderState = LoaderState.ModdingApi,
                Files =
                [
                    new InstalledFileReceipt
                    {
                        RelativePath = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll",
                        Sha256 = new string('0', 64)
                    }
                ]
            });
        var mod = new ModManifest
        {
            Id = "first-mod",
            Name = "First Mod",
            Version = "1.0.0",
            DownloadUrl = "https://example.invalid/first.zip",
            Sha256 = new string('1', 64),
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            CurrentPage = "Downloads",
            CurrentDownloadSection = "ModMarket",
            VersionRoot = Path.Combine(root, "versions"),
            SelectedMarketMod = mod
        };
        typeof(MainViewModel).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new GameCatalog { Mods = [mod] });
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("slow", "Slow", instanceRoot),
            "1.5.78.11833",
            "Modding API v77",
            0));
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        var installButton = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.IsEffectivelyVisible
                && AutomationProperties.GetName(button) == viewModel.Loc["InstallModTitle"]);
        return new(root, window, viewModel, installButton);
    }

    private static async Task CloseSlowMarketInstallAsync(SlowMarketInstallContext context)
    {
        context.ViewModel.IsBusy = false;
        foreach (var dialog in GetDialogs(context.Window))
        {
            dialog.Close();
        }
        if (context.Window.IsVisible)
        {
            CloseImmediately(context.Window);
        }
        await context.ViewModel.DisposeAsync();
        for (var attempt = 0;
             attempt < 100 && context.ViewModel.PrepareMarketInstallTargetsCommand.IsRunning;
             attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
        if (Directory.Exists(context.Root))
        {
            Directory.Delete(context.Root, recursive: true);
        }
    }

    private sealed record SlowMarketInstallContext(
        string Root,
        MainWindow Window,
        MainViewModel ViewModel,
        Button InstallButton);

    private static CustomDialogControl[] GetDialogs(MainWindow window) =>
        window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray();

    private static (MainWindow Window, MainViewModel ViewModel) Show(
        string page,
        string? manageTab = null,
        string? downloadSection = null,
        int width = 900,
        int height = 600,
        UiLanguage language = UiLanguage.SimplifiedChinese)
    {
        var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = page,
            CurrentManageTab = manageTab ?? "Overview",
            CurrentDownloadSection = downloadSection ?? "GameVersions"
        };
        viewModel.Loc.Apply(language);

        var window = new MainWindow { Width = width, Height = height };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        return (window, viewModel);
    }

    private static void CloseImmediately(MainWindow window)
    {
        typeof(MainWindow)
            .GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, true);
        window.Close();
    }
}
