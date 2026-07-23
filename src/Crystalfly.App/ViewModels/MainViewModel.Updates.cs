using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.App.Updates;
using Crystalfly.Core.Updates;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private static readonly Uri UpdateManifestUri = new(
        "https://github.com/wzxnb2333/Crystalfly/releases/latest/download/update-manifest.v1.json");
    private ApplicationUpdateService? applicationUpdateService;
    private ApplicationUpdateCheckStatus? lastApplicationUpdateStatus;
    private string? lastAvailableApplicationVersion;

    public string ApplicationVersion =>
        typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public bool HasAvailableApplicationUpdate => AvailableApplicationUpdate is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailableApplicationUpdate))]
    public partial UpdateManifest? AvailableApplicationUpdate { get; private set; }

    [ObservableProperty]
    public partial string ApplicationUpdateStatus { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCheckingForApplicationUpdates { get; private set; }

    [ObservableProperty]
    public partial bool IsAutomaticUpdateCheckEnabled { get; set; } = true;

    internal void InitializeApplicationUpdateSettings()
    {
        IsAutomaticUpdateCheckEnabled = settings.CheckForUpdates;
        lastApplicationUpdateStatus = null;
        RefreshApplicationUpdateText();
    }

    internal void RefreshApplicationUpdateText()
    {
        if (lastApplicationUpdateStatus is not { } status)
        {
            ApplicationUpdateStatus = string.Format(
                System.Globalization.CultureInfo.CurrentUICulture,
                Loc["CurrentVersionFormat"],
                ApplicationVersion);
            return;
        }
        ApplicationUpdateStatus = string.Format(
            System.Globalization.CultureInfo.CurrentUICulture,
            status switch
            {
                ApplicationUpdateCheckStatus.Offline => Loc["OfflineMode"],
                ApplicationUpdateCheckStatus.Disabled => Loc["UpdateChecksDisabled"],
                ApplicationUpdateCheckStatus.NotDue => Loc["UpdateCheckNotDue"],
                ApplicationUpdateCheckStatus.UpToDate => Loc["ApplicationUpToDate"],
                ApplicationUpdateCheckStatus.VersionSkipped => Loc["UpdateVersionSkipped"],
                ApplicationUpdateCheckStatus.UpdateAvailable => Loc["ApplicationUpdateAvailableFormat"],
                _ => Loc["UpdateCheckFailed"]
            },
            lastAvailableApplicationVersion ?? string.Empty);
    }

    internal async Task<ApplicationUpdateCheckResult> CheckApplicationUpdateAsync(
        bool force,
        CancellationToken cancellationToken = default)
    {
        if (IsCheckingForApplicationUpdates)
        {
            return new(ApplicationUpdateCheckStatus.NotDue);
        }

        IsCheckingForApplicationUpdates = true;
        try
        {
            ApplicationUpdateCheckResult result = applicationUpdateCheckOverride is not null
                ? await applicationUpdateCheckOverride(settings, force, cancellationToken)
                : await GetApplicationUpdateService().CheckAsync(settings, force, cancellationToken);
            if (result.CheckedAt is { } checkedAt)
            {
                settings = settings with { LastUpdateCheckAt = checkedAt };
                await QueueSettingsSave();
            }

            AvailableApplicationUpdate = result.Status == ApplicationUpdateCheckStatus.UpdateAvailable
                ? result.Manifest
                : null;
            lastApplicationUpdateStatus = result.Status;
            lastAvailableApplicationVersion = result.Manifest?.Version;
            RefreshApplicationUpdateText();
            return result;
        }
        finally
        {
            IsCheckingForApplicationUpdates = false;
        }
    }

    internal async Task SkipApplicationUpdateAsync()
    {
        if (AvailableApplicationUpdate is not { } update)
        {
            return;
        }
        settings = settings with { SkippedUpdateVersion = update.Version };
        AvailableApplicationUpdate = null;
        lastApplicationUpdateStatus = ApplicationUpdateCheckStatus.VersionSkipped;
        lastAvailableApplicationVersion = null;
        RefreshApplicationUpdateText();
        await QueueSettingsSave();
    }

    internal async Task<bool> StartAvailableApplicationUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        if (AvailableApplicationUpdate is not { } update)
        {
            return false;
        }

        bool portable = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"));
        UpdateAssetKind requiredKind = portable
            ? UpdateAssetKind.Portable
            : UpdateAssetKind.Installer;
        UpdateAsset asset = update.Assets.Single(candidate => candidate.Kind == requiredKind);
        IsBusy = true;
        try
        {
            ApplicationUpdateStatus = Loc["DownloadingApplicationUpdate"];
            string assetPath = await GetApplicationUpdateService().DownloadAssetAsync(
                asset,
                Path.Combine(paths.ApplicationDataRoot, "updates"),
                cancellationToken);
            string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
            string restart = Path.Combine(target, "Crystalfly.App.exe");
            string bundledUpdater = Path.Combine(target, "Crystalfly.Updater.exe");
            var request = new ApplicationUpdateLaunchRequest(
                Environment.ProcessId,
                portable ? ApplicationInstallationMode.Portable : ApplicationInstallationMode.Installed,
                assetPath,
                asset.Size,
                asset.Sha256,
                target,
                restart);
            using Process updater = ApplicationUpdateLauncher.LaunchFromTemporaryCopy(
                bundledUpdater,
                Path.Combine(Path.GetTempPath(), "Crystalfly"),
                request);
            ApplicationUpdateStatus = Loc["ApplicationUpdateStarting"];
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal Task FlushSettingsAsync() => QueueSettingsSave();

    internal void ReportApplicationUpdateFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        lastApplicationUpdateStatus = null;
        ApplicationUpdateStatus = Loc["UpdateCheckFailed"];
        ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
    }

    partial void OnIsAutomaticUpdateCheckEnabledChanged(bool value)
    {
        if (settings.CheckForUpdates == value)
        {
            return;
        }
        settings = settings with { CheckForUpdates = value };
        _ = QueueSettingsSave();
    }

    private ApplicationUpdateService GetApplicationUpdateService() =>
        applicationUpdateService ??= new ApplicationUpdateService(
            metadataHttpClient,
            packageHttpClient,
            networkPolicy,
            UpdateManifestUri,
            ParseApplicationVersion(),
            EmbeddedUpdateSigningKey.Load());

    private static Version ParseApplicationVersion()
    {
        Version assemblyVersion = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0, 0);
        return new Version(assemblyVersion.Major, assemblyVersion.Minor, Math.Max(0, assemblyVersion.Build));
    }
}
