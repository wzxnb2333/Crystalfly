using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class MarketInstallDialogViewModel : ViewModelBase, IDialogContext, IDisposable
{
    private readonly MainViewModel main;
    private string? dialogError;

    public MarketInstallDialogViewModel(MainViewModel main, string modName)
    {
        this.main = main;
        ModName = modName;
        Targets = main.MarketInstallTargets
            .Select(target => new MarketInstallTargetOptionViewModel(this, target))
            .ToArray();
        RefreshSelections();
        main.PropertyChanged += OnMainPropertyChanged;
    }

    public MainViewModel Main => main;

    public string ModName { get; }

    public IReadOnlyList<MarketInstallTargetOptionViewModel> Targets { get; }

    public bool HasTargets => Targets.Count > 0;

    public bool IsBusy => main.IsBusy;

    public bool CanCancel => !IsBusy;

    public bool CanInstall => !IsBusy && main.SelectedMarketInstallTarget?.IsAvailable == true;

    public string? DialogError
    {
        get => dialogError;
        private set => SetProperty(ref dialogError, value);
    }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    public void Dispose() => main.PropertyChanged -= OnMainPropertyChanged;

    internal void Select(MarketInstallTargetViewModel target)
        => main.SelectedMarketInstallTarget = target;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (CanCancel)
        {
            RequestClose?.Invoke(this, false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        DialogError = null;
        await main.InstallMarketModCommand.ExecuteAsync(null);
        if (main.ErrorMessage is null)
        {
            RequestClose?.Invoke(this, true);
        }
        else
        {
            DialogError = main.ErrorMessage;
        }
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainViewModel.IsBusy))
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanInstall));
            NotifyCommandState();
        }
        else if (eventArgs.PropertyName == nameof(MainViewModel.SelectedMarketInstallTarget))
        {
            RefreshSelections();
            OnPropertyChanged(nameof(CanInstall));
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    private void RefreshSelections()
    {
        foreach (var target in Targets)
        {
            target.SetSelected(ReferenceEquals(target.Target, main.SelectedMarketInstallTarget));
        }
    }

    private void NotifyCommandState()
    {
        CancelCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
    }
}

public sealed class MarketInstallTargetOptionViewModel : ViewModelBase
{
    private readonly MarketInstallDialogViewModel owner;
    private bool isSelected;
    private bool updatingSelection;

    public MarketInstallTargetOptionViewModel(
        MarketInstallDialogViewModel owner,
        MarketInstallTargetViewModel target)
    {
        this.owner = owner;
        Target = target;
    }

    public MarketInstallTargetViewModel Target { get; }

    public string DisplayName => Target.DisplayName;

    public string BuildDisplayName => Target.BuildDisplayName;

    public string LoaderDisplayName => Target.LoaderDisplayName;

    public string StatusText => Target.StatusText;

    public bool IsAvailable => Target.IsAvailable;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (!SetProperty(ref isSelected, value) || updatingSelection || !value)
            {
                return;
            }
            owner.Select(Target);
        }
    }

    internal void SetSelected(bool value)
    {
        updatingSelection = true;
        IsSelected = value;
        updatingSelection = false;
    }
}
