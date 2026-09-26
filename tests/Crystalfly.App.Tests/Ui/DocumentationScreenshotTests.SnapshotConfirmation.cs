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
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Snapshots;

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
    [InlineData(false, "version-root", "confirm")]
    [InlineData(true, "version-root", "confirm")]
    [InlineData(false, "other", "cancel")]
    [InlineData(true, "other", "cancel")]
    [InlineData(false, "other", "close")]
    [InlineData(true, "other", "close")]
    [InlineData(false, "other", "data-context")]
    [InlineData(true, "other", "data-context")]
    public async Task Snapshot_confirmation_preserves_displayed_target_and_later_selection(
        bool restore, string selection, string action)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceDetail);
        var viewModel = fixture.ViewModel;
        var original = fixture.Instance.Record;
        var sourceRoot = viewModel.VersionRoot;
        SetPrivateField(viewModel, "sharedLocalLowPathOverride", Path.Combine(sourceRoot, "shared-local-low"));
        var alternateRoot = Path.Combine(sourceRoot, "other-root");
        var current = original with { Id = "current", Name = "Current", RootPath = Path.Combine(sourceRoot, "current") };
        foreach (var record in new[] { original, current })
        {
            Directory.CreateDirectory(record.RootPath);
            await File.WriteAllTextAsync(Path.Combine(record.RootPath, "hollow_knight.exe"), "fixture");
            await InstanceSidecar.SaveAsync(record);
        }
        SetPrivateField(viewModel, "instanceDiscovery",
            (Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>)
                ((_, _, _) => Task.FromResult<IReadOnlyList<InstanceRecord>>([original, current])));
        var snapshots = new Dictionary<(string Root, string Instance), NamedSnapshot[]>();
        var before = new Dictionary<string, string>();
        foreach (var root in new[] { sourceRoot, alternateRoot })
        {
            foreach (var record in new[] { original, current })
            {
                var localLow = LocalLow(root, record.Id);
                Directory.CreateDirectory(localLow);
                await WriteSaveAsync("A");
                var first = await Service(root).CreateAsync(record.Id, "A");
                await WriteSaveAsync("B");
                await File.WriteAllTextAsync(Path.Combine(localLow, "user2.dat"), "B-only");
                var second = await Service(root).CreateAsync(record.Id, "B");
                await WriteSaveAsync("Live");
                await File.WriteAllTextAsync(Path.Combine(localLow, "user3.dat"), "Live-only");
                await File.WriteAllTextAsync(Path.Combine(localLow, "Mod.GlobalSettings.json"), "{\"setting\":1}");
                snapshots.Add((root, record.Id), [first, second]);
                foreach (var path in Directory.EnumerateFiles(Path.GetDirectoryName(localLow)!, "*", SearchOption.AllDirectories))
                {
                    before.Add(path, await File.ReadAllTextAsync(path));
                }

                async Task WriteSaveAsync(string value)
                {
                    await File.WriteAllTextAsync(Path.Combine(localLow, "user1.dat"), record.Id + "-" + value);
                    await File.WriteAllTextAsync(Path.Combine(localLow, "user1.dat.bak1"), record.Id + "-" + value + "-backup");
                }
            }
        }
        var sourceSnapshots = snapshots[(sourceRoot, original.Id)];
        var target = sourceSnapshots[0];
        viewModel.SelectedInstance = null;
        viewModel.SelectedInstance = fixture.Instance;
        await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        viewModel.SelectedSnapshot = Assert.Single(viewModel.Snapshots, snapshot => snapshot.Id == target.Id);
        viewModel.CurrentManageTab = "Snapshots";
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName() == viewModel.Loc[restore ? "Restore" : "DeleteSnapshot"]));
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
            var item = new InstanceItemViewModel(current, current.BuildId, "Vanilla", 0);
            viewModel.Instances.Instances.Add(item);
            viewModel.Instances.VisibleInstances.Add(item);
            viewModel.SelectedInstance = selection == "none" ? null : item;
            await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.SelectedSnapshot = selection == "none" ? null : snapshots[(sourceRoot, current.Id)][1];
        }
        else if (selection == "changed")
        {
            viewModel.SelectedSnapshot = sourceSnapshots[1];
        }
        else if (selection == "version-root")
        {
            viewModel.VersionRoot = alternateRoot;
            viewModel.Snapshots.Clear();
            foreach (var snapshot in snapshots[(alternateRoot, original.Id)])
            {
                viewModel.Snapshots.Add(snapshot);
            }
            viewModel.SelectedSnapshot = snapshots[(alternateRoot, original.Id)][1];
        }
        var selectedInstanceId = viewModel.SelectedInstance?.Id;
        var selectedSnapshotId = viewModel.SelectedSnapshot?.Id;
        var selectionChanges = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.SelectedInstance) or nameof(MainViewModel.SelectedSnapshot))
            {
                selectionChanges.Add($"{args.PropertyName}: instance={viewModel.SelectedInstance?.Id}, snapshot={viewModel.SelectedSnapshot?.Id}");
            }
        };
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
                if (attempt >= 20 && !viewModel.IsBusy)
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
        await DetailsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.ErrorMessage is null, viewModel.ErrorMessage);
        var targetSnapshotRoot = Path.GetDirectoryName(target.SnapshotPath)! + Path.DirectorySeparatorChar;
        foreach (var file in before)
        {
            var expected = file.Value;
            var deleted = action == "confirm" && !restore && file.Key.StartsWith(targetSnapshotRoot, StringComparison.OrdinalIgnoreCase);
            if (action == "confirm" && restore && Path.GetDirectoryName(file.Key) == LocalLow(sourceRoot, original.Id))
            {
                var name = Path.GetFileName(file.Key);
                if (name is "user1.dat" or "user1.dat.bak1")
                {
                    expected = await File.ReadAllTextAsync(Path.Combine(target.SnapshotPath, name));
                }
                deleted = name is "user2.dat" or "user3.dat";
            }
            Assert.Equal(!deleted, File.Exists(file.Key));
            if (!deleted)
            {
                Assert.Equal(expected, await File.ReadAllTextAsync(file.Key));
            }
        }
        Assert.Equal(selectedInstanceId, viewModel.SelectedInstance?.Id);
        var expectedSelection = action == "confirm" && !restore && selection == "same" ? null : selectedSnapshotId;
        Assert.True(expectedSelection == viewModel.SelectedSnapshot?.Id,
            $"Expected snapshot {expectedSelection}; actual {viewModel.SelectedSnapshot?.Id}." + Environment.NewLine
            + string.Join(Environment.NewLine, selectionChanges));
        Assert.False(viewModel.IsBusy);

        NamedSnapshotService Service(string root) => new(Path.Combine(root, ".crystalfly"));
        string LocalLow(string root, string id) => Path.Combine(root, ".crystalfly", "instances", id, "local-low");
        Task DetailsTask() => Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
            .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel));
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
