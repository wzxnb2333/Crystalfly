using System.IO.Compression;
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
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData("other", "confirm")]
    [InlineData("none", "confirm")]
    [InlineData("same", "confirm")]
    [InlineData("version-root", "confirm")]
    [InlineData("other", "cancel")]
    [InlineData("other", "close")]
    [InlineData("other", "data-context")]
    public async Task Loader_uninstall_confirmation_preserves_displayed_target_and_later_selection(
        string selection, string action)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceDetail);
        var viewModel = fixture.ViewModel;
        var original = fixture.Instance.Record;
        var sourceRoot = viewModel.VersionRoot;
        SetPrivateField(viewModel, "sharedLocalLowPathOverride", Path.Combine(sourceRoot, "shared-local-low"));
        var current = original with { Id = "current", Name = "Current", RootPath = Path.Combine(sourceRoot, "current") };
        var alternate = original with { RootPath = Path.Combine(sourceRoot, "alternate", original.Id) };
        var records = new[] { original, current, alternate };
        foreach (var record in records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssemblyPath(record))!);
            await File.WriteAllTextAsync(Path.Combine(record.RootPath, "hollow_knight.exe"), "fixture");
            await File.WriteAllTextAsync(AssemblyPath(record), "vanilla");
            await File.WriteAllTextAsync(Path.Combine(record.RootPath, "keep.txt"), record.RootPath);
            await InstanceSidecar.SaveAsync(record);
            var package = Path.Combine(record.RootPath, "loader.zip");
            using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("Assembly-CSharp.dll").Open());
                writer.Write("patched");
            }
            var manifest = new LoaderManifest
            {
                Id = "modding-api-77", Name = "Modding API", Version = "77",
                DownloadUrl = "https://example.invalid/loader.zip",
                Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(package))),
                SizeBytes = new FileInfo(package).Length, SupportedBuildIds = [record.BuildId]
            };
            await Manager(record).InstallFromFileAsync(manifest, package);
        }
        var receipts = records.ToDictionary(record => record.RootPath, record => File.ReadAllBytes(ReceiptPath(record)));
        SetPrivateField(viewModel, "instanceDiscovery",
            (Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>)
                ((_, _, _) => Task.FromResult<IReadOnlyList<InstanceRecord>>([original, current])));
        viewModel.SelectedInstance = null;
        viewModel.SelectedInstance = fixture.Instance;
        await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        viewModel.CurrentManageTab = "Loader";
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName() == viewModel.Loc["Uninstall"]));
        ConfirmationDialogView? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<ConfirmationDialogView>().SingleOrDefault();
        }
        Assert.NotNull(dialog);
        Assert.Equal(original.Name, Assert.IsType<ConfirmationDialogViewModel>(dialog.DataContext).Target);
        if (selection != "same")
        {
            var record = selection == "version-root" ? alternate : current;
            if (selection == "version-root") viewModel.VersionRoot = Path.GetDirectoryName(alternate.RootPath)!;
            var item = new InstanceItemViewModel(record, record.BuildId, "Modding API", 0);
            viewModel.Instances.Instances.Add(item);
            viewModel.Instances.VisibleInstances.Add(item);
            viewModel.SelectedInstance = selection == "none" ? null : item;
            await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        var selectedRecord = viewModel.SelectedInstance?.Record;
        Exception? callbackException = null;
        void CaptureException(object? sender, DispatcherUnhandledExceptionEventArgs args)
        {
            callbackException = args.Exception;
            args.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += CaptureException;
        try
        {
            if (action == "close") fixture.Window.Close();
            else
            {
                if (action == "data-context") fixture.Window.DataContext = null;
                Click(Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button =>
                    new ButtonAutomationPeer(button).GetName() == viewModel.Loc[action == "cancel" ? "Cancel" : "Confirm"]));
            }
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
                if (attempt >= 20 && !viewModel.IsBusy) break;
            }
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= CaptureException;
        }
        Assert.Null(callbackException);
        await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.ErrorMessage is null, viewModel.ErrorMessage);
        foreach (var record in records)
        {
            var removed = action == "confirm" && record == original;
            Assert.Equal(removed ? "vanilla" : "patched", await File.ReadAllTextAsync(AssemblyPath(record)));
            Assert.Equal(!removed, File.Exists(ReceiptPath(record)));
            if (!removed) Assert.Equal(receipts[record.RootPath], await File.ReadAllBytesAsync(ReceiptPath(record)));
            Assert.Equal(record.RootPath, await File.ReadAllTextAsync(Path.Combine(record.RootPath, "keep.txt")));
        }
        Assert.Equal(selectedRecord, viewModel.SelectedInstance?.Record);
        Assert.False(viewModel.IsBusy);
        if (action == "confirm")
        {
            var refreshed = Assert.Single(viewModel.Instances.Instances, item => item.Record == original);
            Assert.Equal(LoaderState.Vanilla.ToString(), refreshed.LoaderDisplay);
            Assert.Equal(0, refreshed.ModCount);
        }

        string AssemblyPath(InstanceRecord record) => Path.Combine(record.RootPath, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll");
        string ReceiptPath(InstanceRecord record) => Path.Combine(Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(record.RootPath, record.Id))!, "loader.json");
        LoaderManager Manager(InstanceRecord record) => new(record.RootPath,
            Path.Combine(Path.GetDirectoryName(record.RootPath)!, ".crystalfly", "transactions"), ReceiptPath(record));
        Task DetailsTask() => Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
            .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel));
        void Click(Button button)
        {
            button.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var center = Assert.IsType<Point>(button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
            fixture.Window.MouseMove(center);
            fixture.Window.MouseDown(center, MouseButton.Left);
            fixture.Window.MouseUp(center, MouseButton.Left);
        }
    }
}
