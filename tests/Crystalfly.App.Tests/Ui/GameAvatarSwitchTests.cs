using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Crystalfly.App.Services;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.App.Tests.Ui;

public sealed class GameAvatarSwitchTests
{
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
        Assert.False(model.ToggleGameCommand.CanExecute(null));
        Assert.False(avatar.IsEffectivelyEnabled);
        await model.ToggleGameCommand.ExecuteAsync(null);
        Assert.Equal(GameId.HollowKnight, model.SelectedGameId);
        model.IsBusy = false;
        model.IsGameRunning = true;
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

    private sealed class Idle : IHollowKnightProcessProbe { public bool IsRunning() => false; }

    private sealed class Fixture(string root, MainViewModel model, MainWindow window) : IAsyncDisposable
    {
        public string Root { get; } = root;
        public MainViewModel Model { get; } = model;
        public MainWindow Window { get; } = window;

        public static async Task<Fixture> CreateAsync(UiMotionPreference motion)
        {
            var root = Path.Combine(Path.GetTempPath(), "Crystalfly.GameAvatar", Guid.NewGuid().ToString("N"));
            await CrystalflySettingsStore.SaveAsync(Path.Combine(root, "settings.json"), new()
            {
                OfflineMode = true, CheckForUpdates = false, OnboardingCompleted = true,
                ErrorReportingEnabled = false, MotionPreference = motion, Language = UiLanguage.SimplifiedChinese
            });
            var model = new MainViewModel(root);
            await model.InitializeAsync();
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
