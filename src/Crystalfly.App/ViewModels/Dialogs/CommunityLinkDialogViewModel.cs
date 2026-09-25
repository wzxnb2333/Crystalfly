using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Configuration;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class CommunityLinkDialogViewModel : ViewModelBase, IDialogContext
{
    private readonly SpeedrunCommunityLinkDefinition original;
    private readonly Func<SpeedrunCommunityLinkDefinition, Task<bool>> saveAsync;

    public CommunityLinkDialogViewModel(LocalizationViewModel loc,
        Func<SpeedrunCommunityLinkDefinition, Task<bool>> saveAsync,
        SpeedrunCommunityLinkDefinition? existing = null)
    {
        Loc = loc;
        this.saveAsync = saveAsync;
        Title = loc[existing is null ? "SpeedrunCommunityAdd" : "SpeedrunCommunityEdit"];
        original = existing ?? new SpeedrunCommunityLinkDefinition
        {
            Id = $"custom-{Guid.NewGuid():N}", Name = string.Empty, Url = "https://", Group = SpeedrunCommunityGroup.Other
        };
        name = original.Name;
        url = original.Url;
    }

    public LocalizationViewModel Loc { get; }
    public string Title { get; }
    [ObservableProperty] private string name;
    [ObservableProperty] private string url;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(CancelCommand))]
    private bool isSaving;
    public bool CanEdit => !IsSaving;
    public event EventHandler<object?>? RequestClose;
    public void Close() => Cancel();

    partial void OnNameChanged(string value) => ErrorMessage = null;
    partial void OnUrlChanged(string value) => ErrorMessage = null;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Cancel()
    {
        if (CanEdit) RequestClose?.Invoke(this, false);
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 120)
        {
            ErrorMessage = Loc["SpeedrunCommunityNameRequired"];
            return;
        }
        if (Url.Length > 2048 || !Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length != 0)
        {
            ErrorMessage = Loc["SpeedrunCommunityInvalidUrl"];
            return;
        }
        IsSaving = true;
        try
        {
            if (await saveAsync(original with { Name = Name.Trim(), Url = uri.AbsoluteUri }))
            {
                RequestClose?.Invoke(this, true);
            }
            else
            {
                ErrorMessage = Loc["SpeedrunCommunityDuplicate"];
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
