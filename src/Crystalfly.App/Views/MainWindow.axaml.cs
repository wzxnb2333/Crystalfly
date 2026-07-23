using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.App.Runtime;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace Crystalfly.App.Views;

public partial class MainWindow : Window
{
    internal const string OverlayHostId = "Crystalfly.Main";

    private bool closeAfterDispose;
    private bool closeForApplicationUpdate;
    private bool closeRequested;
    private bool toastManagerClosing;
    private bool toastManagerUninstalled;
    private Task? disposeBeforeCloseTask;
    private Task? closeConfirmationTask;
    private Task<bool>? marketInstallDialogTask;
    private Task<LaunchIssuesDialogResult?>? launchIssuesDialogTask;
    private int marketInstallDialogOpening;
    private readonly WindowToastManager toastManager;
    private readonly SemaphoreSlim externalCommandGate = new(1, 1);
    private Action<string>? toastRequestedHandler;
    private MainViewModel? toastViewModel;
    private bool externalCommandReady;

    internal bool IsExternalCommandReady => externalCommandReady;

    public MainWindow()
    {
        InitializeComponent();
        toastManager = new WindowToastManager(this) { MaxItems = 3 };
        DataContextChanged += OnDataContextChanged;
        OnDataContextChanged(this, EventArgs.Empty);
        Opened += OnOpened;
    }

