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
    public async Task Mod_removal_confirmation_executes_only_displayed_instance_and_mods(
        bool bulk, string selection, string action)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceDetail);
        var viewModel = fixture.ViewModel;
        viewModel.CurrentManageTab = "Mods";
        var original = fixture.Instance.Record;
        var current = original with { Id = "current", Name = "Current", RootPath = Path.Combine(viewModel.VersionRoot, "current") };
        await PrepareAsync(original);
        await PrepareAsync(current);
        SetPrivateField(viewModel, "instanceDiscovery",
            (Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>)
                ((_, _, _) => Task.FromResult<IReadOnlyList<InstanceRecord>>([original, current])));
        var originalManager = Manager(original);
        var currentManager = Manager(current);
        var originalReceipts = await originalManager.GetInstalledAsync();
        var currentReceipts = CrystalflyJson.Serialize(await currentManager.GetInstalledAsync());
        var sourceItem = fixture.Instance with { ModCount = 3 };
        viewModel.Instances.Instances[viewModel.Instances.Instances.IndexOf(fixture.Instance)] = sourceItem;
        viewModel.Instances.VisibleInstances[viewModel.Instances.VisibleInstances.IndexOf(fixture.Instance)] = sourceItem;
        viewModel.SelectedInstance = sourceItem;
        await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var mods = viewModel.ModManagement;
        Assert.Equal(3, mods.InstalledMods.Count);
        var targets = bulk ? new[] { "local-A", "local-B" } : ["local-A"];
        mods.SelectedInstalledMod = mods.InstalledMods.Single(mod => mod.Id == targets[0]);
        foreach (var mod in mods.InstalledMods)
        {
            mod.IsSelected = bulk && targets.Contains(mod.Id);
        }
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName() == viewModel.Loc["Uninstall"]
                && (bulk ? button.DataContext == viewModel
                    : button.DataContext is InstalledModItemViewModel { Id: "local-A" })));
        DependencyPlanDialogView? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<DependencyPlanDialogView>().SingleOrDefault();
        }
        Assert.NotNull(dialog);
        var dialogModel = Assert.IsType<DependencyPlanDialogViewModel>(dialog.DataContext);
        Assert.Equal(targets, dialogModel.Nodes.Select(node => node.ModId).Order().ToArray());
        Assert.All(dialogModel.Nodes, node => Assert.Equal(viewModel.Loc["WillDelete"], node.Action));
        if (selection is "other" or "none")
        {
            var item = new InstanceItemViewModel(current, current.BuildId, "Modding API", 3);
            viewModel.Instances.Instances.Add(item);
            viewModel.Instances.VisibleInstances.Add(item);
            viewModel.SelectedInstance = selection == "none" ? null : item;
            await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        if (selection != "same")
        {
            mods.SelectedInstalledMod = mods.InstalledMods.SingleOrDefault(mod => mod.Id == "local-C");
            foreach (var mod in mods.InstalledMods)
            {
                mod.IsSelected = mod.Id == "local-C";
            }
        }
        var selectedId = viewModel.SelectedInstance?.Id;
        var selectedMod = mods.SelectedInstalledMod;
        mods.UnusedDependencySuggestions.Add("Later instance feedback");
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
                    new ButtonAutomationPeer(button).GetName() == viewModel.Loc[action == "cancel" ? "Cancel" : "Uninstall"]));
            }
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
                if (attempt >= 20 && !viewModel.IsBusy
                    && (action != "confirm" || callbackException is not null || viewModel.ErrorMessage is not null
                        || (await originalManager.GetInstalledAsync()).Count < 3
                        || (await currentManager.GetInstalledAsync()).Count < 3))
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
        foreach (var receipt in await currentManager.GetInstalledAsync())
        {
            Assert.Equal("current-" + receipt.Name, await File.ReadAllTextAsync(Path.Combine(current.RootPath, receipt.Files.Single().RelativePath)));
        }
        var expectedIds = originalReceipts.Select(mod => mod.Id)
            .Where(id => action != "confirm" || !targets.Contains(id)).Order().ToArray();
        Assert.Equal(expectedIds, (await originalManager.GetInstalledAsync()).Select(mod => mod.Id).Order().ToArray());
        foreach (var receipt in originalReceipts)
        {
            var path = Path.Combine(original.RootPath, receipt.Files.Single().RelativePath);
            Assert.Equal(expectedIds.Contains(receipt.Id), File.Exists(path));
            if (File.Exists(path))
            {
                Assert.Equal(original.Id + "-" + receipt.Name, await File.ReadAllTextAsync(path));
            }
        }
        Assert.Equal(selectedId, viewModel.SelectedInstance?.Id);
        Assert.Equal(expectedIds.Length, Assert.Single(viewModel.Instances.Instances, item => item.Id == original.Id).ModCount);
        if (selection is "other" or "none")
        {
            Assert.Same(selectedMod, mods.SelectedInstalledMod);
            Assert.Equal(["Later instance feedback"], mods.UnusedDependencySuggestions.ToArray());
        }

        Task DetailsTask() => Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
            .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel));

        ModManager Manager(InstanceRecord record) => Assert.IsType<ModManager>(typeof(MainViewModel)
            .GetMethod("CreateModManager", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(viewModel, [record]));

        async Task PrepareAsync(InstanceRecord record)
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
            var packageRoot = Path.Combine(viewModel.VersionRoot, "packages", record.Id);
            Directory.CreateDirectory(packageRoot);
            foreach (var name in new[] { "A", "B", "C" })
            {
                var path = Path.Combine(packageRoot, name + ".dll");
                await File.WriteAllTextAsync(path, record.Id + "-" + name);
                await Manager(record).ImportLocalDllAsync("local-" + name, name, "modding-api-77", path);
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
