using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.ViewModels;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed record HistoricalManifestDownloadRequest(ulong ManifestId, string InstanceName);

public sealed partial class HistoricalManifestDialogViewModel : ViewModelBase, IDialogContext
{
    private readonly IReadOnlyDictionary<ulong, DownloadBuildOption> knownBuilds;
    private readonly string knownFormat;
    private readonly string unverifiedMessage;
    private readonly string invalidMessage;

    public HistoricalManifestDialogViewModel(
        IReadOnlyList<DownloadBuildOption> builds,
        string title,
        string message,
        string manifestPlaceholder,
        string instanceNamePlaceholder,
        string knownFormat,
        string unverifiedMessage,
        string invalidMessage,
        string confirmText,
        string cancelText)
    {
        knownBuilds = builds
            .Where(build => build.ManifestId is not null)
            .ToDictionary(build => build.ManifestId!.Value);
        Title = title;
        Message = message;
        ManifestPlaceholder = manifestPlaceholder;
        InstanceNamePlaceholder = instanceNamePlaceholder;
        this.knownFormat = knownFormat;
        this.unverifiedMessage = unverifiedMessage;
        this.invalidMessage = invalidMessage;
        ConfirmText = confirmText;
        CancelText = cancelText;
        UpdateValidation();
    }

    public string Title { get; }

    public string Message { get; }

    public string ManifestPlaceholder { get; }

    public string InstanceNamePlaceholder { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyPropertyChangedFor(nameof(IsKnownManifest))]
    private string manifestId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string instanceName = string.Empty;

    public bool CanConfirm => TryGetManifestId(out _) && !string.IsNullOrWhiteSpace(InstanceName);

    public bool IsKnownManifest => TryGetManifestId(out var manifestId) && knownBuilds.ContainsKey(manifestId);

    public string ValidationMessage => !TryGetManifestId(out var manifestId)
        ? invalidMessage
        : knownBuilds.TryGetValue(manifestId, out var build)
            ? string.Format(CultureInfo.CurrentCulture, knownFormat, build.DisplayName)
            : unverifiedMessage;

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    partial void OnManifestIdChanged(string value)
    {
        UpdateValidation();
    }

    partial void OnInstanceNameChanged(string value)
    {
        UpdateValidation();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (TryGetManifestId(out var parsed) && !string.IsNullOrWhiteSpace(InstanceName))
        {
            RequestClose?.Invoke(this, new HistoricalManifestDownloadRequest(parsed, InstanceName.Trim()));
        }
    }

    private bool TryGetManifestId(out ulong parsed) =>
        ulong.TryParse(ManifestId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out parsed)
        && parsed != 0;

    private void UpdateValidation()
    {
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(IsKnownManifest));
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}