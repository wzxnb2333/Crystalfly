using System.Reflection;
using System.Security.Cryptography;
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
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(false, "other", "confirm")]
    [InlineData(true, "other", "confirm")]
    [InlineData(false, "none", "confirm")]
    [InlineData(true, "none", "confirm")]
    [InlineData(false, "changed", "confirm")]
    [InlineData(true, "changed", "confirm")]
    [InlineData(false, "same", "confirm")]
    [InlineData(true, "same", "confirm")]
    [InlineData(false, "other", "cancel")]
    [InlineData(true, "other", "cancel")]
    [InlineData(false, "other", "close")]
    [InlineData(true, "other", "close")]
    [InlineData(false, "other", "data-context")]
    [InlineData(true, "other", "data-context")]
    public async Task Local_mod_confirmation_preserves_displayed_target_and_window_lifetime(
        bool accept, string selection, string action)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceDetail);
        var viewModel = fixture.ViewModel;
        viewModel.CurrentManageTab = "Mods";
        var original = fixture.Instance.Record;
        var current = original with { Id = "current", Name = "Current", RootPath = Path.Combine(viewModel.VersionRoot, "current") };
        var originalFiles = await PrepareAsync(original);
        var currentFiles = await PrepareAsync(current);
        SetPrivateField(viewModel, "instanceDiscovery",
            (Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>)
                ((_, _, _) => Task.FromResult<IReadOnlyList<InstanceRecord>>([original, current])));
        var originalManager = Manager(original);
        var currentManager = Manager(current);
        var originalReceipts = await originalManager.GetInstalledAsync();
        var currentReceipts = CrystalflyJson.Serialize(await currentManager.GetInstalledAsync());
        var sourceItem = fixture.Instance with { ModCount = 2 };
        viewModel.Instances.Instances[viewModel.Instances.Instances.IndexOf(fixture.Instance)] = sourceItem;
        viewModel.Instances.VisibleInstances[viewModel.Instances.VisibleInstances.IndexOf(fixture.Instance)] = sourceItem;
        viewModel.SelectedInstance = sourceItem;
        await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var mods = viewModel.ModManagement;
        Assert.Equal(2, mods.InstalledMods.Count);
        var target = Assert.Single(mods.InstalledMods, mod => mod.Name == "A");
        Assert.True(accept ? target.CanAcceptCurrent : target.CanTakeOver);
        mods.SelectedInstalledMod = target;
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && button.DataContext == target
                && new ButtonAutomationPeer(button).GetName() == viewModel.Loc[accept ? "AcceptCurrentFiles" : "TakeOverMod"]));
        ConfirmationDialogView? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<ConfirmationDialogView>().SingleOrDefault();
        }
        Assert.NotNull(dialog);
        Assert.Equal(target.Name, Assert.IsType<ConfirmationDialogViewModel>(dialog.DataContext).Target);
        if (selection is "other" or "none")
        {
            var item = new InstanceItemViewModel(current, current.BuildId, "Modding API", 2);
            viewModel.Instances.Instances.Add(item);
            viewModel.Instances.VisibleInstances.Add(item);
            viewModel.SelectedInstance = selection == "none" ? null : item;
            await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        if (selection != "same")
        {
            mods.SelectedInstalledMod = mods.InstalledMods.SingleOrDefault(mod => mod.Name == "B");
        }
        var selectedId = viewModel.SelectedInstance?.Id;
        var selectedMod = mods.SelectedInstalledMod;
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
                if (attempt >= 20 && !viewModel.IsBusy
                    && (action != "confirm" || callbackException is not null || viewModel.ErrorMessage is not null
                        || CrystalflyJson.Serialize(await originalManager.GetInstalledAsync()) != CrystalflyJson.Serialize(originalReceipts)
                        || CrystalflyJson.Serialize(await currentManager.GetInstalledAsync()) != currentReceipts))
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
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(currentReceipts, CrystalflyJson.Serialize(await currentManager.GetInstalledAsync()));
        foreach (var file in originalFiles.Concat(currentFiles))
        {
            Assert.Equal(file.Value, await File.ReadAllTextAsync(file.Key));
        }
        var after = await originalManager.GetInstalledAsync();
        if (action == "confirm")
        {
            var receipt = Assert.Single(after, mod => mod.Id == target.Id);
            Assert.Equal(ModOwnership.LocalTakenOver, receipt.Ownership);
            var file = Assert.Single(receipt.Files);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(
                Path.Combine(original.RootPath, file.RelativePath)))), file.Sha256, ignoreCase: true);
            if (accept)
            {
                Assert.Equal(CrystalflyJson.Serialize(originalReceipts.Single(mod => mod.Name == "B")),
                    CrystalflyJson.Serialize(after.Single(mod => mod.Name == "B")));
                Assert.Equal(2, after.Count);
            }
            else
            {
                Assert.Single(after);
            }
        }
        else
        {
            Assert.Equal(CrystalflyJson.Serialize(originalReceipts), CrystalflyJson.Serialize(after));
        }
        Assert.Equal(selectedId, viewModel.SelectedInstance?.Id);
        Assert.Equal(2, Assert.Single(viewModel.Instances.Instances, item => item.Id == original.Id).ModCount);
        if (selection is "other" or "none")
        {
            Assert.Same(selectedMod, mods.SelectedInstalledMod);
        }

        Task DetailsTask() => Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
            .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel));

        ModManager Manager(InstanceRecord record) => Assert.IsType<ModManager>(typeof(MainViewModel)
            .GetMethod("CreateModManager", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(viewModel, [record]));

        async Task<Dictionary<string, string>> PrepareAsync(InstanceRecord record)
        {
            const string relative = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll";
            var loaderFile = Path.Combine(record.RootPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(loaderFile)!);
            await File.WriteAllTextAsync(Path.Combine(record.RootPath, "hollow_knight.exe"), "fixture");
            await File.WriteAllTextAsync(loaderFile, "loader");
            await InstanceSidecar.SaveAsync(record);
            await AtomicJsonStore.WriteAsync(Path.Combine(
                Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(record.RootPath, record.Id))!, "loader.json"),
                new InstalledPackageReceipt
                {
                    PackageId = "modding-api-77", LoaderState = LoaderState.ModdingApi,
                    Files = [new InstalledFileReceipt
                    {
                        RelativePath = relative, Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(loaderFile)))
                    }]
                });
            var files = new Dictionary<string, string>();
            foreach (var name in new[] { "A", "B" })
            {
                var path = Path.Combine(record.RootPath, "hollow_knight_Data", "Managed", "Mods", name, name + ".dll");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, record.Id + "-" + name);
            }
            if (accept)
            {
                foreach (var entry in (await Manager(record).DiscoverAsync("modding-api-77")).Mods)
                {
                    await Manager(record).TakeOverAsync(entry.Id, entry);
                }
            }
            foreach (var name in new[] { "A", "B" })
            {
                var path = Path.Combine(record.RootPath, "hollow_knight_Data", "Managed", "Mods", name, name + ".dll");
                var contents = record.Id + "-" + name + (accept ? "-changed" : string.Empty);
                await File.WriteAllTextAsync(path, contents);
                files.Add(path, contents);
            }
            return files;
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
