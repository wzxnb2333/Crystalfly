using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Crystalfly.App.Services;
using Crystalfly.App.Views;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [Trait("Category", "VisualScan")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Soft_motion_reference_frames_use_native_pages_and_dialog(bool dark)
    {
        if (Environment.GetEnvironmentVariable("CRYSTALFLY_MOTION_FRAMES") is not { Length: > 0 } output) return;
        var previous = MainWindow.ClientAreaAnimationsOverride;
        MainWindow.ClientAreaAnimationsOverride = () => true;
        try
        {
            await using var fixture = CreateFixture();
            await fixture.PrepareAsync(ScreenshotState.Launch);
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            fixture.Window.Width = 1100;
            fixture.Window.Height = 720;
            fixture.Window.Show();
            fixture.Window.DataContext = fixture.ViewModel;
            Dispatcher.UIThread.RunJobs();
            var coordinator = (MotionCoordinator)typeof(MainWindow).GetField("motionCoordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Window)!;
            var active = (IDictionary)typeof(MotionCoordinator).GetField("activeEntranceAnimations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(coordinator)!;
            var tick = typeof(MotionCoordinator).GetMethod("OnEntranceAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await Task.Delay(500);
            fixture.ViewModel.CurrentPage = "Versions";
            Capture("instances");
            fixture.ViewModel.CurrentPage = "Settings";
            Capture("settings");
            await fixture.OpenDependencyGraphAsync();
            Capture("dialog");

            void Capture(string scene)
            {
                Dispatcher.UIThread.RunJobs();
                fixture.Window.UpdateLayout();
                Assert.NotEmpty(active);
                var states = active.Values.Cast<object>().ToArray();
                var directory = Path.Combine(output, dark ? "dark" : "light", scene);
                Directory.CreateDirectory(directory);
                var hashes = new HashSet<string>();
                // Sample the real frame driver at 60 fps. This checks choreography, not GPU performance.
                for (var frame = 0; frame < 45; frame++)
                {
                    foreach (var state in states)
                    {
                        state.GetType().GetProperty("HasStarted")!.SetValue(state, true);
                        state.GetType().GetProperty("StartedTimestamp")!.SetValue(state,
                            Stopwatch.GetTimestamp() - (long)(frame / 60d * Stopwatch.Frequency));
                    }
                    tick.Invoke(coordinator, [TimeSpan.Zero]);
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                    Dispatcher.UIThread.RunJobs();
                    using var bitmap = fixture.Window.GetLastRenderedFrame();
                    Assert.NotNull(bitmap);
                    var path = Path.Combine(directory, $"{frame:D3}.png");
                    bitmap.Save(path, new PngBitmapEncoderOptions());
                    hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
                }
                Assert.True(hashes.Count > 8, $"{scene}: render capture did not advance through the animation ({hashes.Count} distinct frames).");
                Assert.Empty(active);
            }
        }
        finally { MainWindow.ClientAreaAnimationsOverride = previous; }
    }
}
