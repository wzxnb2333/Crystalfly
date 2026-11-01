using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(GameId.HollowKnight)]
    [InlineData(GameId.Silksong)]
    public async Task Instance_multi_selection_keeps_navigation_stable_and_confirms_exact_targets(GameId game)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceSelection);
        var model = fixture.ViewModel;
        var items = new[] { "日常游玩", "模组练习", "保留的实例" }.Select((name, index) => fixture.Instance with
        {
            Record = fixture.Instance.Record with { Id = $"bulk-{index}", Name = name, GameId = game },
            DisplayVersion = game == GameId.HollowKnight ? "1.5.78.11833" : "1.0.30000"
        }).ToArray();
        model.Instances.Instances.Clear();
        foreach (var item in items) model.Instances.Instances.Add(item);
        model.Instances.ApplyInstanceFilter();
        model.SelectedInstance = items[0];
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        Application.Current!.RequestedThemeVariant = game == GameId.HollowKnight ? ThemeVariant.Dark : ThemeVariant.Light;
        fixture.Window.Show();
        fixture.Window.DataContext = model;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();

        Click(Find(model.Loc["SelectMultiple"]));
        Assert.True(model.Instances.IsSelectingInstances);
        Assert.Empty(model.Instances.SelectedInstances);
        foreach (var item in items[..2])
        {
            var button = fixture.Window.GetVisualDescendants().OfType<Button>().Single(button =>
                button.Classes.Contains("cfp-instance-main") && ReferenceEquals(button.DataContext, item));
            Click(button);
        }
        Assert.Same(items[0], model.SelectedInstance);
        Assert.Equal("Versions", model.CurrentPage);
        Assert.Equal(items[..2], model.Instances.SelectedInstances);
        var list = Assert.Single(fixture.Window.GetVisualDescendants().OfType<ListBox>(), box => box.Classes.Contains("cfp-instance-list"));
        var checkboxes = list.GetVisualDescendants().OfType<CheckBox>().ToArray();
        Assert.Equal(3, checkboxes.Length);
        Assert.Equal(2, checkboxes.Count(box => box.IsChecked == true));
        Assert.All(checkboxes, box => Assert.True(box.IsEffectivelyVisible));
        Capture($"instances-multi-{game}");

        Click(Find(model.Loc["DeleteSelectedInstances"]));
        ConfirmationDialogView? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<ConfirmationDialogView>().SingleOrDefault();
        }
        Assert.NotNull(dialog);
        var confirmation = Assert.IsType<ConfirmationDialogViewModel>(dialog.DataContext);
        Assert.True(confirmation.IsDangerous);
        Assert.Contains(items[0].Name, confirmation.Target);
        Assert.Contains(items[1].Name, confirmation.Target);
        Assert.DoesNotContain(items[2].Name, confirmation.Target);
        Capture($"instances-delete-confirm-{game}");
        confirmation.CancelCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, model.Instances.Instances.Count);
        Assert.Equal(items[..2], model.Instances.SelectedInstances);

        Click(Find(model.Loc["ClearSelection"]));
        Assert.False(Find(model.Loc["DeleteSelectedInstances"]).IsEnabled);
        Click(Find(model.Loc["SelectMultiple"]));
        Assert.False(model.Instances.IsSelectingInstances);
        Assert.Same(items[0], model.SelectedInstance);

        Button Find(string name) => Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && AutomationProperties.GetName(button) == name);

        void Click(Button button)
        {
            fixture.Window.UpdateLayout();
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window)!.Value;
            fixture.Window.MouseMove(point);
            fixture.Window.MouseDown(point, MouseButton.Left);
            fixture.Window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        void Capture(string name)
        {
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is not { Length: > 0 } output) return;
            Directory.CreateDirectory(output);
            fixture.Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            using var frame = fixture.Window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
        }
    }
}
