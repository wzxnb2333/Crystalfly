using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Services;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Snapshots;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class GameAvatarSwitchTests
{
    [AvaloniaTheory]
    [InlineData(GameId.HollowKnight, false, false, "Launch")]
    [InlineData(GameId.Silksong, false, false, "Launch")]
    [InlineData(GameId.HollowKnight, true, false, "Launch")]
    [InlineData(GameId.HollowKnight, false, true, "Launch")]
    [InlineData(GameId.HollowKnight, false, false, "Downloads")]
    public async Task Switching_keeps_the_current_instance_visible_until_the_scan_finishes(
        GameId original, bool empty, bool fail, string page)
    {
        await using var fixture = await Fixture.CreateAsync(UiMotionPreference.Off, withInstances: true);
        var model = fixture.Model;
        await model.SelectGameCommand.ExecuteAsync(original.ToString());
        model.CurrentPage = page;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        var input = page == "Downloads" ? fixture.Window.FindControl<TextBox>("DownloadBuildSearch") : null;
        if (input is not null) Assert.True(input.Focus());
        var oldText = input?.Text;
        var previous = Assert.IsType<InstanceItemViewModel>(model.SelectedInstance);
        var card = fixture.Window.FindControl<Control>("LaunchInstanceCard")!;
        CapturePreview(fixture.Window, $"{original}-{page}-before");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = original == GameId.HollowKnight ? GameId.Silksong : GameId.HollowKnight;
        Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>> discover = async (path, catalog, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            if (fail) throw new IOException("scan failed");
            return empty ? [] : await InstanceImportService.DiscoverAsync(path, catalog, target, token);
        };
        typeof(MainViewModel).GetField("instanceDiscovery", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(model, discover);
        var switching = model.ToggleGameCommand.ExecuteAsync(null);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Dispatcher.UIThread.RunJobs();
            fixture.Window.UpdateLayout();
            Assert.Same(previous, model.SelectedInstance);
            Assert.True(card.IsVisible, "The instance card must not flash an empty state while scanning.");
            Assert.False(model.CanAttemptLaunch);
            Assert.False(model.CanNavigate);
            Assert.All(fixture.Window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("cfp-nav")),
                button => Assert.True(button.IsEffectivelyEnabled, "Navigation must not flash its disabled style during a switch."));
            if (input is not null)
            {
                fixture.Window.KeyTextInput("unexpected edit");
                fixture.Window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                fixture.Window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                Assert.Equal(oldText, input.Text);
                var center = input.TranslatePoint(new Point(input.Bounds.Width / 2, input.Bounds.Height / 2), fixture.Window)!.Value;
                var hit = fixture.Window.InputHitTest(center) as Control;
                Assert.False(hit == input || hit?.GetVisualAncestors().Contains(input) == true);
            }
            CapturePreview(fixture.Window, $"{original}-{page}-scanning");
            var cloneName = model.CloneInstanceName;
            model.CloneInstanceName = "Must not clone while switching";
            await model.Instances.CloneSelectedInstanceCommand.ExecuteAsync(null);
            Assert.False(Directory.Exists(Path.Combine(model.VersionRoot, model.CloneInstanceName)));
            model.CloneInstanceName = cloneName;
        }
        finally
        {
            release.TrySetResult();
            await switching.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(target, model.SelectedGameId);
        Assert.Equal(!empty && !fail, card.IsVisible);
        if (empty || fail) Assert.Null(model.SelectedInstance);
        else Assert.Equal(target, model.SelectedInstance!.Record.GameId);
        Assert.Equal(fail, model.ErrorMessage is not null);
        await Task.Delay(350);
        Dispatcher.UIThread.RunJobs();
        CapturePreview(fixture.Window, $"{original}-{page}-{(empty ? "empty" : fail ? "failed" : "populated")}-after");
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.English)]
    public async Task Successful_switch_shows_a_localized_toast_at_the_top_center(UiLanguage language)
    {
        await using var fixture = await Fixture.CreateAsync(UiMotionPreference.Off);
        var model = fixture.Model;
        model.SelectedLanguage = new(language, language.ToString());
        foreach (var name in new[] { "GameSilksong", "GameHollowKnight" })
        {
            await model.ToggleGameCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            fixture.Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var expected = language == UiLanguage.English
                ? $"Switched to {model.Loc[name]}"
                : $"已切换到{model.Loc[name]}";
            var toast = Assert.Single(fixture.Window.GetVisualDescendants().OfType<ToastCard>(),
                card => card.Content?.ToString() == expected);
            Assert.Equal(NotificationType.Success, toast.NotificationType);
            var center = toast.TranslatePoint(new Point(toast.Bounds.Width / 2, 0), fixture.Window)!.Value;
            Assert.InRange(center.X, fixture.Window.Bounds.Width / 2 - 2, fixture.Window.Bounds.Width / 2 + 2);
            Assert.InRange(center.Y, 0, 150);
        }
    }

    [AvaloniaTheory]
    [InlineData("Launch")]
    [InlineData("Versions")]
    [InlineData("Downloads")]
    [InlineData("Manage")]
    [InlineData("Speedrun")]
    [InlineData("Settings")]
    public async Task Shift_tab_switches_both_ways_from_each_page_without_navigating(string page)
    {
        await using var fixture = await Fixture.CreateAsync(UiMotionPreference.Off);
        var model = fixture.Model;
        model.CurrentPage = page;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        var input = fixture.Window.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
        if (input is not null) Assert.True(input.Focus());
        else fixture.Window.Focus();

        foreach (var expectedGame in new[] { GameId.Silksong, GameId.HollowKnight })
        {
            fixture.Window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
            fixture.Window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
            Assert.NotNull(model.ToggleGameCommand.ExecutionTask);
            await model.ToggleGameCommand.ExecutionTask!;
            Assert.Equal(expectedGame, model.SelectedGameId);
            Assert.Equal(page, model.CurrentPage);
        }
    }

    [AvaloniaFact]
    public async Task Shift_tab_respects_guards_and_dialogs_and_other_tab_gestures_keep_the_game()
    {
        await using var fixture = await Fixture.CreateAsync(UiMotionPreference.Off);
        var model = fixture.Model;
        void PressTab(RawInputModifiers modifiers)
        {
            fixture.Window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
            fixture.Window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
            Assert.Equal(GameId.HollowKnight, model.SelectedGameId);
        }
        PressTab(RawInputModifiers.None);
        PressTab(RawInputModifiers.Control | RawInputModifiers.Shift);
        model.IsBusy = true;
        PressTab(RawInputModifiers.Shift);
        model.IsBusy = false;
        model.IsGameRunning = true;
        PressTab(RawInputModifiers.Shift);
        model.IsGameRunning = false;
        var host = fixture.Window.FindControl<Panel>("MainOverlayDialogHost")!;
        var dialog = new CustomDialogControl();
        host.Children.Add(dialog);
        PressTab(RawInputModifiers.Shift);
        host.Children.Remove(dialog);
        model.SaveEditor = new SaveEditorViewModel(new NamedSnapshotService(fixture.Root,
            "Crystalfly.Shortcut." + Guid.NewGuid().ToString("N"), new Idle()), "test", null, "test") { IsDirty = true };
        PressTab(RawInputModifiers.Shift);
        Assert.Equal(model.Loc["SaveBeforeGameSwitch"], model.ErrorMessage);
    }

    [AvaloniaTheory]
    [InlineData(UiMotionPreference.Off)]
    [InlineData(UiMotionPreference.Reduced)]
    public async Task Clicking_portrait_switches_both_ways_without_a_loading_overlay(UiMotionPreference motion)
    {
        await using var fixture = await Fixture.CreateAsync(motion);
        var model = fixture.Model;
        var avatar = fixture.Window.FindControl<Button>("GameAvatarButton")!;
        Assert.True(avatar.Focusable);
        Assert.Same(model.ToggleGameCommand, avatar.Command);
        Assert.Null(fixture.Window.FindControl<Control>("GameSelector"));
        AssertPortrait(fixture.Window, silksong: false);
        var switchingStates = new List<bool>();
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainViewModel.SelectedGameId)) return;
            switchingStates.Add(model.IsSwitchingGame);
            Assert.False(model.CanNavigate);
            Assert.False(model.ToggleGameCommand.CanExecute(null));
            Assert.False(model.IsBusy);
        };

        foreach (var expectedGame in new[] { GameId.Silksong, GameId.HollowKnight })
        {
            Dispatcher.UIThread.RunJobs();
            fixture.Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = avatar.TranslatePoint(new Point(avatar.Bounds.Width / 2, avatar.Bounds.Height / 2), fixture.Window)!.Value;
            fixture.Window.MouseDown(center, MouseButton.Left);
            fixture.Window.MouseUp(center, MouseButton.Left);
            Assert.NotNull(model.ToggleGameCommand.ExecutionTask);
            await model.ToggleGameCommand.ExecutionTask!;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(expectedGame, model.SelectedGameId);
            AssertPortrait(fixture.Window, expectedGame == GameId.Silksong);
            Assert.False(model.IsSwitchingGame);
            Assert.True(avatar.IsEffectivelyEnabled);
            Assert.Equal(model.SelectedGameName, fixture.Window.FindControl<TextBlock>("CurrentGameLabel")!.Text);
            Assert.Equal(model.GameAvatarHint, AutomationProperties.GetName(avatar));
            Assert.Null(fixture.Window.FindControl<Image>("KnightGameAvatar")!.Transitions);
            Assert.Null(fixture.Window.FindControl<Image>("HornetGameAvatar")!.Transitions);
        }
        Assert.Equal(new[] { true, true }, switchingStates);
        var saved = await CrystalflySettingsStore.LoadAsync(Path.Combine(fixture.Root, "settings.json"));
        Assert.Equal(GameId.HollowKnight, saved.SelectedGameId);
    }

    [AvaloniaFact]
    public async Task Portrait_respects_busy_running_and_unsaved_save_guards()
    {
        await using var fixture = await Fixture.CreateAsync(UiMotionPreference.Off);
        var model = fixture.Model;
        var avatar = fixture.Window.FindControl<Button>("GameAvatarButton")!;
        model.IsBusy = true;
        Assert.False(model.IsWorkspaceEnabled);
        Assert.False(model.ToggleGameCommand.CanExecute(null));
        Assert.False(avatar.IsEffectivelyEnabled);
        await model.ToggleGameCommand.ExecuteAsync(null);
        Assert.Equal(GameId.HollowKnight, model.SelectedGameId);
        model.IsBusy = false;
        model.IsGameRunning = true;
        Assert.False(model.IsWorkspaceEnabled);
        Assert.False(model.ToggleGameCommand.CanExecute(null));
        Assert.False(avatar.IsEffectivelyEnabled);
        await model.ToggleGameCommand.ExecuteAsync(null);
        Assert.Equal(GameId.HollowKnight, model.SelectedGameId);
        model.IsGameRunning = false;
        model.SaveEditor = new SaveEditorViewModel(new NamedSnapshotService(fixture.Root,
            "Crystalfly.Avatar." + Guid.NewGuid().ToString("N"), new Idle()), "test", null, "test") { IsDirty = true };
        await model.ToggleGameCommand.ExecuteAsync(null);
        Assert.Equal(GameId.HollowKnight, model.SelectedGameId);
        Assert.Equal(model.Loc["SaveBeforeGameSwitch"], model.ErrorMessage);
        AssertPortrait(fixture.Window, silksong: false);
        Assert.False(model.IsSwitchingGame);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(fixture.Window.GetVisualDescendants().OfType<ToastCard>(),
            card => card.NotificationType == NotificationType.Success);
    }

    [AvaloniaFact]
    public async Task Portrait_hint_and_game_name_follow_language_changes()
    {
        await using var fixture = await Fixture.CreateAsync(UiMotionPreference.Off);
        var model = fixture.Model;
        var label = fixture.Window.FindControl<TextBlock>("CurrentGameLabel")!;
        var avatar = fixture.Window.FindControl<Button>("GameAvatarButton")!;
        Assert.Contains("点击头像", model.GameAvatarHint, StringComparison.Ordinal);
        model.SelectedLanguage = new(UiLanguage.English, "English");
        Assert.Equal("Hollow Knight", label.Text);
        Assert.Contains("Click the portrait", AutomationProperties.GetName(avatar), StringComparison.Ordinal);
        await model.ToggleGameCommand.ExecuteAsync(null);
        Assert.Equal(model.Loc["GameSilksong"], label.Text);
        model.SelectedLanguage = new(UiLanguage.SimplifiedChinese, "简体中文");
        Assert.Equal(model.Loc["GameSilksong"], label.Text);
        Assert.Contains("点击头像", AutomationProperties.GetName(avatar), StringComparison.Ordinal);
        Assert.Contains(model.Loc["GameHollowKnight"], (string)ToolTip.GetTip(avatar)!, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Full_motion_uses_short_transitions_and_stops_when_motion_is_disabled()
    {
        await using var fixture = await Fixture.CreateAsync(UiMotionPreference.FollowSystem);
        var coordinator = (MotionCoordinator)typeof(MainWindow)
            .GetField("motionCoordinator", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fixture.Window)!;
        var motionEnabled = (bool)typeof(MotionCoordinator)
            .GetMethod("IsFullMotionEnabled", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(coordinator, null)!;
        await fixture.Model.ToggleGameCommand.ExecuteAsync(null);
        foreach (var name in new[] { "KnightGameAvatar", "HornetGameAvatar" })
        {
            var portrait = fixture.Window.FindControl<Image>(name)!;
            Assert.NotNull(portrait.Source);
            Assert.True(portrait.Source.Size.Width > 0);
            if (motionEnabled)
            {
                var transition = Assert.IsType<DoubleTransition>(Assert.Single(portrait.Transitions!));
                Assert.Equal(Visual.OpacityProperty, transition.Property);
                Assert.Equal(TimeSpan.FromMilliseconds(180), transition.Duration);
                Assert.Equal(2, Assert.IsType<ScaleTransform>(portrait.RenderTransform).Transitions!.Count);
            }
            else Assert.Null(portrait.Transitions);
        }
        fixture.Model.SelectedMotionPreference = new(UiMotionPreference.Off, "Off");
        AssertPortrait(fixture.Window, silksong: true);
        Assert.Null(fixture.Window.FindControl<Image>("HornetGameAvatar")!.Transitions);
        Assert.Null(Assert.IsType<ScaleTransform>(fixture.Window.FindControl<Image>("HornetGameAvatar")!.RenderTransform).Transitions);
    }

    private static void AssertPortrait(MainWindow window, bool silksong)
    {
        Assert.Equal(silksong ? 0 : 1, window.FindControl<Image>("KnightGameAvatar")!.Opacity);
        Assert.Equal(silksong ? 1 : 0, window.FindControl<Image>("HornetGameAvatar")!.Opacity);
    }

    private static void CapturePreview(MainWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("CRYSTALFLY_GAME_SWITCH_PREVIEW_DIR") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        using var frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
    }

    private sealed class Idle : IHollowKnightProcessProbe { public bool IsRunning() => false; }

    private sealed class Fixture(string root, MainViewModel model, MainWindow window) : IAsyncDisposable
    {
        public string Root { get; } = root;
        public MainViewModel Model { get; } = model;
        public MainWindow Window { get; } = window;

        public static async Task<Fixture> CreateAsync(UiMotionPreference motion, bool withInstances = false)
        {
            var root = Path.Combine(Path.GetTempPath(), "Crystalfly.GameAvatar", Guid.NewGuid().ToString("N"));
            var library = Path.Combine(root, "library");
            if (withInstances)
            {
                foreach (var game in new[] { GameDefinition.HollowKnight, GameDefinition.Silksong })
                {
                    var path = Path.Combine(library, game.Id.ToString());
                    Directory.CreateDirectory(Path.Combine(path, game.DataDirectoryName));
                    File.WriteAllText(Path.Combine(path, game.ExecutableName), "exe");
                    File.WriteAllText(Path.Combine(path, "UnityPlayer.dll"), "unity");
                    File.WriteAllText(Path.Combine(path, game.GlobalGameManagersPath), "data");
                }
            }
            await CrystalflySettingsStore.SaveAsync(Path.Combine(root, "settings.json"), new()
            {
                OfflineMode = true, CheckForUpdates = false, OnboardingCompleted = true,
                ErrorReportingEnabled = false, MotionPreference = motion, Language = UiLanguage.SimplifiedChinese,
                GameDirectories = withInstances
                    ? [new() { GameId = GameId.HollowKnight, Path = library, DisplayName = "HK" },
                        new() { GameId = GameId.Silksong, Path = library, DisplayName = "Silksong" }] : [],
                GameWorkspaces = withInstances
                    ? [new() { GameId = GameId.HollowKnight, VersionRoot = library },
                        new() { GameId = GameId.Silksong, VersionRoot = library }] : []
            });
            var model = new MainViewModel(root);
            await model.InitializeAsync();
            await ((Task)typeof(MainViewModel).GetField("catalogRefreshTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(model)!)
                .WaitAsync(TimeSpan.FromSeconds(10));
            var window = new MainWindow { Width = 1100, Height = 720 };
            window.Show();
            window.DataContext = model;
            model.CurrentPage = "Launch";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            return new Fixture(root, model, window);
        }

        public async ValueTask DisposeAsync()
        {
            typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Window, true);
            Window.Close();
            await Model.DisposeAsync();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
