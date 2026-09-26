using System.Reflection;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
    [InlineData("other", "confirm")]
    [InlineData("none", "confirm")]
    [InlineData("changed", "confirm")]
    [InlineData("same", "confirm")]
    [InlineData("version-root", "confirm")]
    [InlineData("other", "cancel")]
    [InlineData("other", "close")]
    [InlineData("other", "data-context")]
    public async Task Global_mod_settings_confirmation_deletes_only_displayed_target(
        string selection, string action)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceDetail);
        var viewModel = fixture.ViewModel;
        var original = fixture.Instance.Record;
        var sourceRoot = viewModel.VersionRoot;
        var currentRoot = Path.Combine(sourceRoot, "other-root");
        var current = original with { Id = "current", RootPath = Path.Combine(sourceRoot, "current") };
        var first = fixture.Mod;
        var second = first with { Id = "hkmod:Second", Name = "Second", DisplayName = "Second" };
        SetPrivateField(viewModel, "sharedLocalLowPathOverride", Path.Combine(sourceRoot, "shared-local-low"));
        var files = new Dictionary<string, string>();
        foreach (var root in new[] { sourceRoot, currentRoot })
        {
            foreach (var instance in new[] { original, current })
            {
                var directory = Path.Combine(root, ".crystalfly", "instances", instance.Id, "local-low");
                Directory.CreateDirectory(directory);
                foreach (var mod in new[] { first, second })
                {
                    foreach (var suffix in new[] { string.Empty, ".bak" })
                    {
                        var path = Path.Combine(directory, mod.Name + ".GlobalSettings.json" + suffix);
                        var content = "{\"fixture\":\"" + instance.Id + "-" + mod.Id + suffix + "\"}";
                        await File.WriteAllTextAsync(path, content);
                        files.Add(path, content);
                    }
                }
            }
        }
        InstallProjection();
        viewModel.SelectedMarketMod = first;
        viewModel.CurrentPage = "Downloads";
        viewModel.CurrentDownloadSection = "ModMarket";
        Assert.True(viewModel.HasSelectedModGlobalSettings);
        var targetPath = Assert.IsType<string>(viewModel.ResolveSelectedModGlobalSettingsPath());
        Assert.Equal(Path.Combine(sourceRoot, ".crystalfly", "instances", original.Id, "local-low",
            first.Name + ".GlobalSettings.json"), targetPath);
        var targetName = viewModel.SelectedMarketModDisplay!.PrimaryName;
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName() == viewModel.Loc["DeleteGlobalSettings"]));
        ConfirmationDialogView? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<ConfirmationDialogView>().SingleOrDefault();
        }
        Assert.NotNull(dialog);
        Assert.Equal(targetName, Assert.IsType<ConfirmationDialogViewModel>(dialog.DataContext).Target);
        if (selection is "other" or "none")
        {
            var item = new InstanceItemViewModel(current, current.BuildId, "Modding API", 2);
            viewModel.Instances.Instances.Add(item);
            viewModel.Instances.VisibleInstances.Add(item);
            viewModel.SelectedInstance = selection == "none" ? null : item;
            await Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
                .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel))
                .WaitAsync(TimeSpan.FromSeconds(5));
            InstallProjection();
        }
        if (selection == "version-root")
        {
            viewModel.VersionRoot = currentRoot;
        }
        if (selection is not ("same" or "version-root"))
        {
            viewModel.SelectedMarketMod = second;
        }
        var selectedId = viewModel.SelectedInstance?.Id;
        var selectedMod = viewModel.SelectedMarketMod;
        var settingsPath = viewModel.ResolveSelectedModGlobalSettingsPath();
        Assert.Null(viewModel.ErrorMessage);
        Exception? callbackException = null;
        void CaptureException(object? sender, DispatcherUnhandledExceptionEventArgs args)
        {
            callbackException = args.Exception;
            args.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += CaptureException;
        try
        {
            if (action == "close")
            {
                fixture.Window.Close();
            }
            else
            {
                if (action == "data-context")
                {
                    fixture.Window.DataContext = null;
                }
                Click(Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button =>
                    new ButtonAutomationPeer(button).GetName() == viewModel.Loc[action == "cancel" ? "Cancel" : "Confirm"]));
            }
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
                if (attempt >= 20 && (action != "confirm" || callbackException is not null
                    || viewModel.ErrorMessage is not null || !File.Exists(targetPath)))
                {
                    break;
                }
            }
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= CaptureException;
        }
        Assert.Null(callbackException);
        Assert.Null(viewModel.ErrorMessage);
        foreach (var file in files)
        {
            var shouldDelete = action == "confirm" && (file.Key == targetPath || file.Key == targetPath + ".bak");
            Assert.Equal(!shouldDelete, File.Exists(file.Key));
            if (!shouldDelete)
            {
                Assert.Equal(file.Value, await File.ReadAllTextAsync(file.Key));
            }
        }
        Assert.Equal(selectedId, viewModel.SelectedInstance?.Id);
        Assert.Same(selectedMod, viewModel.SelectedMarketMod);
        Assert.Equal(settingsPath, viewModel.ResolveSelectedModGlobalSettingsPath());
        if (action == "confirm" && selection == "same")
        {
            Assert.False(viewModel.HasSelectedModGlobalSettings);
        }

        void InstallProjection()
        {
            viewModel.ModManagement.ClearInstalledMods();
            foreach (var mod in new[] { first, second })
            {
                viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(new InstalledModReceipt
                {
                    Id = mod.Id, Name = mod.Name, Version = mod.Version, LoaderId = mod.LoaderId,
                    InstallRoot = "hollow_knight_Data/Managed/Mods/" + mod.Name
                }, mod, static () => { }));
            }
        }

        void Click(Button button)
        {
            button.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = Assert.IsType<Point>(button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
            fixture.Window.MouseMove(center);
            fixture.Window.MouseDown(center, MouseButton.Left);
            fixture.Window.MouseUp(center, MouseButton.Left);
        }
    }
}
