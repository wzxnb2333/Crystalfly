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
        // 行的内容容器(ContentPresenter)会在行悬停/选中时拦截空白区域的指针按下,
        // 且 ListBoxItem 会在冒泡阶段标记事件已处理;因此选择逻辑必须在隧道阶段、
        // 以 ListBox 为根注册,才能覆盖行内任意非交互区域。
        InstalledModsList.AddHandler(
            InputElement.PointerPressedEvent,
            OnInstalledModPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
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
            viewModel.GuardCodePrompt = PromptForSteamGuardCodeAsync;
            viewModel.DeviceConfirmationPrompt = PromptForSteamDeviceConfirmationAsync;
            viewModel.ExternalContentConfirmPrompt = (title, message, confirmText) =>
                ShowConfirmationAsync(title, message, string.Empty, viewModel, confirmText: confirmText);
            viewModel.CatalogMatchPrompt = ShowModCatalogMatchDialogAsync;
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
                viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
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
                viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
            }
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
        if (DataContext is MainViewModel { DownloadCenter.HasUnfinishedDownloads: true } viewModel)
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

        if (viewModel.DownloadCenter.HasUnfinishedDownloads)
        {
            bool confirmed = await ShowConfirmationAsync(
                viewModel.Loc["ConfirmCloseDownloadsTitle"],
                viewModel.Loc["ConfirmCloseDownloadsMessage"],
                string.IsNullOrWhiteSpace(viewModel.DownloadCenter.ActiveDownloadSummary)
                    ? viewModel.Loc["DownloadQueue"]
                    : viewModel.DownloadCenter.ActiveDownloadSummary,
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
                string.IsNullOrWhiteSpace(viewModel.DownloadCenter.ActiveDownloadSummary)
                    ? viewModel.Loc["DownloadQueue"]
                    : viewModel.DownloadCenter.ActiveDownloadSummary,
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
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
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
    private void OpenSelectedModFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel
            || viewModel.GetSelectedInstanceModDirectory() is not { } modDirectory)
        {
            return;
        }
        OpenSafeAbsoluteFolder(instance.RootPath, modDirectory, create: false, viewModel);
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

    internal async Task<string?> PromptForSteamGuardCodeAsync(
        string title,
        string message,
        bool previousCodeWasIncorrect)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return null;
        }
        var dialogViewModel = new SteamGuardDialogViewModel(
            title,
            message,
            viewModel.Loc["SteamGuardCodePlaceholder"],
            viewModel.Loc["SteamLogin"],
            viewModel.Loc["Cancel"],
            viewModel.Loc["SteamGuardIncorrect"],
            previousCodeWasIncorrect);
        return await OverlayDialog.ShowCustomAsync<
            SteamGuardDialogView,
            SteamGuardDialogViewModel,
            string?>(
            dialogViewModel,
            OverlayHostId,
            CreateOverlayOptions());
    }

    internal async Task<bool?> PromptForSteamDeviceConfirmationAsync()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return null;
        }
        var dialogViewModel = new SteamDeviceConfirmationDialogViewModel(
            viewModel.Loc["SteamDeviceConfirmationTitle"],
            viewModel.Loc["SteamDeviceConfirmationMessage"],
            viewModel.Loc["SteamDeviceConfirmationAccept"],
            viewModel.Loc["SteamDeviceConfirmationSwitchToCode"],
            viewModel.Loc["Cancel"]);
        return await OverlayDialog.ShowCustomAsync<
            SteamDeviceConfirmationDialogView,
            SteamDeviceConfirmationDialogViewModel,
            bool?>(
            dialogViewModel,
            OverlayHostId,
            CreateOverlayOptions());
    }

    internal async Task<ModManifest?> ShowModCatalogMatchDialogAsync(
        IReadOnlyList<ModManifest> candidates,
        string modName)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return null;
        }
        var dialogViewModel = new ModCatalogMatchDialogViewModel(
            viewModel.Loc["MatchToCatalogTitle"],
            string.Format(viewModel.Loc["MatchToCatalogMessage"], modName),
            candidates,
            viewModel.Loc["MatchToCatalog"],
            viewModel.Loc["Cancel"]);
        return await OverlayDialog.ShowCustomAsync<
            ModCatalogMatchDialogView,
            ModCatalogMatchDialogViewModel,
            ModManifest?>(
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
                viewModel.ErrorMessage = $"{viewModel.Loc["InvalidExternalCommand"]}: {viewModel.Loc.ErrorMessageFor(exception)}";
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
                viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
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
                viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
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
