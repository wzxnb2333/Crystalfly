using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Styling;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed class DownloadDirectorySelectionTests : IDisposable
{
    private readonly string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "crystalfly-download-directory", Guid.NewGuid().ToString("N"))).FullName;

    private string CreateDirectory(string name) => Directory.CreateDirectory(Path.Combine(root, name)).FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    [AvaloniaFact]
    public async Task Download_directory_selector_uses_registered_directories_and_updates_the_download_root()
    {
        var app = CreateDirectory("app");
        var first = CreateDirectory("hk-primary");
        var second = CreateDirectory("hk-practice");
        var silksong = CreateDirectory("silksong");
        await CrystalflySettingsStore.SaveAsync(Path.Combine(app, "settings.json"), new()
        {
            OfflineMode = true,
            CheckForUpdates = false,
            OnboardingCompleted = true,
            GameDirectoryDiscoveryCompleted = true,
            VersionRoot = first,
            GameDirectories =
            [
                new() { Path = first, DisplayName = "空洞骑士", Source = GameDirectorySourceKind.Managed },
                new() { Path = second, DisplayName = "练习版本", Source = GameDirectorySourceKind.Managed },
                new() { Path = silksong, DisplayName = "丝之歌", GameId = GameId.Silksong, Source = GameDirectorySourceKind.Managed }
            ]
        });
        await using var model = new MainViewModel(app);
        await model.InitializeAsync();
        model.CurrentPage = "Downloads";
        model.CurrentDownloadSection = "GameVersions";
        var window = new MainWindow { Width = 900, Height = 600, DataContext = model };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var selector = window.FindControl<ComboBox>("DownloadGameDirectorySelector")!;
            Assert.True(selector.IsEffectivelyVisible);
            Assert.Same(model.Instances.GameDirectories, selector.ItemsSource);
            Assert.Equal(2, selector.ItemCount);
            Assert.Equal(first, Assert.IsType<GameDirectoryItemViewModel>(selector.SelectedItem).Path);
            selector.Focus(NavigationMethod.Tab);
            selector.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(selector.IsDropDownOpen);
            Assert.Equal(second, Assert.IsType<GameDirectoryItemViewModel>(selector.SelectedItem).Path);
            Assert.Equal(second, model.VersionRoot);
            Assert.Same(selector.SelectedItem, model.Instances.SelectedGameDirectory);
            Assert.Equal(2, model.Instances.GameDirectories.Count);
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is { Length: > 0 } output)
            {
                var previousTheme = Application.Current!.RequestedThemeVariant;
                try
                {
                    Directory.CreateDirectory(output);
                    foreach (var (theme, name) in new[] { (ThemeVariant.Light, "light"), (ThemeVariant.Dark, "dark") })
                    {
                        Application.Current.RequestedThemeVariant = theme;
                        Capture("download-directory-" + name);
                        selector.IsDropDownOpen = true;
                        Capture("download-directory-" + name + "-menu");
                        selector.IsDropDownOpen = false;
                    }
                }
                finally
                {
                    selector.IsDropDownOpen = false;
                    Application.Current.RequestedThemeVariant = previousTheme;
                }

                void Capture(string name)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                    using var frame = window.GetLastRenderedFrame();
                    Assert.NotNull(frame);
                    frame.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
                }
            }
            await model.SelectGameCommand.ExecuteAsync(nameof(GameId.Silksong));
            Dispatcher.UIThread.RunJobs();
            Assert.Single(selector.Items);
            Assert.Equal(silksong, Assert.IsType<GameDirectoryItemViewModel>(selector.SelectedItem).Path);
            Assert.Equal(silksong, model.VersionRoot);
            model.Instances.GameDirectories.Clear();
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(selector.Items);
            Assert.False(selector.IsEnabled);
        }
        finally
        {
            typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
            window.Close();
        }
    }
}
