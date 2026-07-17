using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(Crystalfly.App.Tests.Ui.TestApplication))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Crystalfly.App.Tests.Ui;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
                ShouldRenderOnUIThread = true
            });
}
