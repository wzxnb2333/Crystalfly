using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Saves;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaFact]
    public async Task Navigation_names_follow_language_changes_without_reopening_window()
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.Settings);
        fixture.Window.Show();
        fixture.Window.DataContext = fixture.ViewModel;
        foreach (var language in new[] { UiLanguage.English, UiLanguage.SimplifiedChinese })
        {
            fixture.ViewModel.Loc.Apply(language);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var buttons = fixture.Window.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible
                    && (button.Classes.Contains("cfp-nav") || button.Classes.Contains("cfp-local-nav")))
                .ToArray();
            Assert.Equal(9, buttons.Length);
            foreach (var button in buttons)
            {
                var label = Assert.Single(button.GetVisualDescendants().OfType<TextBlock>());
                Assert.Equal(label.Text, new ButtonAutomationPeer(button).GetName());
            }
        }
    }

    [AvaloniaFact]
    public async Task Save_button_handles_invalid_value_and_saves_corrected_input()
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceDetail);
        var path = Path.Combine(fixture.ViewModel.VersionRoot, ".crystalfly", "instances",
            fixture.Instance.Id, "local-low", "user1.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await SaveFileCodec.EncryptAsync(path, "{\"health\":5}");
        var original = await File.ReadAllBytesAsync(path);
        fixture.ViewModel.CurrentManageTab = "Snapshots";
        await fixture.ViewModel.EditSaveCommand.ExecuteAsync("current");
        var editor = fixture.ViewModel.SaveEditor!;
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = fixture.ViewModel;
        Dispatcher.UIThread.RunJobs();
        var save = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(),
            button => ReferenceEquals(button.Command, editor.SaveCommand));
        var entry = Assert.Single(editor.Entries);
        entry.Value = "invalid";
        await ClickSaveAsync();
        Assert.True(fixture.Window.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(fixture.ViewModel.ErrorMessage));
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.True(editor.IsDirty);
        entry.Value = "9";
        await ClickSaveAsync();
        Assert.Null(fixture.ViewModel.ErrorMessage);
        Assert.False(editor.IsDirty);
        Assert.Equal("{\"health\":9}", await SaveFileCodec.DecryptAsync(path));

        async Task ClickSaveAsync()
        {
            Dispatcher.UIThread.RunJobs();
            save.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var point = Assert.IsType<Point>(save.TranslatePoint(
                new Point(save.Bounds.Width / 2, save.Bounds.Height / 2), fixture.Window));
            Assert.InRange(point.Y, 0, fixture.Window.Bounds.Height);
            fixture.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            fixture.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            await Assert.IsAssignableFrom<Task>(editor.SaveCommand.ExecutionTask);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
