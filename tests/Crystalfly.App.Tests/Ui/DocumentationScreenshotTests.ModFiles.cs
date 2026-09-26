using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(false, "other", "select", true)]
    [InlineData(true, "other", "select", true)]
    [InlineData(false, "none", "select", false)]
    [InlineData(true, "none", "select", false)]
    [InlineData(false, "same", "select", false)]
    [InlineData(true, "same", "select", false)]
    [InlineData(false, "other", "cancel", false)]
    [InlineData(true, "other", "cancel", false)]
    [InlineData(false, "other", "close", false)]
    [InlineData(true, "other", "close", false)]
    [InlineData(false, "other", "io-error", false)]
    [InlineData(true, "other", "io-error", false)]
    [InlineData(false, "other", "cancel-error", false)]
    [InlineData(true, "other", "cancel-error", false)]
    [InlineData(false, "other", "invalid-package", true)]
    [InlineData(true, "other", "invalid-package", true)]
    [InlineData(false, "other", "data-context", false)]
    [InlineData(true, "other", "data-context", false)]
    [InlineData(false, "other", "close-error", false)]
    [InlineData(true, "other", "close-error", false)]
    public async Task Mod_file_picker_preserves_target_selection_and_pending_input(
        bool reimport, string selection, string action, bool zip)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceDetail);
        var viewModel = fixture.ViewModel;
        viewModel.CurrentManageTab = "Mods";
        var original = fixture.Instance.Record;
        var current = original with { Id = "current", Name = "Current", RootPath = Path.Combine(viewModel.VersionRoot, "current") };
        await PrepareInstanceAsync(original);
        await PrepareInstanceAsync(current);
        SetPrivateField(viewModel, "instanceDiscovery",
            (Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>)
                ((_, _, _) => Task.FromResult<IReadOnlyList<InstanceRecord>>([original, current])));
        var originalManager = Manager(original);
        var currentManager = Manager(current);
        var initialPath = await PackageAsync("initial", "Local", false, "original");
        var currentPath = await PackageAsync("current-package", "Local", false, "current");
        var initial = await originalManager.ImportLocalDllAsync("local-Local", "Local", "modding-api-77", initialPath);
        await currentManager.ImportLocalDllAsync("local-Local", "Local", "modding-api-77", currentPath);
        var currentReceipts = CrystalflyJson.Serialize(await currentManager.GetInstalledAsync());
        var path = await PackageAsync("submitted", reimport ? "Local" : "New", zip, "submitted");
        if (action == "invalid-package")
        {
            await File.WriteAllTextAsync(path, "not a ZIP");
        }
        var sourceItem = fixture.Instance with { ModCount = 1 };
        viewModel.Instances.Instances[viewModel.Instances.Instances.IndexOf(fixture.Instance)] = sourceItem;
        viewModel.Instances.VisibleInstances[viewModel.Instances.VisibleInstances.IndexOf(fixture.Instance)] = sourceItem;
        viewModel.SelectedInstance = sourceItem;
        await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var mods = viewModel.ModManagement;
        mods.ClearInstalledMods();
        var originalItem = new InstalledModItemViewModel(initial, null, static () => { });
        mods.InstalledMods.Add(originalItem);
        mods.VisibleInstalledMods.Add(originalItem);
        mods.SelectedInstalledMod = originalItem;
        var storage = DispatchProxy.Create<IStorageProvider, DeferredPresetPicker>();
        var picker = (DeferredPresetPicker)storage;
        Assert.Single(typeof(TopLevel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(IStorageProvider)).SetValue(fixture.Window, storage);
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        var button = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), control =>
            control.IsEffectivelyVisible && new ButtonAutomationPeer(control).GetName()
                == viewModel.Loc[reimport ? "ReimportLocalMod" : "ImportLocalMod"]);
        button.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        var center = Assert.IsType<Point>(button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
        fixture.Window.MouseMove(center);
        fixture.Window.MouseDown(center, MouseButton.Left);
        fixture.Window.MouseUp(center, MouseButton.Left);
        await picker.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (selection != "same")
        {
            var item = new InstanceItemViewModel(current, current.BuildId, "Modding API", 1);
            viewModel.Instances.Instances.Add(item);
            viewModel.Instances.VisibleInstances.Add(item);
            viewModel.SelectedInstance = selection == "none" ? null : item;
            await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        var expectedId = viewModel.SelectedInstance?.Id;
        Assert.Equal(selection == "none" ? null : selection == "same" ? original.Id : current.Id, expectedId);
        Assert.Null(viewModel.ErrorMessage);
        mods.SelectedInstalledMod = selection == "none" ? null : mods.InstalledMods.FirstOrDefault();
        var selectedMod = mods.SelectedInstalledMod;
        mods.LocalModPath = "later user input";
        var file = DispatchProxy.Create<IStorageFile, PresetPickerFile>();
        ((PresetPickerFile)file).LocalPath = path;
        Exception? callbackException = null;
        void CaptureException(object? sender, DispatcherUnhandledExceptionEventArgs args)
        {
            callbackException = args.Exception;
            args.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += CaptureException;
        try
        {
            if (action is "close" or "close-error")
            {
                fixture.Window.Close();
                for (var attempt = 0; attempt < 100 && fixture.Window.IsVisible; attempt++)
                {
                    await Task.Delay(10);
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.False(fixture.Window.IsVisible);
            }
            if (action == "data-context")
            {
                fixture.Window.DataContext = null;
            }
            if (action is "io-error" or "close-error")
            {
                picker.Response.SetException(new IOException("Picker unavailable"));
            }
            else if (action == "cancel-error")
            {
                picker.Response.SetException(new OperationCanceledException());
            }
            else
            {
                picker.Response.SetResult(action == "cancel" ? null : file);
            }
            for (var attempt = 0; attempt < 150; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
                if (attempt >= 20 && !viewModel.IsBusy
                    && (action != "select" || callbackException is not null || viewModel.ErrorMessage is not null
                        || await HasSubmittedAsync(originalManager, original) || await HasSubmittedAsync(currentManager, current)))
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
        Assert.Equal(currentReceipts, CrystalflyJson.Serialize(await currentManager.GetInstalledAsync()));
        Assert.Equal("current", await File.ReadAllTextAsync(Path.Combine(current.RootPath, initial.Files.Single().RelativePath)));
        Assert.Equal(action == "select", await HasSubmittedAsync(originalManager, original));
        Assert.Equal(action == "select" && !reimport ? 2 : 1, (await originalManager.GetInstalledAsync()).Count);
        if (action == "invalid-package")
        {
            Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        }
        else if (action == "io-error")
        {
            Assert.Contains("Picker unavailable", viewModel.ErrorMessage);
        }
        else
        {
            Assert.Null(viewModel.ErrorMessage);
        }
        Assert.Equal(expectedId, viewModel.SelectedInstance?.Id);
        Assert.Equal(action == "select" && !reimport ? 2 : 1,
            Assert.Single(viewModel.Instances.Instances, item => item.Id == original.Id).ModCount);
        if (selection != "same")
        {
            Assert.Same(selectedMod, mods.SelectedInstalledMod);
        }
        Assert.Equal("later user input", mods.LocalModPath);

        Task DetailsTask() => Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
            .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel));

        ModManager Manager(InstanceRecord record) => Assert.IsType<ModManager>(typeof(MainViewModel)
            .GetMethod("CreateModManager", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(viewModel, [record]));

        async Task<bool> HasSubmittedAsync(ModManager manager, InstanceRecord record)
        {
            var receipt = (await manager.GetInstalledAsync()).SingleOrDefault(item => item.Id == (reimport ? "local-Local" : "local-New"));
            return receipt is not null && await File.ReadAllTextAsync(Path.Combine(record.RootPath, receipt.Files.Single().RelativePath)) == "submitted";
        }

        async Task<string> PackageAsync(string directory, string name, bool archive, string content)
        {
            var root = Path.Combine(viewModel.VersionRoot, "packages", directory);
            Directory.CreateDirectory(root);
            var packagePath = Path.Combine(root, name + (archive ? ".zip" : ".dll"));
            if (archive)
            {
                using var package = ZipFile.Open(packagePath, ZipArchiveMode.Create);
                await using var writer = new StreamWriter(package.CreateEntry(name + ".dll").Open());
                await writer.WriteAsync(content);
            }
            else
            {
                await File.WriteAllTextAsync(packagePath, content);
            }
            return packagePath;
        }

        static async Task PrepareInstanceAsync(InstanceRecord record)
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
        }
    }
}
