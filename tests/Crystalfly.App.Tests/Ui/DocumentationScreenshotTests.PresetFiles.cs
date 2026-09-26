using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    public async Task Preset_file_picker_keeps_original_target_when_instance_changes(
        bool export, bool clearSelection, bool cancel, bool closeWindow)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.ModPresets);
        var viewModel = fixture.ViewModel;
        var original = fixture.Instance.Record;
        var source = Assert.IsType<ModPreset>(viewModel.SelectedPreset);
        var presetsRoot = Path.Combine(viewModel.VersionRoot, ".crystalfly", "instances", original.Id, "presets");
        Directory.CreateDirectory(presetsRoot);
        foreach (var preset in viewModel.ModPresets)
        {
            await File.WriteAllTextAsync(PresetPath(presetsRoot, preset.Id), CrystalflyJson.Serialize(preset));
        }
        var originals = Directory.GetFiles(presetsRoot, "*.json").ToDictionary(path => path, File.ReadAllText);
        var currentRoot = Path.Combine(viewModel.VersionRoot, "current");
        Directory.CreateDirectory(currentRoot);
        var current = original with { Id = "current", RootPath = currentRoot };
        var currentPresetsRoot = Path.Combine(viewModel.VersionRoot, ".crystalfly", "instances", current.Id, "presets");
        Directory.CreateDirectory(currentPresetsRoot);
        var currentPreset = source with { Name = "Current preset", Entries = [] };
        var currentDocument = CrystalflyJson.Serialize(currentPreset);
        var currentFile = PresetPath(currentPresetsRoot, currentPreset.Id);
        await File.WriteAllTextAsync(currentFile, currentDocument);
        var path = Path.Combine(viewModel.VersionRoot, export ? "export.json" : "import.json");
        if (!export)
        {
            await File.WriteAllTextAsync(path, CrystalflyJson.Serialize(source));
        }
        var storage = DispatchProxy.Create<IStorageProvider, DeferredPresetPicker>();
        var picker = (DeferredPresetPicker)storage;
        var storageField = Assert.Single(typeof(TopLevel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(IStorageProvider));
        storageField.SetValue(fixture.Window, storage);
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        var button = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), item =>
            item.IsEffectivelyVisible && new ButtonAutomationPeer(item).GetName() == viewModel.Loc[export ? "ExportPreset" : "ImportPreset"]);
        button.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        var center = Assert.IsType<Point>(button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window));
        fixture.Window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        fixture.Window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
        await picker.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(export, picker.Save);
        if (export)
        {
            Assert.Equal(source.Name + ".json", picker.SuggestedFileName);
        }
        var currentItem = new InstanceItemViewModel(current, current.BuildId, "Vanilla", 0);
        viewModel.Instances.Instances.Add(currentItem);
        viewModel.Instances.VisibleInstances.Add(currentItem);
        viewModel.SelectedInstance = clearSelection ? null : currentItem;
        var loadTask = Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
            .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel));
        await loadTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(clearSelection ? null : current.Id, viewModel.SelectedInstance?.Id);
        Assert.Null(viewModel.ErrorMessage);
        if (!clearSelection)
        {
            viewModel.SelectedPreset = Assert.Single(viewModel.ModPresets);
            Assert.Equal(currentPreset.Name, viewModel.SelectedPreset.Name);
        }
        var selectedAfterSwitch = viewModel.SelectedPreset;
        viewModel.PresetName = "Later edit";
        var file = DispatchProxy.Create<IStorageFile, PresetPickerFile>();
        ((PresetPickerFile)file).LocalPath = path;
        if (closeWindow)
        {
            fixture.Window.Close();
            for (var attempt = 0; attempt < 100 && fixture.Window.IsVisible; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }
            Assert.False(fixture.Window.IsVisible);
        }
        Exception? pickerException = null;
        void CapturePickerException(object? sender, DispatcherUnhandledExceptionEventArgs args)
        {
            pickerException = args.Exception;
            args.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += CapturePickerException;
        try
        {
            picker.Response.SetResult(cancel ? null : file);
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
                if (cancel || closeWindow || (export ? File.Exists(path)
                        : Directory.GetFiles(presetsRoot, "*.json").Length == originals.Count + 1)
                    && !viewModel.IsBusy)
                {
                    break;
                }
            }
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= CapturePickerException;
        }
        Assert.Null(pickerException);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(currentDocument, await File.ReadAllTextAsync(currentFile));
        Assert.Single(Directory.GetFiles(currentPresetsRoot, "*.json"));
        Assert.Same(selectedAfterSwitch, viewModel.SelectedPreset);
        Assert.Equal("Later edit", viewModel.PresetName);
        foreach (var originalFile in originals)
        {
            Assert.Equal(originalFile.Value, await File.ReadAllTextAsync(originalFile.Key));
        }
        if (export)
        {
            Assert.Equal(!cancel && !closeWindow, File.Exists(path));
            if (!cancel && !closeWindow)
            {
                Assert.Equal(CrystalflyJson.Serialize(source), await File.ReadAllTextAsync(path));
            }
            Assert.Equal(originals.Count, Directory.GetFiles(presetsRoot, "*.json").Length);
        }
        else
        {
            var added = Directory.GetFiles(presetsRoot, "*.json").Except(originals.Keys).ToArray();
            if (cancel || closeWindow)
            {
                Assert.Empty(added);
            }
            else
            {
                var imported = await AtomicJsonStore.ReadAsync<ModPreset>(Assert.Single(added));
                Assert.Equal(source.Name, imported.Name);
                Assert.Equal(source.Entries.Select(entry => entry.Id), imported.Entries.Select(entry => entry.Id));
            }
        }

        static string PresetPath(string root, string id) => Path.Combine(root,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))) + ".json");
    }

    public class DeferredPresetPicker : DispatchProxy
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IStorageFile?> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Save { get; private set; }
        public string? SuggestedFileName { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_CanOpen":
                case "get_CanSave":
                    return true;
                case nameof(IStorageProvider.OpenFilePickerAsync):
                    Started.TrySetResult();
                    return OpenAsync();
                case nameof(IStorageProvider.SaveFilePickerAsync):
                    Save = true;
                    SuggestedFileName = ((FilePickerSaveOptions)args![0]!).SuggestedFileName;
                    Started.TrySetResult();
                    return Response.Task;
                case nameof(IDisposable.Dispose):
                    return null;
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }

        private async Task<IReadOnlyList<IStorageFile>> OpenAsync() =>
            await Response.Task is { } file ? [file] : [];
    }

    public class PresetPickerFile : DispatchProxy
    {
        public string LocalPath { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_Path" => new Uri(LocalPath),
            "get_Name" => Path.GetFileName(LocalPath),
            nameof(IDisposable.Dispose) => null,
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
    }
}
