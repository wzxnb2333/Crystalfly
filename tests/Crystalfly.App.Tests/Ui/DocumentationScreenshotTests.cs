using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed class DocumentationScreenshotTests
{
    [AvaloniaFact]
    public async Task Render_documentation_screenshots_when_requested()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("CRYSTALFLY_UPDATE_SCREENSHOTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        const string versionRoot = @"D:\HK_ver";
        Assert.True(Directory.Exists(versionRoot), $"Screenshot version root not found: {versionRoot}");
        var repositoryRoot = FindRepositoryRoot();
        var applicationData = Path.Combine(repositoryRoot, "artifacts", "ui-screenshot-data");
        Directory.CreateDirectory(applicationData);
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(applicationData, "settings.json"),
            new CrystalflySettings
            {
                VersionRoot = versionRoot,
                Language = UiLanguage.SimplifiedChinese,
                Theme = UiTheme.Dark
            });

        var viewModel = new MainViewModel(applicationData);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        await viewModel.InitializeAsync();

        try
        {
            var cases = new[]
            {
                (Width: 900, Height: 600, RenderScaling: 1d, Page: "Downloads"),
                (Width: 1280, Height: 720, RenderScaling: 1d, Page: "Launch"),
                (Width: 1920, Height: 1080, RenderScaling: 1.5d, Page: "Settings"),
                (Width: 2560, Height: 1440, RenderScaling: 2d, Page: "Launch")
            };
            var outputDirectory = Path.Combine(repositoryRoot, "docs", "screenshots");
            foreach (var capture in cases)
            {
                viewModel.CurrentPage = capture.Page;
                window.Width = capture.Width / capture.RenderScaling;
                window.Height = capture.Height / capture.RenderScaling;
                Dispatcher.UIThread.RunJobs();
                window.SetRenderScaling(capture.RenderScaling);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);

                using var frame = Assert.IsType<WriteableBitmap>(window.GetLastRenderedFrame());
                Assert.Equal(capture.Width, frame.PixelSize.Width);
                Assert.Equal(capture.Height, frame.PixelSize.Height);
                frame.Save(
                    Path.Combine(outputDirectory, $"crystalfly-{capture.Width}x{capture.Height}-zh.jpg"),
                    new JpegBitmapEncoderOptions { Quality = 92 });
            }
        }
        finally
        {
            await viewModel.DisposeAsync();
            typeof(MainWindow)
                .GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(window, true);
            window.Close();
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx")))
        {
            directory = directory.Parent;
        }

        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }
}
