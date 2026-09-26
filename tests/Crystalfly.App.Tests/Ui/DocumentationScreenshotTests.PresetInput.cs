using System.Net;
using System.Reflection;
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
using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese, false, "other", "confirm")]
    [InlineData(UiLanguage.SimplifiedChinese, false, "none", "confirm")]
    [InlineData(UiLanguage.SimplifiedChinese, true, "other", "confirm")]
    [InlineData(UiLanguage.SimplifiedChinese, true, "none", "confirm")]
    [InlineData(UiLanguage.English, false, "other", "confirm")]
    [InlineData(UiLanguage.English, false, "none", "confirm")]
    [InlineData(UiLanguage.English, true, "other", "confirm")]
    [InlineData(UiLanguage.English, true, "none", "confirm")]
    [InlineData(UiLanguage.SimplifiedChinese, false, "same", "confirm")]
    [InlineData(UiLanguage.SimplifiedChinese, true, "same", "confirm")]
    [InlineData(UiLanguage.SimplifiedChinese, false, "other", "cancel")]
    [InlineData(UiLanguage.SimplifiedChinese, true, "other", "cancel")]
    [InlineData(UiLanguage.SimplifiedChinese, false, "other", "close")]
    [InlineData(UiLanguage.SimplifiedChinese, true, "other", "close")]
    [InlineData(UiLanguage.SimplifiedChinese, true, "same", "pending-close")]
    public async Task Preset_input_dialog_keeps_opening_instance_and_later_edits(
        UiLanguage language, bool shared, string selection, string action)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.ModPresets);
        var viewModel = fixture.ViewModel;
        viewModel.Loc.Apply(language);
        var original = fixture.Instance.Record;
        var source = Assert.IsType<ModPreset>(viewModel.SelectedPreset);
        var presetsRoot = Path.Combine(viewModel.VersionRoot, ".crystalfly", "instances", original.Id, "presets");
        Directory.CreateDirectory(presetsRoot);
        foreach (var preset in viewModel.ModPresets)
        {
            await File.WriteAllTextAsync(PresetPath(presetsRoot, preset.Id), CrystalflyJson.Serialize(preset));
        }
        var originals = Directory.GetFiles(presetsRoot, "*.json").ToDictionary(path => path, File.ReadAllText);
        await WriteLoaderAsync(original);
        var currentRoot = Path.Combine(viewModel.VersionRoot, "current");
        Directory.CreateDirectory(currentRoot);
        var current = original with { Id = "current", RootPath = currentRoot };
        await WriteLoaderAsync(current);
        var currentPresetsRoot = Path.Combine(viewModel.VersionRoot, ".crystalfly", "instances", current.Id, "presets");
        Directory.CreateDirectory(currentPresetsRoot);
        var currentPreset = source with { Name = "Current preset", ApplyMode = ModPresetApplyMode.Append, Entries = [] };
        var currentFile = PresetPath(currentPresetsRoot, currentPreset.Id);
        var currentDocument = CrystalflyJson.Serialize(currentPreset);
        await File.WriteAllTextAsync(currentFile, currentDocument);
        using var handler = new InputDialogShareHandler(source, action == "pending-close");
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy();
        SetPrivateField(viewModel, "presetShareClient", new PresetShareClient(
            client, policy, new Uri("https://share.example.test/")));
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        fixture.Window.Show();
        fixture.Window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        Click(Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
            button.IsEffectivelyVisible && new ButtonAutomationPeer(button).GetName()
                == viewModel.Loc[shared ? "ImportSharedPreset" : "CreateModPack"]));
        UserControl? dialog = null;
        for (var attempt = 0; attempt < 100 && dialog is null; attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            dialog = fixture.Window.GetVisualDescendants().OfType<UserControl>().SingleOrDefault(control =>
                shared ? control is TextInputDialogView : control is ModPackEditorDialogView);
        }
        Assert.NotNull(dialog);
        Assert.Single(dialog.GetVisualDescendants().OfType<TextBox>(), input =>
            AutomationProperties.GetName(input) == viewModel.Loc[shared ? "PresetShareCode" : "PresetName"]).Text = shared
            ? "  A1B2C3D4E5F6  " : "  Submitted preset  ";
        if (!shared)
        {
            var editor = Assert.IsType<ModPackEditorDialogViewModel>(dialog.DataContext);
            Assert.Single(dialog.GetVisualDescendants().OfType<ComboBox>()).SelectedItem =
                editor.ModeOptions.Single(option => option.Value == ModPresetApplyMode.Exact);
        }
        if (selection != "same")
        {
            var item = new InstanceItemViewModel(current, current.BuildId, "Vanilla", 0);
            viewModel.Instances.Instances.Add(item);
            viewModel.Instances.VisibleInstances.Add(item);
            viewModel.SelectedInstance = selection == "none" ? null : item;
            await Assert.IsAssignableFrom<Task>(typeof(MainViewModel)
                .GetField("detailsLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel))
                .WaitAsync(TimeSpan.FromSeconds(5));
            if (selection == "other")
            {
                viewModel.SelectedPreset = Assert.Single(viewModel.ModPresets);
                Assert.Equal(currentPreset.Name, viewModel.SelectedPreset.Name);
            }
        }
        Assert.Equal(selection == "none" ? null : selection == "same" ? original.Id : current.Id,
            viewModel.SelectedInstance?.Id);
        Assert.Null(viewModel.ErrorMessage);
        var selectedAfterSwitch = viewModel.SelectedPreset;
        viewModel.PresetName = "Later edit";
        viewModel.SelectedPresetModeOption = viewModel.PresetModeOptions.Single(option => option.Value == ModPresetApplyMode.Append);
        viewModel.PresetShareCode = "Z9Y8X7W6V5U4";
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
                Click(Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button =>
                    AutomationProperties.GetName(button) == viewModel.Loc[action == "cancel"
                        ? "Cancel" : shared ? "ImportSharedPreset" : "Confirm"]));
                if (action == "pending-close")
                {
                    await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    fixture.Window.Close();
                }
            }
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
                if (action is "close" or "pending-close" ? !fixture.Window.IsVisible
                    : !fixture.Window.GetVisualDescendants().Contains(dialog) && !viewModel.IsBusy
                        && (action == "cancel" || callbackException is not null || viewModel.ErrorMessage is not null
                            || Directory.GetFiles(presetsRoot, "*.json").Length == originals.Count + 1))
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
        if (action is "close" or "pending-close")
        {
            Assert.False(fixture.Window.IsVisible);
        }
        else
        {
            Assert.DoesNotContain(dialog, fixture.Window.GetVisualDescendants());
        }
        Assert.Equal(currentDocument, await File.ReadAllTextAsync(currentFile));
        Assert.Single(Directory.GetFiles(currentPresetsRoot, "*.json"));
        foreach (var file in originals)
        {
            Assert.Equal(file.Value, await File.ReadAllTextAsync(file.Key));
        }
        var added = Directory.GetFiles(presetsRoot, "*.json").Except(originals.Keys).ToArray();
        if (action == "confirm")
        {
            var saved = await AtomicJsonStore.ReadAsync<ModPreset>(Assert.Single(added));
            Assert.Equal(shared ? source.Name : "Submitted preset", saved.Name);
            Assert.Equal(ModPresetApplyMode.Exact, saved.ApplyMode);
            if (shared)
            {
                Assert.Equal(source.Entries.Select(entry => entry.Id), saved.Entries.Select(entry => entry.Id));
                Assert.EndsWith("/A1B2C3D4E5F6", Assert.Single(handler.Requests));
            }
            if (selection == "same")
            {
                Assert.Equal(saved.Id, viewModel.SelectedPreset?.Id);
            }
        }
        else
        {
            Assert.Empty(added);
            if (action == "pending-close")
            {
                Assert.EndsWith("/A1B2C3D4E5F6", Assert.Single(handler.Requests));
            }
            else
            {
                Assert.Empty(handler.Requests);
            }
        }
        if (selection != "same")
        {
            Assert.Same(selectedAfterSwitch, viewModel.SelectedPreset);
            Assert.Equal("Later edit", viewModel.PresetName);
            Assert.Equal(ModPresetApplyMode.Append, viewModel.SelectedPresetModeOption?.Value);
        }
        Assert.Equal("Z9Y8X7W6V5U4", viewModel.PresetShareCode);

        static string PresetPath(string root, string id) => Path.Combine(root,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))) + ".json");

        static async Task WriteLoaderAsync(InstanceRecord record)
        {
            const string relative = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll";
            var path = Path.Combine(record.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "loader");
            await AtomicJsonStore.WriteAsync(Path.Combine(
                Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(record.RootPath, record.Id))!, "loader.json"),
                new InstalledPackageReceipt
                {
                    PackageId = "modding-api-77", LoaderState = LoaderState.ModdingApi,
                    Files = [new InstalledFileReceipt
                    {
                        RelativePath = relative, Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)))
                    }]
                });
        }

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

    private sealed class InputDialogShareHandler(ModPreset preset, bool waitForClose) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.RequestUri!.AbsolutePath);
            Started.TrySetResult();
            if (waitForClose)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CrystalflyJson.Serialize(new { Code = "A1B2C3D4E5F6", Preset = preset }),
                    Encoding.UTF8, "application/json")
            };
        }
    }
}
