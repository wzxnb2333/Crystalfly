using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Diagnostics;
using Crystalfly.Core.Diagnostics;

namespace Crystalfly.App.ViewModels;

public sealed partial class MainViewModel
{
    private readonly IErrorReporter errorReporter;
    private readonly ErrorReportingService? diagnostics;
    private bool diagnosticSettingsLoaded;

    [ObservableProperty]
    public partial bool HasErrorReportingNotice { get; set; }

    public bool ErrorReportingEnabled
    {
        get => settings.ErrorReportingEnabled;
        set
        {
            if (value == settings.ErrorReportingEnabled) return;
            settings = settings with { ErrorReportingEnabled = value };
            diagnostics?.Configure(value, IsOfflineMode, !value || diagnosticSettingsLoaded);
            OnPropertyChanged();
            _ = QueueSettingsSave();
        }
    }

    public string ErrorReportingDestination => diagnostics?.IsConfigured == true
        ? string.Format(Loc["ErrorReportingDestinationFormat"], diagnostics.Destination,
            ErrorReportingRuntime.Region == "US-NewYork" ? Loc["ErrorReportingRegionUs"] : ErrorReportingRuntime.Region,
            ErrorReportingRuntime.GetMetadata("ErrorReportingRetention") == "90 days"
                ? Loc["ErrorReportingRetention90"] : ErrorReportingRuntime.GetMetadata("ErrorReportingRetention"))
        : Loc["ErrorReportingNotConfigured"];

    private void InitializeDiagnostics()
    {
        diagnosticSettingsLoaded = true;
        diagnostics?.Configure(settings.ErrorReportingEnabled, settings.OfflineMode);
        HasErrorReportingNotice = diagnostics?.IsConfigured == true && settings.ErrorReportingNoticeVersion < 1;
        OnPropertyChanged(nameof(ErrorReportingEnabled));
        OnPropertyChanged(nameof(ErrorReportingDestination));
    }

    [RelayCommand]
    private void DismissErrorReportingNotice()
    {
        HasErrorReportingNotice = false;
        settings = settings with { ErrorReportingNoticeVersion = 1 };
        _ = QueueSettingsSave();
    }

    [RelayCommand]
    private void DisableErrorReporting()
    {
        ErrorReportingEnabled = false;
        DismissErrorReportingNotice();
    }

    public void ReportError(Exception exception, ErrorOperation operation) => errorReporter.Capture(exception, operation);
    public byte[] ExportErrorReports() => diagnostics?.Export() ?? "[]"u8.ToArray();
}