    private void OnWindowChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled
            || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed
            || e.Source is not Avalonia.Visual visual
            || visual.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private void OnWindowMinimizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnWindowMaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnWindowCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        Opened -= OnOpened;
        if (DataContext is MainViewModel viewModel)
        {
            var initialized = false;
            try
            {
                await viewModel.InitializeAsync();
                initialized = true;
            }
            catch (Exception exception)
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
            }
            finally
            {
                ResumeExternalCommands();
            }
            if (initialized)
            {
                ApplicationUpdateHealthHandshake.SignalFromEnvironment();
                await CheckForApplicationUpdateAsync(viewModel, force: false);
            }
        }
    }

    private async void OnCheckForApplicationUpdatesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await CheckForApplicationUpdateAsync(viewModel, force: true);
        }
    }

    private async Task CheckForApplicationUpdateAsync(MainViewModel viewModel, bool force)
    {
        try
        {
            var result = await viewModel.CheckApplicationUpdateAsync(force);
            if (result.Manifest is not { } manifest || !IsVisible)
            {
                return;
            }

            var dialogViewModel = new ApplicationUpdateDialogViewModel(
                viewModel.Loc["ApplicationUpdateTitle"],
                string.Format(
                    System.Globalization.CultureInfo.CurrentUICulture,
                    viewModel.Loc["ApplicationUpdateVersionFormat"],
                    manifest.Version),
                manifest.NotesMarkdown,
                viewModel.Loc["UpdateNow"],
                viewModel.Loc["Later"],
                viewModel.Loc["SkipThisVersion"]);
            var choice = await OverlayDialog.ShowCustomAsync<
                ApplicationUpdateDialogView,
                ApplicationUpdateDialogViewModel,
                ApplicationUpdateDialogResult>(
                dialogViewModel,
                OverlayHostId,
                CreateOverlayOptions());
            switch (choice)
            {
                case ApplicationUpdateDialogResult.Update:
                    await StartApplicationUpdateAsync(
                        viewModel,
                        viewModel.StartAvailableApplicationUpdateAsync);
                    break;
                case ApplicationUpdateDialogResult.SkipVersion:
                    await viewModel.SkipApplicationUpdateAsync();
                    break;
                case ApplicationUpdateDialogResult.Later:
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            viewModel.ReportApplicationUpdateFailure(exception);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (closeForApplicationUpdate)
        {
            closeForApplicationUpdate = false;
            closeRequested = true;
            SuspendExternalCommands();
            e.Cancel = true;
            base.OnClosing(e);
            CloseOpenDialogs();
            disposeBeforeCloseTask ??= DisposeBeforeCloseAsync();
            return;
        }

        if (!closeAfterDispose && DataContext is MainViewModel { IsBusy: true })
        {
            e.Cancel = true;
            base.OnClosing(e);
            return;
        }

        if (closeAfterDispose)
        {
            closeRequested = true;
            SuspendExternalCommands();
            base.OnClosing(e);
            return;
        }

        closeRequested = true;
        SuspendExternalCommands();
        e.Cancel = true;
        base.OnClosing(e);
        if (DataContext is MainViewModel { HasUnfinishedDownloads: true } viewModel)
        {
            closeConfirmationTask ??= ConfirmCloseWithDownloadsAsync(viewModel);
            return;
        }
        CloseOpenDialogs();
        disposeBeforeCloseTask ??= DisposeBeforeCloseAsync();
    }

    internal async Task<bool> StartApplicationUpdateAsync(
        MainViewModel viewModel,
        Func<CancellationToken, Task<bool>> startUpdate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(startUpdate);

        if (viewModel.HasUnfinishedDownloads)
        {
            bool confirmed = await ShowConfirmationAsync(
                viewModel.Loc["ConfirmCloseDownloadsTitle"],
                viewModel.Loc["ConfirmCloseDownloadsMessage"],
                string.IsNullOrWhiteSpace(viewModel.ActiveDownloadSummary)
                    ? viewModel.Loc["DownloadQueue"]
                    : viewModel.ActiveDownloadSummary,
                viewModel,
                isDangerous: true);
            if (!confirmed)
            {
                return false;
            }
        }

        if (!await startUpdate(cancellationToken))
        {
            return false;
        }

        closeForApplicationUpdate = true;
        Close();
        return true;
    }

    private async Task ConfirmCloseWithDownloadsAsync(MainViewModel viewModel)
    {
        var confirmed = false;
        try
        {
            confirmed = await ShowConfirmationAsync(
                viewModel.Loc["ConfirmCloseDownloadsTitle"],
                viewModel.Loc["ConfirmCloseDownloadsMessage"],
                string.IsNullOrWhiteSpace(viewModel.ActiveDownloadSummary)
                    ? viewModel.Loc["DownloadQueue"]
                    : viewModel.ActiveDownloadSummary,
                viewModel,
                isDangerous: true);
            if (confirmed)
            {
                CloseOpenDialogs();
                disposeBeforeCloseTask ??= DisposeBeforeCloseAsync();
            }
        }
        finally
        {
            closeConfirmationTask = null;
            if (!confirmed && !closeAfterDispose)
            {
                closeRequested = false;
                ResumeExternalCommands();
            }
        }
    }

    private void CloseOpenDialogs()
    {
        foreach (var dialog in MainOverlayDialogHost.Children.OfType<CustomDialogControl>().ToArray())
        {
            dialog.Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape
            && MainOverlayDialogHost.Children.OfType<CustomDialogControl>().LastOrDefault()?.DataContext
                is IDialogContext context)
        {
            e.Handled = true;
            context.Close();
            return;
        }

        base.OnKeyDown(e);
    }

    private async Task DisposeBeforeCloseAsync()
    {
        try
        {
            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                await UninstallToastManagerAsync();
            }
            finally
            {
                closeAfterDispose = true;
                Close();
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (toastViewModel is not null)
        {
            if (toastRequestedHandler is not null)
            {
                toastViewModel.ToastRequested -= toastRequestedHandler;
            }
            toastViewModel.PropertyChanged -= OnToastViewModelPropertyChanged;
        }

        toastRequestedHandler = null;
        toastViewModel = DataContext as MainViewModel;
        if (toastViewModel is not null)
        {
            var owner = toastViewModel;
            toastRequestedHandler = message => ShowToast(owner, message, NotificationType.Success);
            toastViewModel.ToastRequested += toastRequestedHandler;
            toastViewModel.PropertyChanged += OnToastViewModelPropertyChanged;
        }
    }

    private void OnToastViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainViewModel.ErrorMessage)
            && sender is MainViewModel viewModel
            && !string.IsNullOrWhiteSpace(viewModel.ErrorMessage))
        {
            ShowToast(viewModel, viewModel.ErrorMessage, NotificationType.Error);
        }
    }

    private void ShowToast(MainViewModel owner, string message, NotificationType type) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!toastManagerClosing && ReferenceEquals(owner, toastViewModel))
            {
                toastManager.Show(message, type);
            }
        });

    private async Task UninstallToastManagerAsync()
    {
        if (toastManagerUninstalled)
        {
            return;
        }

        DetachToastSubscriptions();
        toastManagerClosing = true;
        var cards = toastManager.GetVisualDescendants().OfType<ToastCard>()
            .Where(card => !card.IsClosed)
            .ToArray();
        var completions = new List<Task>(cards.Length);
        foreach (var card in cards)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<MessageClosedEventArgs>? handler = null;
            handler = (_, _) =>
            {
                card.MessageClosed -= handler;
                completion.TrySetResult();
            };
            card.MessageClosed += handler;
            completions.Add(completion.Task);
        }

        toastManager.CloseAll();
        var allClosed = Task.WhenAll(completions);
        if (await Task.WhenAny(allClosed, Task.Delay(TimeSpan.FromSeconds(2))) != allClosed)
        {
            foreach (var card in cards.Where(card => !card.IsClosed))
            {
                card.IsClosed = true;
            }
            await allClosed;
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        }
        toastManager.Uninstall();
        toastManagerUninstalled = true;
    }

    private void DetachToastSubscriptions()
    {
        DataContextChanged -= OnDataContextChanged;
        if (toastViewModel is not null)
        {
            if (toastRequestedHandler is not null)
            {
                toastViewModel.ToastRequested -= toastRequestedHandler;
            }
            toastViewModel.PropertyChanged -= OnToastViewModelPropertyChanged;
            toastRequestedHandler = null;
            toastViewModel = null;
        }
    }

    private async void CloneInstanceWithName(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstanceItemViewModel instance })
        {
            return;
        }
        viewModel.SelectedInstance = instance;
        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["CloneInstance"],
            viewModel.Loc["CloneInstanceName"],
            $"{instance.Name} {viewModel.Loc["CopySuffix"]}",
            viewModel.Loc["CloneInstanceName"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var name = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        viewModel.CloneInstanceName = name;
        await viewModel.CloneSelectedInstanceCommand.ExecuteAsync(null);
        if (viewModel.SelectedInstance is { } selected
            && !string.Equals(selected.Id, instance.Id, StringComparison.Ordinal))
        {
            viewModel.CurrentPage = "Launch";
        }
    }

    private async void ConfirmDeleteInstance(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstanceItemViewModel instance })
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["DeleteInstance"],
            viewModel.Loc["PermanentDeleteWarning"],
            instance.Name,
            viewModel,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.DeleteInstanceCommand.ExecuteAsync(instance);
        }
    }

    private void OnInstalledModPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed
            || eventArgs.Source is Avalonia.Visual visual && visual.FindAncestorOfType<Button>() is not null
            || DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        viewModel.SelectInstalledMod(
            item,
            eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control),
            eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift));
        InstalledModsList.Focus(NavigationMethod.Pointer, eventArgs.KeyModifiers);
        eventArgs.Handled = true;
    }

    private void OnInstalledModsKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.A
            && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            && DataContext is MainViewModel viewModel)
        {
            viewModel.SelectAllInstalledModsCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    private void OpenInstalledModInfo(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.OpenInstalledModInfoCommand.Execute(item);
        }
    }

    private void OpenInstalledModFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        viewModel.SelectedInstalledMod = item;
        OpenSafeInstanceFolder(instance.RootPath, item.InstallRoot, viewModel);
    }

    private void OpenSelectedMarketModFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel
            {
                SelectedInstance: { } instance,
                SelectedMarketInstalledMod: { } item
            } viewModel)
        {
            return;
        }
        OpenSafeInstanceFolder(instance.RootPath, item.InstallRoot, viewModel);
    }

    private void OpenSelectedModGlobalSettings(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            var filePath = viewModel.ResolveSelectedModGlobalSettingsPath();
            var directory = filePath is null ? null : Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or Win32Exception)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private async void ConfirmDeleteSelectedModGlobalSettings(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel
            {
                SelectedMarketModDisplay: { } mod,
                HasSelectedModGlobalSettings: true
            } viewModel)
        {
            return;
        }
        if (await ShowConfirmationAsync(
                viewModel.Loc["DeleteGlobalSettings"],
                viewModel.Loc["DeleteGlobalSettings"],
                mod.PrimaryName,
                viewModel,
                isDangerous: true))
        {
            await viewModel.DeleteSelectedModGlobalSettingsCommand.ExecuteAsync(null);
        }
    }

    private void OpenInstalledModRoot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            return;
        }
        var relative = viewModel.CurrentLoaderState == Crystalfly.Core.Models.LoaderState.BepInEx
            ? Path.Combine("BepInEx", "plugins")
            : Path.Combine("hollow_knight_Data", "Managed", "Mods");
        OpenSafeInstanceFolder(instance.RootPath, relative, viewModel);
    }

    private async void ImportLocalModPackage(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["SelectModPackageTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["Mods"])
                {
                    Patterns = ["*.zip", "*.dll"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        viewModel.LocalModPath = path;
        await viewModel.ImportLocalModCommand.ExecuteAsync(null);
    }

    private async void ConfirmApplyPreset(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel
            {
                SelectedInstance: { } instance,
                SelectedPreset: { } preset
            } viewModel)
        {
            return;
        }

        try
        {
            var plan = await viewModel.CreateSelectedPresetPlanAsync();
            if (plan is null)
            {
                return;
            }

            var dialog = new DependencyPlanDialogViewModel(
                viewModel.Loc["ConfirmApplyPresetTitle"],
                $"{viewModel.Loc["ConfirmApplyPresetMessage"]}{Environment.NewLine}"
                + $"{viewModel.Loc["PresetMode"]}: {GetPresetModeName(viewModel, preset.ApplyMode)}{Environment.NewLine}"
                + $"{viewModel.Loc["PresetTargetInstance"]}: {instance.Name}",
                BuildPresetApplyNodes(viewModel),
                viewModel.Loc["ApplyPreset"],
                viewModel.Loc["Cancel"],
                canConfirm: !plan.IsBlocked,
                isDangerous: false);
            var confirmed = await OverlayDialog.ShowCustomAsync<
                DependencyPlanDialogView,
                DependencyPlanDialogViewModel,
                bool>(dialog, OverlayHostId, CreateOverlayOptions());
            if (confirmed)
            {
                await viewModel.EnqueueSelectedPresetAsync();
            }
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private async void ConfirmDeletePreset(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedPreset: { } preset } viewModel)
        {
            return;
        }

        if (await ShowConfirmationAsync(
                viewModel.Loc["ConfirmDeletePresetTitle"],
                viewModel.Loc["ConfirmDeletePresetMessage"],
                preset.Name,
                viewModel,
                isDangerous: true))
        {
            await viewModel.DeleteSelectedPresetAsync();
        }
    }

    private async void ImportPresetFile(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["ImportPreset"],
            AllowMultiple = false,
            FileTypeFilter = [CreatePresetJsonFileType(viewModel)]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        if (!IsPresetJsonPath(path))
        {
            viewModel.ErrorMessage = viewModel.Loc["PresetJsonFileRequired"];
            return;
        }

        await viewModel.ImportPresetFromFileAsync(path);
    }

    private async void ExportSelectedPreset(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedPreset: { } preset } viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = viewModel.Loc["ExportPreset"],
            SuggestedFileName = $"{preset.Name}.json",
            DefaultExtension = "json",
            FileTypeChoices = [CreatePresetJsonFileType(viewModel)],
            ShowOverwritePrompt = true
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.ExportSelectedPresetToFileAsync(path);
    }

    private async void CopyPresetShareLink(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || Clipboard is null
            || !Uri.TryCreate(viewModel.LastPresetShareUrl, UriKind.Absolute, out var link)
            || link.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        try
        {
            await Clipboard.SetTextAsync(link.AbsoluteUri);
            ShowToast(viewModel, viewModel.Loc["PresetShareLinkCopied"], NotificationType.Success);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private static FilePickerFileType CreatePresetJsonFileType(MainViewModel viewModel) => new(
        viewModel.Loc["PresetJsonFiles"])
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    private static bool IsPresetJsonPath(string path) =>
        string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);

    private static string GetPresetModeName(MainViewModel viewModel, Crystalfly.Core.Models.ModPresetApplyMode mode) =>
        viewModel.Loc[mode == Crystalfly.Core.Models.ModPresetApplyMode.Exact
            ? "PresetModeExact"
            : "PresetModeAppend"];

    private static IReadOnlyList<DependencyPlanNodeViewModel> BuildPresetApplyNodes(MainViewModel viewModel) =>
        viewModel.PresetApplySteps.Select(step => new DependencyPlanNodeViewModel(
            step.Action,
            string.Join(" · ", new[] { step.State, step.Version, step.LoaderId }
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            step.ModId,
            step.Step.Reason,
            step.State,
            depth: 0,
            isTarget: false,
            isUnresolved: step.IsUnresolved || step.IsBlocked,
            unresolvedLabel: step.IsBlocked
                ? viewModel.Loc["PresetStateBlocked"]
                : viewModel.Loc["PresetStateUnresolved"])).ToArray();

    private async void ToggleHoveredInstalledMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.SelectedInstalledMod = item;
            await viewModel.ToggleSelectedModCommand.ExecuteAsync(null);
        }
    }

    private async void ToggleHoveredModPinned(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.SelectedInstalledMod = item;
            await viewModel.ToggleSelectedModPinnedCommand.ExecuteAsync(null);
        }
    }

    private async void TakeOverHoveredInstalledMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        viewModel.SelectedInstalledMod = item;
        if (await ShowConfirmationAsync(
                viewModel.Loc["TakeOverMod"],
                viewModel.Loc["ExternalModReadOnly"],
                item.Name,
                viewModel))
        {
            await viewModel.TakeOverSelectedModCommand.ExecuteAsync(null);
        }
    }

    private async void RepairHoveredInstalledMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.SelectedInstalledMod = item;
            await viewModel.RepairSelectedModCommand.ExecuteAsync(null);
        }
    }

    private async void AcceptHoveredLocalModFiles(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        viewModel.SelectedInstalledMod = item;
        if (await ShowConfirmationAsync(
                viewModel.Loc["AcceptCurrentFiles"],
                item.HealthDisplayName,
                item.Name,
                viewModel))
        {
            await viewModel.AcceptSelectedLocalModFilesCommand.ExecuteAsync(null);
        }
    }

    private async void ReimportHoveredLocalMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["ReimportLocalMod"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["Mods"])
                {
                    Patterns = ["*.zip", "*.dll"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        viewModel.SelectedInstalledMod = item;
        viewModel.LocalModPath = path;
        await viewModel.ReimportSelectedLocalModCommand.ExecuteAsync(null);
    }

    private async void ShowInstalledModHealth(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        var report = item.HealthReport;
        var paths = report.MissingFiles
            .Concat(report.ModifiedFiles)
            .Concat(report.ExtraFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await ShowConfirmationAsync(
            item.HealthDisplayName,
            string.IsNullOrWhiteSpace(report.Detail) ? item.HealthDisplayName : report.Detail,
            paths.Length == 0 ? item.Name : string.Join(Environment.NewLine, paths),
            viewModel,
            canConfirm: false);
    }

    private async void ConfirmHoveredModUninstall(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.SelectedInstalledMod = item;
            await ConfirmModRemovalAsync(viewModel, bulk: false);
        }
    }

    private async void ConfirmRepairDependencies(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        try
        {
            var plan = viewModel.CreateModDependencyRepairPlan();
            var nodes = BuildDependencyRepairNodes(viewModel, plan);
            var dialog = new DependencyPlanDialogViewModel(
                viewModel.Loc["RepairDependencies"],
                viewModel.Loc["DependencyImpact"],
                nodes,
                viewModel.Loc["Confirm"],
                viewModel.Loc["Cancel"],
                nodes.Any(node => !node.IsUnresolved),
                isDangerous: false);
            var confirmed = await OverlayDialog.ShowCustomAsync<
                DependencyPlanDialogView,
                DependencyPlanDialogViewModel,
                bool>(dialog, OverlayHostId, CreateOverlayOptions());
            if (confirmed)
            {
                await viewModel.RepairModDependenciesCommand.ExecuteAsync(null);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private async Task ConfirmModRemovalAsync(MainViewModel viewModel, bool bulk)
    {
        var plan = viewModel.CreateModRemovalPlan(bulk);
        var installed = viewModel.InstalledMods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var nodes = plan.Nodes.Select(node =>
        {
            installed.TryGetValue(node.ModId, out var item);
            return new DependencyPlanNodeViewModel(
                item?.PrimaryName ?? node.ReceiptName,
                item?.SecondaryName ?? string.Empty,
                node.ModId,
                node.InstallRoot,
                node.Kind == Crystalfly.Core.Mods.ModRemovalImpactKind.WillRemove
                    ? viewModel.Loc["WillDelete"]
                    : viewModel.Loc["DependenciesWillBeMissing"],
                node.Depth,
                node.Kind == Crystalfly.Core.Mods.ModRemovalImpactKind.WillRemove,
                isUnresolved: false);
        }).ToArray();
        var dialog = new DependencyPlanDialogViewModel(
            bulk ? viewModel.Loc["ConfirmBulkUninstallTitle"] : viewModel.Loc["ConfirmModUninstallTitle"],
            viewModel.Loc["DependencyImpact"],
            nodes,
            viewModel.Loc["Uninstall"],
            viewModel.Loc["Cancel"],
            canConfirm: true,
            isDangerous: true);
        var confirmed = await OverlayDialog.ShowCustomAsync<
            DependencyPlanDialogView,
            DependencyPlanDialogViewModel,
            bool>(dialog, OverlayHostId, CreateOverlayOptions());
        if (confirmed)
        {
            if (bulk)
            {
                await viewModel.UninstallSelectedModsCommand.ExecuteAsync(null);
            }
            else
            {
                await viewModel.UninstallSelectedModCommand.ExecuteAsync(null);
            }
        }
    }

    private static void OpenSafeInstanceFolder(
        string instanceRoot,
        string relativePath,
        MainViewModel viewModel)
    {
        try
        {
            var target = ResolveSafeInstanceFolder(instanceRoot, relativePath);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or Win32Exception)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private static string ResolveSafeInstanceFolder(string instanceRoot, string relativePath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(instanceRoot));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Instance root '{root}' was not found.");
        }
        RejectReparsePoint(root);

        var target = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested Mod path is outside the selected instance.");
        }

        var current = root;
        foreach (var segment in Path.GetRelativePath(root, target).Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                throw new DirectoryNotFoundException($"Mod directory '{current}' was not found.");
            }
            RejectReparsePoint(current);
        }
        return target;
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Cannot open reparse point '{path}'.");
        }
    }

    private static IReadOnlyList<DependencyPlanNodeViewModel> BuildDependencyRepairNodes(
        MainViewModel viewModel,
        ModDependencyRepairPlan plan)
    {
        var installed = viewModel.InstalledMods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var items = plan.Items.ToDictionary(item => item.ModId, StringComparer.OrdinalIgnoreCase);
        var children = plan.Items
            .SelectMany(item => item.RequiredByModIds.Select(parent => (Parent: parent, Item: item)))
            .GroupBy(link => link.Parent, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.Item).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var nodes = new List<DependencyPlanNodeViewModel>();
        var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = plan.Items.SelectMany(item => item.RequiredByModIds)
            .Where(id => !items.ContainsKey(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var rootId in roots)
        {
            installed.TryGetValue(rootId, out var root);
            nodes.Add(NewNode(
                root?.PrimaryName ?? rootId,
                root?.SecondaryName ?? string.Empty,
                rootId,
                root?.InstallRoot ?? string.Empty,
                root is null ? viewModel.Loc["Missing"] : root.IsEnabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"],
                0,
                isTarget: true,
                isUnresolved: false));
            AppendChildren(rootId, 1, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootId });
        }
        foreach (var item in plan.Items.Where(item => !rendered.Contains(item.ModId)))
        {
            AppendItem(item, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
        return nodes;

        void AppendChildren(string parentId, int depth, HashSet<string> path)
        {
            if (!children.TryGetValue(parentId, out var childItems))
            {
                return;
            }
            foreach (var child in childItems)
            {
                AppendItem(child, depth, path);
            }
        }

        void AppendItem(
            ModDependencyRepairPlanItem item,
            int depth,
            IReadOnlySet<string> parentPath)
        {
            if (parentPath.Contains(item.ModId))
            {
                return;
            }
            rendered.Add(item.ModId);
            installed.TryGetValue(item.ModId, out var current);
            var currentStatus = current is null
                ? viewModel.Loc["Missing"]
                : current.IsEnabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"];
            var actionStatus = item.Action switch
            {
                ModDependencyRepairAction.ReEnable => viewModel.Loc["WillReEnable"],
                ModDependencyRepairAction.DownloadAndInstall => viewModel.Loc["WillDownloadAndInstall"],
                _ => viewModel.Loc["CannotRepair"]
            };
            nodes.Add(NewNode(
                current?.PrimaryName ?? item.Name,
                current?.SecondaryName ?? string.Empty,
                item.ModId,
                current?.InstallRoot ?? string.Empty,
                $"{currentStatus} · {actionStatus}",
                depth,
                isTarget: false,
                isUnresolved: item.Action == ModDependencyRepairAction.Unresolved));
            var path = new HashSet<string>(parentPath, StringComparer.OrdinalIgnoreCase) { item.ModId };
            AppendChildren(item.ModId, depth + 1, path);
        }

        DependencyPlanNodeViewModel NewNode(
            string primaryName,
            string secondaryName,
            string modId,
            string installRoot,
            string status,
            int depth,
            bool isTarget,
            bool isUnresolved) => new(
                primaryName,
                secondaryName,
                modId,
                installRoot,
                status,
                depth,
                isTarget,
                isUnresolved,
                viewModel.Loc["Target"],
                viewModel.Loc["Unresolved"]);
    }
    private async void ConfirmUninstallLoader(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedInstance is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmLoaderUninstallTitle"],
            viewModel.Loc["ConfirmLoaderUninstallMessage"],
            viewModel.SelectedInstance.Name,
            viewModel,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.UninstallLoaderCommand.ExecuteAsync(null);
        }
    }

    private async void ConfirmUninstallMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await ConfirmModRemovalAsync(viewModel, bulk: false);
        }
    }

    private async void ConfirmBulkUninstallMods(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel && viewModel.HasSelectedMods)
        {
            await ConfirmModRemovalAsync(viewModel, bulk: true);
        }
    }

    private async void ConfirmRestoreSnapshot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedSnapshot is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmSnapshotRestoreTitle"],
            viewModel.Loc["ConfirmSnapshotRestoreMessage"],
            viewModel.SelectedSnapshot.Name,
            viewModel);
        if (confirmed)
        {
            await viewModel.RestoreSnapshotCommand.ExecuteAsync(null);
        }
    }

    private async void OpenMarketInstallDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedMarketMod is not { } mod)
        {
            return;
        }
        if (viewModel.PrepareMarketInstallTargetsCommand.IsRunning
            || marketInstallDialogTask is not null
            || Interlocked.Exchange(ref marketInstallDialogOpening, 1) != 0)
        {
            return;
        }

        try
        {
            await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);
            if (!IsVisible || !ReferenceEquals(viewModel.SelectedMarketMod, mod))
            {
                return;
            }
            using var dialogViewModel = new MarketInstallDialogViewModel(
                viewModel,
                mod.DisplayName ?? mod.Name);
            await dialogViewModel.LoadPlanAsync();
            marketInstallDialogTask = OverlayDialog.ShowCustomAsync<
                MarketInstallDialogView,
                MarketInstallDialogViewModel,
                bool>(
                dialogViewModel,
                OverlayHostId,
                CreateOverlayOptions());
            await marketInstallDialogTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            marketInstallDialogTask = null;
            Interlocked.Exchange(ref marketInstallDialogOpening, 0);
        }
    }

    internal async Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string target,
        MainViewModel viewModel,
        bool canConfirm = true,
        bool isDangerous = false,
        string? confirmText = null,
        string? cancelText = null)
    {
        var dialogViewModel = new ConfirmationDialogViewModel(
            title,
            message,
            target,
            confirmText ?? viewModel.Loc["Confirm"],
            cancelText ?? viewModel.Loc["Cancel"],
            canConfirm,
            isDangerous);
        return await OverlayDialog.ShowCustomAsync<
            ConfirmationDialogView,
            ConfirmationDialogViewModel,
            bool>(
            dialogViewModel,
            OverlayHostId,
            CreateOverlayOptions());
    }

    internal void EnqueueExternalMessage(string message) =>
        _ = HandleExternalMessageAsync(message);

    internal void ActivateForExternalCommand()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Show();
        Activate();
    }

    internal void SuspendExternalCommands() => externalCommandReady = false;

    internal void ResumeExternalCommands()
    {
        if (closeAfterDispose || closeRequested)
        {
            return;
        }

        externalCommandReady = true;
        App.DrainExternalMessages();
    }

    private async Task HandleExternalMessageAsync(string message)
    {
        await externalCommandGate.WaitAsync();
        try
        {
            if (!externalCommandReady)
            {
                return;
            }

            ActivateForExternalCommand();
            if (string.Equals(message, Program.ActivateMessage, StringComparison.Ordinal))
            {
                return;
            }
            if (DataContext is not MainViewModel viewModel)
            {
                return;
            }

            ProtocolCommand command;
            try
            {
                command = viewModel.PrepareProtocolCommand(ProtocolCommandParser.Parse(message));
            }
            catch (Exception exception) when (exception is ProtocolCommandException
                or InvalidOperationException)
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["InvalidExternalCommand"]}: {exception.Message}";
                return;
            }

            if (!viewModel.CanExecuteProtocolCommand(command, out string rejectionReason))
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["InvalidExternalCommand"]}: {rejectionReason}";
                return;
            }

            if (command.RequiresConfirmation)
            {
                var confirmed = await ShowConfirmationAsync(
                    viewModel.Loc["ExternalCommandTitle"],
                    viewModel.Loc["ExternalCommandMessage"],
                    viewModel.DescribeProtocolCommand(command),
                    viewModel,
                    isDangerous: command.Kind is ProtocolCommandKind.ResetApplicationSettings
                        or ProtocolCommandKind.DeleteModSettings
                        or ProtocolCommandKind.DeleteAllModSettings,
                    confirmText: viewModel.Loc["RunCommand"]);
                if (!confirmed)
                {
                    return;
                }
            }

            if (!externalCommandReady)
            {
                return;
            }
            if (!viewModel.CanExecuteProtocolCommand(command, out rejectionReason))
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["InvalidExternalCommand"]}: {rejectionReason}";
                return;
            }

            try
            {
                await viewModel.ExecuteProtocolCommandAsync(command);
            }
            catch (OperationCanceledException) when (!externalCommandReady)
            {
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or InvalidOperationException
                or UnauthorizedAccessException
                or HttpRequestException
                or KeyNotFoundException
                or ArgumentException
                or System.Text.Json.JsonException
                or Win32Exception)
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
            }
        }
        finally
        {
            externalCommandGate.Release();
        }
    }

    private void OpenExternalUrl(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string value }
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
            }
        }
    }

    private async void ConfirmLaunch(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.CanAttemptLaunch)
        {
            return;
        }
        if (viewModel.LaunchPreflight.CanLaunchNormally)
        {
            await viewModel.LaunchGameCommand.ExecuteAsync(null);
            return;
        }
        await ShowLaunchIssuesAsync(viewModel);
    }

    private async void ShowLaunchIssues(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel && viewModel.HasLaunchIssues)
        {
            await ShowLaunchIssuesAsync(viewModel);
        }
    }

    private async Task ShowLaunchIssuesAsync(MainViewModel viewModel)
    {
        if (launchIssuesDialogTask is not null)
        {
            await launchIssuesDialogTask;
            return;
        }
        var canForce = viewModel.LaunchPreflight.CanForceLaunch;
        var dialogViewModel = new LaunchIssuesDialogViewModel(
            canForce ? viewModel.Loc["LaunchWarningTitle"] : viewModel.Loc["LaunchBlockedTitle"],
            canForce ? viewModel.Loc["LaunchWarningMessage"] : viewModel.Loc["LaunchBlocked"],
            viewModel.CreateLaunchIssueItems(),
            viewModel.Loc["ForceLaunch"],
            viewModel.Loc["Cancel"],
            viewModel.Loc["DoNotRemindLaunchWarnings"],
            canForce);
        try
        {
            launchIssuesDialogTask = OverlayDialog.ShowCustomAsync<
                LaunchIssuesDialogView,
                LaunchIssuesDialogViewModel,
                LaunchIssuesDialogResult>(
                dialogViewModel,
                OverlayHostId,
                CreateOverlayOptions());
            var result = await launchIssuesDialogTask;
            if (result is null || !result.ForceLaunch)
            {
                return;
            }
            if (result.DoNotRemind)
            {
                await viewModel.AcknowledgeLaunchWarningsCommand.ExecuteAsync(null);
            }
            await viewModel.ForceLaunchGameCommand.ExecuteAsync(null);
        }
        finally
        {
            launchIssuesDialogTask = null;
        }
    }

    private static OverlayDialogOptions CreateOverlayOptions() => new()
    {
        CanLightDismiss = false,
        CanDragMove = false,
        IsCloseButtonVisible = true,
        CanResize = false
    };
}
