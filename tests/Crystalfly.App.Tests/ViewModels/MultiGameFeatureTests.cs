using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class MultiGameFeatureTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.MultiGame.UI", Guid.NewGuid().ToString("N"));

    [AvaloniaFact]
    public async Task Silksong_speedrun_can_be_created_without_template_and_retains_its_LiveSplit_binding()
    {
        var library = Directory.CreateDirectory(Path.Combine(root, "silk")).FullName;
        var sourceRoot = Directory.CreateDirectory(Path.Combine(library, "source")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(sourceRoot, GameDefinition.Silksong.DataDirectoryName)).FullName;
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, GameDefinition.Silksong.ExecutableName), "game");
        await File.WriteAllTextAsync(Path.Combine(data, "globalgamemanagers"), "data");
        await InstanceSidecar.SaveAsync(new InstanceRecord
        {
            Id = "source", Name = "日常游玩", RootPath = sourceRoot, BuildId = "unknown",
            GameId = GameId.Silksong, DeclaredVersion = "1.0.30000"
        });
        var app = Path.Combine(root, "app");
        await CrystalflySettingsStore.SaveAsync(Path.Combine(app, "settings.json"), new()
        {
            OfflineMode = true, CheckForUpdates = false, OnboardingCompleted = true,
            SelectedGameId = GameId.Silksong, VersionRoot = library,
            GameDirectories = [new() { GameId = GameId.Silksong, Path = library, DisplayName = "Silk" }],
            GameWorkspaces = [new() { GameId = GameId.Silksong, VersionRoot = library }]
        });
        string createdId;
        var lss = Path.Combine(root, "丝之歌 Any Percent.lss");
        await File.WriteAllTextAsync(lss, "<Run />");
        await using (var vm = new MainViewModel(app))
        {
            await InitializeAsync(vm);
            Assert.Null(vm.SelectedSpeedrunTemplate);
            vm.SelectPageCommand.Execute("Speedrun");
            vm.SelectSpeedrunTabCommand.Execute("Activity");
            vm.SelectSpeedrunTabCommand.Execute("Environment");
            Assert.True(vm.IsSpeedrunEnvironmentTab);
            Assert.Single(vm.SpeedrunSourceInstances);
            vm.SpeedrunEnvironmentName = "速通练习";
            await vm.CreateSpeedrunEnvironmentCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            var created = Assert.Single(vm.Instances.SpeedrunInstances);
            createdId = created.Id;
            Assert.Same(created, vm.SelectedSpeedrunInstance);
            Assert.Equal(InstancePurpose.CustomSpeedrun, created.Record.Purpose);
            Assert.Null(created.Record.SpeedrunTemplateId);
            Assert.False(vm.HasRuntimePatchesEnvironment);
            Assert.False(vm.HasSpeedrunReminder);
            Assert.Equal(vm.Loc["SpeedrunCustomReady"], vm.SelectedSpeedrunTechnicalStatus);
            Assert.Single(vm.SpeedrunSourceInstances);
            await vm.BindSpeedrunLiveSplitAsync(created.Record, lss);
            Assert.Null(vm.ErrorMessage);
            Assert.Equal(lss, vm.SpeedrunLiveSplitPath);

            // A duplicate destination must not replace either instance or its binding.
            vm.SpeedrunEnvironmentName = "速通练习";
            await vm.CreateSpeedrunEnvironmentCommand.ExecuteAsync(null);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal(createdId, Assert.Single(vm.Instances.SpeedrunInstances).Id);
            Assert.Equal(lss, (await InstanceSidecar.LoadAsync(created.Record.RootPath))!.SpeedrunLiveSplitPath);
            vm.ErrorMessage = null;

            var window = new MainWindow { Width = 1100, Height = 720 };
            try
            {
                window.Show();
                window.DataContext = vm;
                vm.Settings.SelectedMotionPreference = new(UiMotionPreference.Off, vm.Loc["MotionOff"]);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(window.FindControl<Grid>("SpeedrunLiveSplitBinding")!.IsEffectivelyVisible);
                Assert.True(window.FindControl<Border>("SpeedrunEnvironmentRail")!.IsEffectivelyVisible);
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text =>
                    text.IsEffectivelyVisible && (text.Text == vm.Loc["SpeedrunTitle"] || text.Text == vm.Loc["SpeedrunVerifyAndLaunch"]));
                if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is { Length: > 0 } output)
                {
                    Directory.CreateDirectory(output);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                    using var frame = window.GetLastRenderedFrame();
                    Assert.NotNull(frame);
                    frame.Save(Path.Combine(output, "silksong-speedrun.png"), new PngBitmapEncoderOptions());
                }
            }
            finally { window.Close(); }


        }
        await using var reopened = new MainViewModel(app);
        await InitializeAsync(reopened);
        var restored = Assert.Single(reopened.Instances.SpeedrunInstances);
        Assert.Equal(createdId, restored.Id);
        reopened.SelectedSpeedrunInstance = restored;
        Assert.Equal(lss, reopened.SpeedrunLiveSplitPath);
    }

    [AvaloniaFact]
    public async Task Game_switch_restores_searches_directories_and_filters_after_restart()
    {
        var hk = Directory.CreateDirectory(Path.Combine(root, "hk")).FullName;
        var silk = Directory.CreateDirectory(Path.Combine(root, "silk")).FullName;
        var app = Path.Combine(root, "app");
        await CrystalflySettingsStore.SaveAsync(Path.Combine(app, "settings.json"), new()
        {
            OfflineMode = true, CheckForUpdates = false, OnboardingCompleted = true, VersionRoot = hk,
            GameDirectories = [new() { Path = hk, DisplayName = "HK" }, new() { GameId = GameId.Silksong, Path = silk, DisplayName = "Silk" }],
            GameWorkspaces = [new() { VersionRoot = hk }, new() { GameId = GameId.Silksong, VersionRoot = silk }]
        });
        await using (var vm = new MainViewModel(app))
        {
            await InitializeAsync(vm);
            vm.SearchText = "hk-search";
            vm.MarketSearchText = "hk-mod";
            vm.DownloadBuildSearchText = "1.5.78";
            await vm.SelectGameCommand.ExecuteAsync("Silksong");
            Assert.True(vm.IsSilksong);
            Assert.Equal(silk, vm.VersionRoot);
            Assert.Equal("Silk", Assert.Single(vm.Instances.GameDirectories).DisplayName);
            Assert.True(vm.IsSpeedrunEnvironmentTab);
            Assert.DoesNotContain(vm.DownloadBuilds, item => item.BuildId.StartsWith("1.5.", StringComparison.Ordinal));
            vm.SearchText = "silk-search";
            vm.Thunderstore.SearchText = "SilkMod";
            vm.Thunderstore.RestoreFilters(null, 2, true);
            await vm.SelectGameCommand.ExecuteAsync("HollowKnight");
            Assert.Equal(hk, vm.VersionRoot);
            Assert.Equal("hk-search", vm.SearchText);
            Assert.Equal("hk-mod", vm.MarketSearchText);
            Assert.Equal("1.5.78", vm.DownloadBuildSearchText);
            await vm.SelectGameCommand.ExecuteAsync("Silksong");
            Assert.Equal("silk-search", vm.SearchText);
            Assert.Equal("SilkMod", vm.Thunderstore.SearchText);
            Assert.Equal(2, vm.Thunderstore.SortIndex);
            Assert.True(vm.Thunderstore.ShowDeprecated);
        }
        await using var reopened = new MainViewModel(app);
        await InitializeAsync(reopened);
        Assert.True(reopened.IsSilksong);
        Assert.Equal(silk, reopened.VersionRoot);
        Assert.Equal("SilkMod", reopened.Thunderstore.SearchText);
        Assert.Equal(2, reopened.Thunderstore.SortIndex);
    }

    [AvaloniaFact]
    public async Task Empty_managed_download_library_is_allowed_and_unsaved_edit_blocks_game_switch()
    {
        var app = Path.Combine(root, "app");
        await CrystalflySettingsStore.SaveAsync(Path.Combine(app, "settings.json"), new() { OfflineMode = true, CheckForUpdates = false });
        await using var vm = new MainViewModel(app);
        await InitializeAsync(vm);
        await vm.SelectGameCommand.ExecuteAsync("Silksong");
        var library = Directory.CreateDirectory(Path.Combine(root, "new-library")).FullName;
        await vm.Instances.UseDownloadDirectoryAsync(library);
        Assert.Equal(library, vm.VersionRoot);
        Assert.Equal(GameId.Silksong, Assert.Single(vm.Instances.GameDirectories).Registration.GameId);
        vm.SaveEditor = new SaveEditorViewModel(new NamedSnapshotService(root, "Crystalfly.UI." + Guid.NewGuid().ToString("N"), new Idle()), "test", null, "test") { IsDirty = true };
        await vm.SelectGameCommand.ExecuteAsync("HollowKnight");
        Assert.True(vm.IsSilksong);
        Assert.Equal(vm.Loc["SaveBeforeGameSwitch"], vm.ErrorMessage);
    }

    [Theory]
    [InlineData("playerData.geo", "念珠")]
    [InlineData("playerData.silk", "当前丝线")]
    [InlineData("playerData.ShellShards", "贝壳碎片")]
    [InlineData("playerData.gotCharm_1", "playerData.gotCharm_1")]
    [InlineData("modData.silk", "modData.silk")]
    public void Silksong_fields_use_their_own_dictionary_and_keep_raw_names_in_English(string path, string label)
    {
        var entry = new SaveEntryViewModel(new(path, "5", SaveEntry.KindNumber), GameId.Silksong);
        Assert.Equal(label, entry.DisplayName);
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.English);
        entry.RefreshLocalization(loc);
        Assert.Equal(path, entry.DisplayName);
        Assert.Equal("5", entry.ToEntry().Value);
    }

    [Fact]
    public async Task Corrupt_save_is_readonly_with_no_stale_editable_fields()
    {
        var saves = Directory.CreateDirectory(Path.Combine(root, "instances/test/local-low")).FullName;
        await File.WriteAllTextAsync(Path.Combine(saves, "user1.dat"), "corrupt");
        var editor = new SaveEditorViewModel(new NamedSnapshotService(root, "Crystalfly.UI." + Guid.NewGuid().ToString("N"), new Idle(), GameId.Silksong),
            "test", null, "test", gameId: GameId.Silksong);
        await Assert.ThrowsAnyAsync<Exception>(() => editor.InitializeAsync());
        Assert.True(editor.IsReadOnly);
        Assert.True(editor.IsLoaded);
        Assert.False(editor.CanSave);
        Assert.Empty(editor.Entries);
    }

    private static async Task InitializeAsync(MainViewModel viewModel)
    {
        await viewModel.InitializeAsync();
        // Initialization deliberately returns before the catalog refresh finishes.
        var field = typeof(MainViewModel).GetField("catalogRefreshTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((Task)field.GetValue(viewModel)!).WaitAsync(TimeSpan.FromSeconds(10));
        await viewModel.WaitForBackgroundAppearanceRefreshAsync();
    }

    private sealed class Idle : IHollowKnightProcessProbe { public bool IsRunning() => false; }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
