using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese, false, false)]
    [InlineData(UiLanguage.SimplifiedChinese, false, true)]
    [InlineData(UiLanguage.SimplifiedChinese, true, false)]
    [InlineData(UiLanguage.SimplifiedChinese, true, true)]
    [InlineData(UiLanguage.English, false, false)]
    [InlineData(UiLanguage.English, false, true)]
    [InlineData(UiLanguage.English, true, false)]
    [InlineData(UiLanguage.English, true, true)]
    public async Task Preset_confirmation_keeps_displayed_source_when_selection_changes(
        UiLanguage language, bool delete, bool clearSelection)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.ModPresets);
        var viewModel = fixture.ViewModel;
        viewModel.Loc.Apply(language);
        var source = Assert.IsType<Crystalfly.Core.Models.ModPreset>(viewModel.SelectedPreset);
        var other = Assert.Single(viewModel.ModPresets, preset => preset.Id != source.Id);
        var presetsRoot = Path.Combine(viewModel.VersionRoot, ".crystalfly", "instances",
            viewModel.SelectedInstance!.Id, "presets");
        Directory.CreateDirectory(presetsRoot);
        foreach (var preset in viewModel.ModPresets)
        {
            await File.WriteAllTextAsync(PresetPath(preset.Id), CrystalflyJson.Serialize(preset));
        }
        var otherDocument = await File.ReadAllTextAsync(PresetPath(other.Id));
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName() == viewModel.Loc[delete ? "DeletePreset" : "CopyPreset"]));
        UserControl? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<UserControl>().SingleOrDefault(control =>
                delete ? control is ConfirmationDialogView : control is TextInputDialogView);
        }
        Assert.NotNull(dialog);
        if (!delete)
        {
            Assert.Single(dialog.GetVisualDescendants().OfType<TextBox>()).Text = "Confirmed copy";
        }
        viewModel.SelectedPreset = clearSelection ? null : other;
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsBusy);
        Assert.NotNull(viewModel.SelectedInstance);
        object? confirmationResult = null;
        ((Irihi.Avalonia.Shared.Contracts.IDialogContext)dialog.DataContext!).RequestClose +=
            (_, result) => confirmationResult = result;
        var confirm = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button =>
            AutomationProperties.GetName(button) == viewModel.Loc["Confirm"]);
        Assert.True(confirm.IsEffectivelyEnabled);
        Click(confirm);
        if (delete)
        {
            Assert.Equal(true, confirmationResult);
        }
        else
        {
            Assert.Equal("Confirmed copy", confirmationResult);
        }
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            if (!fixture.Window.GetVisualDescendants().Contains(dialog) && !viewModel.IsBusy
                && (Directory.GetFiles(presetsRoot, "*.json").Length == (delete ? 1 : 3)
                    || viewModel.ErrorMessage is not null))
            {
                break;
            }
        }
        Assert.DoesNotContain(dialog, fixture.Window.GetVisualDescendants());
        Assert.Null(viewModel.ErrorMessage);
        Assert.True(File.Exists(PresetPath(other.Id)), "Confirmation must not delete the later selection.");
        Assert.Equal(otherDocument, await File.ReadAllTextAsync(PresetPath(other.Id)));
        Assert.Equal(!delete, File.Exists(PresetPath(source.Id)));
        Assert.Equal(delete ? 1 : 3, Directory.GetFiles(presetsRoot, "*.json").Length);
        if (!delete)
        {
            var copied = Assert.Single(viewModel.ModPresets, preset => preset.Name == "Confirmed copy");
            Assert.Equal(source.Entries.Select(entry => entry.Id), copied.Entries.Select(entry => entry.Id));
        }

        string PresetPath(string id) => Path.Combine(presetsRoot,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))) + ".json");

        void Click(Button button)
        {
            button.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = Assert.IsType<Point>(button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
            fixture.Window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            fixture.Window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.English)]
    public async Task Overlay_close_button_is_localized_and_cancels_without_saving(UiLanguage language)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.ModPresets);
        var viewModel = fixture.ViewModel;
        viewModel.Loc.Apply(language);
        var originalPresets = viewModel.ModPresets.ToArray();
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName() == viewModel.Loc["CopyPreset"]));
        TextInputDialogView? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<TextInputDialogView>().SingleOrDefault();
        }
        Assert.NotNull(dialog);
        var close = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && button.Name == "PART_CloseButton");
        Assert.Equal(viewModel.Loc["WindowClose"], new ButtonAutomationPeer(close).GetName());
        Assert.Equal(viewModel.Loc["WindowClose"], ToolTip.GetTip(close));
        viewModel.Loc.Apply(language == UiLanguage.English ? UiLanguage.SimplifiedChinese : UiLanguage.English);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(viewModel.Loc["WindowClose"], new ButtonAutomationPeer(close).GetName());
        Assert.Equal(viewModel.Loc["WindowClose"], ToolTip.GetTip(close));
        Click(close);
        for (var attempt = 0; attempt < 100 && fixture.Window.GetVisualDescendants().Contains(dialog); attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.DoesNotContain(dialog, fixture.Window.GetVisualDescendants());
        Assert.Equal(originalPresets, viewModel.ModPresets);
        Assert.Null(viewModel.ErrorMessage);

        void Click(Button button)
        {
            button.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = Assert.IsType<Point>(button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
            fixture.Window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            fixture.Window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese, false)]
    [InlineData(UiLanguage.SimplifiedChinese, true)]
    [InlineData(UiLanguage.English, false)]
    [InlineData(UiLanguage.English, true)]
    public async Task Preset_copy_dialog_confirms_or_cancels_at_minimum_window_size(UiLanguage language, bool confirm)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.ModPresets);
        var viewModel = fixture.ViewModel;
        viewModel.Loc.Apply(language);
        var source = Assert.IsType<Crystalfly.Core.Models.ModPreset>(viewModel.SelectedPreset);
        var presetsRoot = Path.Combine(viewModel.VersionRoot, ".crystalfly", "instances",
            viewModel.SelectedInstance!.Id, "presets");
        Directory.CreateDirectory(presetsRoot);
        foreach (var preset in viewModel.ModPresets)
        {
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preset.Id))) + ".json";
            await File.WriteAllTextAsync(Path.Combine(presetsRoot, name), CrystalflyJson.Serialize(preset));
        }
        var originalFiles = Directory.GetFiles(presetsRoot, "*.json").ToDictionary(
            path => Path.GetFileName(path)!, File.ReadAllText);
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        var copy = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName() == viewModel.Loc["CopyPreset"]);
        Click(copy);
        TextInputDialogView? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<TextInputDialogView>().SingleOrDefault();
        }
        Assert.NotNull(dialog);
        var input = Assert.Single(dialog.GetVisualDescendants().OfType<TextBox>());
        Assert.Equal($"{source.Name} - {viewModel.Loc["CopySuffix"]}", input.Text);
        input.Text = "  Practice copy  ";
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        var submit = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button =>
            AutomationProperties.GetName(button) == viewModel.Loc[confirm ? "Confirm" : "Cancel"]);
        Assert.True(submit.IsEffectivelyEnabled);
        Click(submit);
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            if (!fixture.Window.GetVisualDescendants().Contains(dialog) && !viewModel.IsBusy
                && (!confirm || viewModel.ModPresets.Count == 3))
            {
                break;
            }
        }
        Assert.DoesNotContain(dialog, fixture.Window.GetVisualDescendants());
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(confirm ? 3 : 2, Directory.GetFiles(presetsRoot, "*.json").Length);
        foreach (var original in originalFiles)
        {
            Assert.Equal(original.Value, await File.ReadAllTextAsync(Path.Combine(presetsRoot, original.Key!)));
        }
        if (confirm)
        {
            var copied = Assert.Single(viewModel.ModPresets, preset => preset.Name == "Practice copy");
            Assert.Equal(source.Entries.Select(entry => entry.Id), copied.Entries.Select(entry => entry.Id));
            Assert.Equal(source.ApplyMode, copied.ApplyMode);
            Assert.Equal(copied.Id, viewModel.SelectedPreset?.Id);
        }

        void Click(Button button)
        {
            button.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = Assert.IsType<Point>(button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
            Assert.InRange(center.X, 0, fixture.Window.Bounds.Width);
            Assert.InRange(center.Y, 0, fixture.Window.Bounds.Height);
            fixture.Window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            fixture.Window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
