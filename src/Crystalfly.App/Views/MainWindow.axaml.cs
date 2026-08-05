using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Services;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.App.Runtime;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
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
    private readonly MotionCoordinator motionCoordinator;
    private Task? disposeBeforeCloseTask;
    private Task? closeConfirmationTask;
    private Task<bool>? marketInstallDialogTask;
    private Task<LaunchIssuesDialogResult?>? launchIssuesDialogTask;
    private int marketInstallDialogOpening;
    private readonly WindowToastManager toastManager;
    private readonly SemaphoreSlim externalCommandGate = new(1, 1);
    private Action<string>? toastRequestedHandler;
    private Action? graphModRemovalRequestedHandler;
    private Action? gameDirectoryDiscoveryRequestedHandler;
    private Action<GameDirectoryCandidateItemViewModel>? steamDirectoryRiskRequestedHandler;
    private MainViewModel? toastViewModel;
    private bool externalCommandReady;
    private Point? speedrunSwipeStart;

    internal bool IsExternalCommandReady => externalCommandReady;

    public MainWindow()
    {
        InitializeComponent();
        MainOverlayDialogHost.Children.CollectionChanged += OnOverlayHostChildrenChanged;
        toastManager = new WindowToastManager(this) { MaxItems = 3 };
        motionCoordinator = new MotionCoordinator(
            this,
            () => DataContext is MainViewModel { EffectiveMotionPreference: UiMotionPreference.FollowSystem },
            () => DataContext is MainViewModel { EffectiveMotionPreference: UiMotionPreference.Reduced });
        DataContextChanged += OnDataContextChanged;
        OnDataContextChanged(this, EventArgs.Empty);
        Opened += OnOpened;
        Closed += OnClosed;
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
        motionCoordinator.Start();
        ApplyWin11RoundedCorners();
        if (DataContext is MainViewModel viewModel)
        {
            var initialized = false;
            try
            {
                await viewModel.InitializeAsync();
                initialized = true;
                motionCoordinator.RegisterMotionTargets(animateNewTargets: true);
                Dispatcher.UIThread.Post(
                    () => motionCoordinator.AnimateSpeedrunTabIndicator(SpeedrunTabIndicator),
                    DispatcherPriority.Render);
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

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        uint dwAttribute,
        ref int pvAttribute,
        uint cbAttribute);

    private const uint DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    private void ApplyWin11RoundedCorners()
    {
        var platformHandle = TryGetPlatformHandle();
        var handle = platformHandle?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero
            || !string.Equals(
                platformHandle?.HandleDescriptor,
                "HWND",
                StringComparison.Ordinal))
        {
            return;
        }

        var preference = DwmwcpRound;
        _ = DwmSetWindowAttribute(
            handle,
            DwmwaWindowCornerPreference,
            ref preference,
            sizeof(int));
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        Closed -= OnClosed;
        MainOverlayDialogHost.Children.CollectionChanged -= OnOverlayHostChildrenChanged;
        motionCoordinator.Shutdown();
    }
    private async void OnSaveSlotSelectionChanged(
        object? sender,
        SelectionChangedEventArgs eventArgs)
    {
        if (sender is not ComboBox
            {
                DataContext: SaveEditorViewModel editor,
                SelectedItem: string slot
            })
        {
            return;
        }

        try
        {
            await editor.SelectSlotAsync(slot);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or System.Security.Cryptography.CryptographicException)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
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

    private async void OnAccentColorClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: AccentColorOptionViewModel option }
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!option.IsCustom)
        {
            viewModel.Settings.SetAccentColor(option.Hex);
            return;
        }

        var originalColor = viewModel.Settings.AccentColor;
        var dialog = new AccentColorDialogViewModel(
            viewModel.Loc["AccentColorPickerTitle"],
            viewModel.Loc["AccentOriginal"],
            viewModel.Loc["AccentNew"],
            viewModel.Loc["AccentHex"],
            viewModel.Loc["AccentInvalid"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"],
            originalColor,
            viewModel.Settings.PreviewAccentColor);
        var selected = await OverlayDialog.ShowCustomAsync<
            AccentColorDialogView,
            AccentColorDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (selected is null)
        {
            viewModel.Settings.RestoreAccentColor();
            return;
        }

        viewModel.Settings.SetAccentColor(selected);
    }

    private void OnGlobalBackgroundScopeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Settings.SelectedBackgroundScope = viewModel.Settings.BackgroundScopeOptions.First(option =>
                option.Value == BackgroundEditScope.Global);
        }
    }

    private void OnInstanceBackgroundScopeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel { CanEditInstanceBackground: true } viewModel)
        {
            viewModel.Settings.SelectedBackgroundScope = viewModel.Settings.BackgroundScopeOptions.First(option =>
                option.Value == BackgroundEditScope.CurrentInstance);
        }
    }

    private async void OnSelectBackgroundImageClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["BackgroundSelect"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["BackgroundImage"])
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.Settings.SetBackgroundImageAsync(path);
        }
    }

    private async void OnRemoveBackgroundImageClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.Settings.RemoveBackgroundImageAsync();
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
            if (graphModRemovalRequestedHandler is not null)
            {
                toastViewModel.GraphModRemovalRequested -= graphModRemovalRequestedHandler;
            }
            if (gameDirectoryDiscoveryRequestedHandler is not null)
            {
                toastViewModel.Instances.GameDirectoryDiscoveryRequested -= gameDirectoryDiscoveryRequestedHandler;
            }
            if (steamDirectoryRiskRequestedHandler is not null)
            {
                toastViewModel.Instances.SteamDirectoryRiskRequested -= steamDirectoryRiskRequestedHandler;
            }
        }

        toastRequestedHandler = null;
        gameDirectoryDiscoveryRequestedHandler = null;
        steamDirectoryRiskRequestedHandler = null;
        graphModRemovalRequestedHandler = null;
        toastViewModel = DataContext as MainViewModel;
        if (toastViewModel is not null)
        {
            var owner = toastViewModel;
            toastRequestedHandler = message => ShowToast(owner, message, NotificationType.Success);
            graphModRemovalRequestedHandler = () => _ = ConfirmGraphModRemovalAsync(owner);
            gameDirectoryDiscoveryRequestedHandler = () => _ = ShowGameDirectoryDiscoveryAsync(owner);
            steamDirectoryRiskRequestedHandler = candidate => _ = ShowSteamDirectoryRiskAsync(owner, candidate);
            toastViewModel.ToastRequested += toastRequestedHandler;
            toastViewModel.PropertyChanged += OnToastViewModelPropertyChanged;
            toastViewModel.GraphModRemovalRequested += graphModRemovalRequestedHandler;
            toastViewModel.Instances.GameDirectoryDiscoveryRequested += gameDirectoryDiscoveryRequestedHandler;
            toastViewModel.Instances.SteamDirectoryRiskRequested += steamDirectoryRiskRequestedHandler;
        }
    }

    private async Task ConfirmGraphModRemovalAsync(MainViewModel viewModel)
    {
        try
        {
            await ConfirmModRemovalAsync(viewModel, bulk: false);
        }
        catch (InvalidOperationException exception)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private void OnToastViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainViewModel.EffectiveMotionPreference))
        {
            motionCoordinator.UpdateMotionPreference(replayVisiblePages: true);
            motionCoordinator.AnimateSpeedrunTabIndicator(SpeedrunTabIndicator);
        }
        else if (eventArgs.PropertyName == nameof(MainViewModel.CurrentSpeedrunTab))
        {
            Dispatcher.UIThread.Post(
                () => motionCoordinator.AnimateSpeedrunTabIndicator(SpeedrunTabIndicator),
                DispatcherPriority.Render);
        }
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
                Dispatcher.UIThread.Post(RegisterToastMotionTargets, DispatcherPriority.Render);
            }
        });

    private void OnOverlayHostChildrenChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.NewItems is null)
        {
            return;
        }
        foreach (var dialog in eventArgs.NewItems.OfType<Control>())
        {
            dialog.Classes.Add("cfp-dialog-motion");
            motionCoordinator.RegisterMotionTarget(dialog, animate: true);
        }
    }

    private void RegisterToastMotionTargets()
    {
        foreach (var card in toastManager.GetVisualDescendants().OfType<ToastCard>())
        {
            card.Classes.Add("cfp-toast-motion");
            motionCoordinator.RegisterMotionTarget(card, animate: true);
        }
    }

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
            if (graphModRemovalRequestedHandler is not null)
            {
                toastViewModel.GraphModRemovalRequested -= graphModRemovalRequestedHandler;
            }
            if (gameDirectoryDiscoveryRequestedHandler is not null)
            {
                toastViewModel.Instances.GameDirectoryDiscoveryRequested -= gameDirectoryDiscoveryRequestedHandler;
            }
            if (steamDirectoryRiskRequestedHandler is not null)
            {
                toastViewModel.Instances.SteamDirectoryRiskRequested -= steamDirectoryRiskRequestedHandler;
            }
            toastRequestedHandler = null;
            graphModRemovalRequestedHandler = null;
            gameDirectoryDiscoveryRequestedHandler = null;
            steamDirectoryRiskRequestedHandler = null;
            toastViewModel = null;
        }
    }
    private async void ShowHistoricalManifestDownloadDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new HistoricalManifestDialogViewModel(
            viewModel.DownloadBuilds,
            viewModel.Loc["HistoricalManifestTitle"],
            viewModel.Loc["HistoricalManifestHint"],
            viewModel.Loc["HistoricalManifestId"],
            viewModel.Loc["HistoricalInstanceName"],
            viewModel.Loc["HistoricalManifestKnown"],
            viewModel.Loc["HistoricalManifestUnverified"],
            viewModel.Loc["InvalidHistoricalManifest"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var request = await OverlayDialog.ShowCustomAsync<
            HistoricalManifestDialogView,
            HistoricalManifestDialogViewModel,
            HistoricalManifestDownloadRequest?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (request is null)
        {
            return;
        }

        try
        {
            await viewModel.EnqueueCustomSteamManifestAsync(request, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private async void AddCustomGameDirectory(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await PickAndAddGameDirectoryAsync(viewModel);
        }
    }

    private async Task PickAndAddGameDirectoryAsync(MainViewModel viewModel)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = viewModel.Loc["SelectGameDirectory"],
            AllowMultiple = false
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.Instances.AddCustomGameDirectoryAsync(path);
            await ShowGameDirectoryDiscoveryAsync(viewModel);
        }
    }

    private async Task ShowGameDirectoryDiscoveryAsync(MainViewModel viewModel)
    {
        var dialog = new GameDirectoryDiscoveryDialogViewModel(
            viewModel.Loc["GameDirectoryDiscoveryTitle"],
            viewModel.Loc["GameDirectoryDiscoveryHint"],
            viewModel.Instances.GameDirectoryCandidates,
            viewModel.Loc["ScanGameDirectories"],
            viewModel.Loc["AddGameDirectory"],
            viewModel.Loc["ConfirmAddDirectories"],
            viewModel.Loc["SkipForNow"]);
        var result = await OverlayDialog.ShowCustomAsync<GameDirectoryDiscoveryDialogView,
            GameDirectoryDiscoveryDialogViewModel, GameDirectoryDiscoveryDialogResult>(
            dialog, OverlayHostId, CreateOverlayOptions());
        switch (result)
        {
            case GameDirectoryDiscoveryDialogResult.StartScan:
                await viewModel.Instances.ScanGameDirectoriesCommand.ExecuteAsync(null);
                await ShowGameDirectoryDiscoveryAsync(viewModel);
                break;
            case GameDirectoryDiscoveryDialogResult.AddCustom:
                await PickAndAddGameDirectoryAsync(viewModel);
                break;
            case GameDirectoryDiscoveryDialogResult.Confirm:
                await viewModel.Instances.ConfirmGameDirectoryCandidatesCommand.ExecuteAsync(null);
                break;
        }
    }

    private async Task ShowSteamDirectoryRiskAsync(MainViewModel viewModel, GameDirectoryCandidateItemViewModel candidate)
    {
        var dialog = new ThreeChoiceDialogViewModel(
            viewModel.Loc["SteamDirectoryRiskTitle"],
            viewModel.Loc["SteamDirectoryRiskHint"],
            candidate.Path,
            viewModel.Loc["MigrateInstance"],
            viewModel.Loc["AcceptSteamRisk"],
            viewModel.Loc["Cancel"],
            primaryDangerous: false);
        var result = await OverlayDialog.ShowCustomAsync<SteamDirectoryRiskDialogView,
            ThreeChoiceDialogViewModel, ThreeChoiceDialogResult>(dialog, OverlayHostId, CreateOverlayOptions());
        if (result == ThreeChoiceDialogResult.Secondary)
        {
            await viewModel.Instances.AcceptSteamGameDirectoryAsync(candidate);
            return;
        }
        if (result != ThreeChoiceDialogResult.Primary)
        {
            return;
        }
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = viewModel.Loc["SelectMigrationTarget"],
            AllowMultiple = false
        });
        var target = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(target))
        {
            await viewModel.Instances.MigrateSteamGameDirectoryAsync(candidate, target);
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
        await viewModel.Instances.CloneSelectedInstanceCommand.ExecuteAsync(null);
        if (viewModel.SelectedInstance is { } selected
            && !string.Equals(selected.Id, instance.Id, StringComparison.Ordinal))
        {
            viewModel.CurrentPage = "Launch";
        }
    }

    private async void ShowCreateSpeedrunEnvironmentDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedSpeedrunTemplate: { } template } viewModel)
        {
            return;
        }
        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["SpeedrunCreate"],
            viewModel.Loc["SpeedrunHint"],
            string.IsNullOrWhiteSpace(viewModel.SpeedrunEnvironmentName)
                ? $"{template.Name} Speedrun"
                : viewModel.SpeedrunEnvironmentName,
            viewModel.Loc["SpeedrunEnvironmentName"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        string? name = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        viewModel.SpeedrunEnvironmentName = name;
        await viewModel.CreateSpeedrunEnvironmentCommand.ExecuteAsync(null);
    }

    private async void ConfirmDeleteInstance(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstanceItemViewModel instance })
        {
            return;
        }
        await ConfirmDeleteInstanceAsync(viewModel, instance);
    }

    private async void ConfirmDeleteSelectedInstance(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            await ConfirmDeleteInstanceAsync(viewModel, instance);
        }
    }

    private async Task ConfirmDeleteInstanceAsync(MainViewModel viewModel, InstanceItemViewModel instance)
    {
        if (viewModel.Instances.SelectedGameDirectory?.IsSteam == true)
        {
            var dialog = new ThreeChoiceDialogViewModel(
                viewModel.Loc["DeleteSteamInstanceTitle"],
                viewModel.Loc["DeleteSteamInstanceHint"],
                instance.RootPath,
                viewModel.Loc["PermanentlyDeleteDirectory"],
                viewModel.Loc["UnregisterOnly"],
                viewModel.Loc["Cancel"],
                primaryDangerous: true);
            var result = await OverlayDialog.ShowCustomAsync<SteamInstanceDeletionDialogView,
                ThreeChoiceDialogViewModel, ThreeChoiceDialogResult>(dialog, OverlayHostId, CreateOverlayOptions());
            if (result == ThreeChoiceDialogResult.Secondary)
            {
                await viewModel.Instances.UnregisterCurrentSteamDirectoryAsync();
            }
            else if (result == ThreeChoiceDialogResult.Primary)
            {
                await viewModel.Instances.DeleteInstanceCommand.ExecuteAsync(instance);
                if (!Directory.Exists(instance.RootPath))
                {
                    await viewModel.Instances.UnregisterCurrentSteamDirectoryAsync();
                }
            }
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
            await viewModel.Instances.DeleteInstanceCommand.ExecuteAsync(instance);
        }
    }

    private async void RenameSelectedInstance(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            return;
        }
        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["RenameInstance"],
            viewModel.Loc["RenameInstanceHint"],
            instance.Name,
            viewModel.Loc["HistoricalInstanceName"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var name = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (!string.IsNullOrWhiteSpace(name))
        {
            await viewModel.Instances.RenameInstanceCommand.ExecuteAsync(name);
        }
    }

    private void OpenSelectedInstanceFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            OpenSafeAbsoluteFolder(instance.RootPath, instance.RootPath, create: false, viewModel);
        }
    }

    private void OpenSpeedrunReport(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SpeedrunReportPath: { } reportPath } viewModel
            || !File.Exists(reportPath))
        {
            if (DataContext is MainViewModel missingViewModel)
            {
                missingViewModel.ErrorMessage = missingViewModel.Loc["SpeedrunReportPathMissing"];
            }
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private async void CopySpeedrunReportPath(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SpeedrunReportPath: { } reportPath } viewModel
            || Clipboard is null
            || !File.Exists(reportPath))
        {
            if (DataContext is MainViewModel missingViewModel)
            {
                missingViewModel.ErrorMessage = missingViewModel.Loc["SpeedrunReportPathMissing"];
            }
            return;
        }

        try
        {
            await Clipboard.SetTextAsync(reportPath);
            ShowToast(viewModel, viewModel.Loc["SpeedrunReportPathCopied"], NotificationType.Success);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private void OpenSelectedSaveFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || viewModel.GetSelectedInstanceSaveDirectory() is not { } saveDirectory)
        {
            return;
        }
        OpenSafeAbsoluteFolder(
            Path.Combine(viewModel.VersionRoot, ".crystalfly"),
            saveDirectory,
            create: true,
            viewModel);
    }

    private void OpenSelectedModFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel
            || viewModel.GetSelectedInstanceModDirectory() is not { } modDirectory)
        {
            return;
        }
        OpenSafeAbsoluteFolder(instance.RootPath, modDirectory, create: false, viewModel);
    }

    private async void ConfirmCompleteInstanceFiles(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["CompleteInstanceFiles"],
            viewModel.Loc["CompleteInstanceFilesHint"],
            instance.Name,
            viewModel,
            isDangerous: false);
        if (confirmed)
        {
            await viewModel.EnqueueSelectedInstanceRepairCommand.ExecuteAsync(null);
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

    private void OnSpeedrunWorkspacePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not ScrollViewer host
            || eventArgs.GetCurrentPoint(host).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed
            || !CanBeginSpeedrunSwipe(eventArgs))
        {
            return;
        }

        speedrunSwipeStart = eventArgs.GetPosition(host);
        eventArgs.Pointer.Capture(host);
    }

    private void OnSpeedrunWorkspacePointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (sender is not ScrollViewer host || speedrunSwipeStart is not { } start)
        {
            return;
        }

        speedrunSwipeStart = null;
        eventArgs.Pointer.Capture(null);
        Point end = eventArgs.GetPosition(host);
        double horizontal = end.X - start.X;
        double vertical = end.Y - start.Y;
        if (Math.Abs(horizontal) < 64
            || Math.Abs(horizontal) <= Math.Abs(vertical) * 1.25
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SelectSpeedrunTabCommand.Execute(horizontal < 0 ? "Activity" : "Environment");
        eventArgs.Handled = true;
    }

    private static bool CanBeginSpeedrunSwipe(PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.Source is not Avalonia.Visual visual)
        {
            return true;
        }

        return visual.FindAncestorOfType<Button>() is null
            && visual.FindAncestorOfType<ComboBox>() is null
            && visual.FindAncestorOfType<TextBox>() is null
            && visual.FindAncestorOfType<ScrollBar>() is null;
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

    private async void ShowCreateModPackDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new ModPackEditorDialogViewModel(
            viewModel.Loc["CreateModPack"],
            viewModel.Loc["CreateModPackHint"],
            string.Empty,
            ModPresetApplyMode.Append,
            viewModel.Loc["PresetName"],
            viewModel.Loc["PresetMode"],
            viewModel.Loc["PresetModeAppend"],
            viewModel.Loc["PresetModeExact"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var result = await OverlayDialog.ShowCustomAsync<
            ModPackEditorDialogView,
            ModPackEditorDialogViewModel,
            ModPackEditorDialogResult?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (result is null)
        {
            return;
        }

        viewModel.PresetName = result.Name;
        viewModel.SelectedPresetModeOption = viewModel.PresetModeOptions.First(option => option.Value == result.ApplyMode);
        await viewModel.CreatePresetCommand.ExecuteAsync(null);
    }

    private async void ShowCopyModPackDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedPreset: { } preset } viewModel)
        {
            return;
        }

        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["CopyPreset"],
            viewModel.Loc["CopyModPackHint"],
            $"{preset.Name} - {viewModel.Loc["CopySuffix"]}",
            viewModel.Loc["PresetName"],
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

        viewModel.PresetCopyName = name;
        await viewModel.CopySelectedPresetCommand.ExecuteAsync(null);
    }

    private async void ShowImportSharedModPackDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["ImportSharedPreset"],
            viewModel.Loc["ImportSharedPresetHint"],
            string.Empty,
            viewModel.Loc["PresetShareCode"],
            viewModel.Loc["ImportSharedPreset"],
            viewModel.Loc["Cancel"]);
        var code = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        viewModel.PresetShareCode = code;
        await viewModel.ImportSharedPresetCommand.ExecuteAsync(null);
    }

    private async void ShareAndCopyPresetLink(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        await viewModel.ShareSelectedPresetCommand.ExecuteAsync(null);
        if (viewModel.ErrorMessage is null && await TryCopyPresetShareLinkAsync(viewModel))
        {
            ShowToast(viewModel, viewModel.Loc["PresetShareLinkCopied"], NotificationType.Success);
        }
        else if (viewModel.ErrorMessage is null)
        {
            ShowToast(viewModel, viewModel.Loc["PresetShared"], NotificationType.Success);
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

    private async Task<bool> TryCopyPresetShareLinkAsync(MainViewModel viewModel)
    {
        if (Clipboard is null
            || !Uri.TryCreate(viewModel.LastPresetShareUrl, UriKind.Absolute, out var link)
            || link.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        try
        {
            await Clipboard.SetTextAsync(link.AbsoluteUri);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
            return false;
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
            step.ModId,
            step.ModId,
            string.Join(" · ", new[] { step.Version, step.LoaderId }
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            step.State,
            step.IsUnresolved || step.IsBlocked
                ? DependencyGraphNodeState.Attention
                : DependencyGraphNodeState.Normal,
            step.Action)).ToArray();

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
                plan.Items.Any(item => item.Action != ModDependencyRepairAction.Unresolved),
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
                node.ModId,
                item?.PrimaryName ?? node.ReceiptName,
                item?.SecondaryName ?? node.ModId,
                node.Enabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"],
                DependencyGraphNodeState.Attention,
                node.Kind == ModRemovalImpactKind.WillRemove
                    ? viewModel.Loc["WillDelete"]
                    : viewModel.Loc["DependenciesWillBeMissing"],
                node.RelatedToModId is null ? null : [node.RelatedToModId]);
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

    private static void OpenSafeAbsoluteFolder(
        string allowedRoot,
        string targetPath,
        bool create,
        MainViewModel viewModel)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot));
            var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
            if (!string.Equals(root, target, StringComparison.OrdinalIgnoreCase)
                && !target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The requested folder is outside the selected instance data root.");
            }
            if (create)
            {
                Directory.CreateDirectory(target);
            }
            if (!Directory.Exists(target))
            {
                throw new DirectoryNotFoundException($"Directory '{target}' was not found.");
            }
            for (var current = new DirectoryInfo(target); current is not null; current = current.Parent)
            {
                RejectReparsePoint(current.FullName);
                if (string.Equals(current.FullName, root, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
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
        var repairItems = plan.Items.ToDictionary(item => item.ModId, StringComparer.OrdinalIgnoreCase);
        var prerequisitesByRequiredMod = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in plan.Items)
        {
            foreach (var requiredById in item.RequiredByModIds)
            {
                if (!prerequisitesByRequiredMod.TryGetValue(requiredById, out var prerequisites))
                {
                    prerequisites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    prerequisitesByRequiredMod[requiredById] = prerequisites;
                }
                prerequisites.Add(item.ModId);
            }
        }

        var allIds = repairItems.Keys
            .Concat(prerequisitesByRequiredMod.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        return allIds.Select(id =>
        {
            installed.TryGetValue(id, out var current);
            prerequisitesByRequiredMod.TryGetValue(id, out var prerequisites);
            if (!repairItems.TryGetValue(id, out var item))
            {
                return new DependencyPlanNodeViewModel(
                    id,
                    current?.PrimaryName ?? id,
                    current?.SecondaryName ?? string.Empty,
                    current is null ? viewModel.Loc["Missing"] : current.IsEnabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"],
                    current is null
                        ? DependencyGraphNodeState.Missing
                        : current.IsExternal
                            ? DependencyGraphNodeState.External
                            : current.IsEnabled
                                ? DependencyGraphNodeState.Normal
                                : DependencyGraphNodeState.Disabled,
                    PrerequisiteIds: prerequisites?.ToArray());
            }

            var action = item.Action switch
            {
                ModDependencyRepairAction.ReEnable => viewModel.Loc["WillReEnable"],
                ModDependencyRepairAction.DownloadAndInstall => viewModel.Loc["WillDownloadAndInstall"],
                _ => viewModel.Loc["CannotRepair"]
            };
            var state = item.Action == ModDependencyRepairAction.Unresolved
                ? DependencyGraphNodeState.Attention
                : current is null
                    ? DependencyGraphNodeState.Missing
                    : current.IsExternal
                        ? DependencyGraphNodeState.External
                        : current.IsEnabled
                            ? DependencyGraphNodeState.Normal
                            : DependencyGraphNodeState.Disabled;
            return new DependencyPlanNodeViewModel(
                item.ModId,
                current?.PrimaryName ?? item.Name,
                current?.SecondaryName ?? string.Empty,
                current is null ? viewModel.Loc["Missing"] : current.IsEnabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"],
                state,
                action,
                prerequisites?.ToArray());
        }).ToArray();
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

    private async void ConfirmDeleteSnapshot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedSnapshot is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmSnapshotDeleteTitle"],
            viewModel.Loc["ConfirmSnapshotDeleteMessage"],
            viewModel.SelectedSnapshot.Name,
            viewModel,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.DeleteSnapshotCommand.ExecuteAsync(null);
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

    private void OpenSpeedrunRun(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string value }
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(string.Equals(uri.Host, "speedrun.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "www.speedrun.com", StringComparison.OrdinalIgnoreCase)))
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

    private OverlayDialogOptions CreateOverlayOptions() => new()
    {
        TopLevelHashCode = GetHashCode(),
        CanLightDismiss = false,
        CanDragMove = false,
        IsCloseButtonVisible = true,
        CanResize = false
    };
}
