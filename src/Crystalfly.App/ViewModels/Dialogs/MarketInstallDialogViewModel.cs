using System.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Mods;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class MarketInstallDialogViewModel : ViewModelBase, IDialogContext, IDisposable
{
    private readonly MainViewModel main;
    private readonly Func<MarketInstallTargetViewModel, CancellationToken, Task<ModInstallPlan>> createPlan;
    private readonly CancellationTokenSource lifetime = new();
    private string? dialogError;
    private bool isPlanLoading;
    private bool isSubmitting;
    private long planGeneration;

    public MarketInstallDialogViewModel(MainViewModel main, string modName)
        : this(main, modName, main.CreateSelectedMarketInstallPlanAsync)
    {
    }

    internal MarketInstallDialogViewModel(
        MainViewModel main,
        string modName,
        Func<MarketInstallTargetViewModel, CancellationToken, Task<ModInstallPlan>> createPlan)
    {
        this.main = main;
        this.createPlan = createPlan ?? throw new ArgumentNullException(nameof(createPlan));
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

    public ObservableCollection<MarketInstallPlanItemViewModel> PlanItems { get; } = [];

    public bool HasTargets => Targets.Count > 0;

    public bool IsBusy => main.IsBusy || IsPlanLoading || IsSubmitting;

    public bool IsPlanLoading
    {
        get => isPlanLoading;
        private set
        {
            if (SetProperty(ref isPlanLoading, value))
            {
                NotifyCommandState();
            }
        }
    }

    public bool IsSubmitting
    {
        get => isSubmitting;
        private set
        {
            if (SetProperty(ref isSubmitting, value))
            {
                NotifyCommandState();
            }
        }
    }

    public bool CanCancel => !IsBusy;

    public bool CanInstall => !IsBusy
        && main.SelectedMarketInstallTarget?.IsAvailable == true
        && PlanItems.Count > 0
        && PlanItems.All(item => !item.IsBlocked);

    public string? DialogError
    {
        get => dialogError;
        private set => SetProperty(ref dialogError, value);
    }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    public void Dispose()
    {
        main.PropertyChanged -= OnMainPropertyChanged;
        lifetime.Cancel();
        lifetime.Dispose();
    }

    internal void Select(MarketInstallTargetViewModel target)
    {
        main.SelectedMarketInstallTarget = target;
        _ = LoadPlanAsync();
    }

    public async Task LoadPlanAsync()
    {
        var target = main.SelectedMarketInstallTarget;
        var generation = Interlocked.Increment(ref planGeneration);
        PlanItems.Clear();
        DialogError = null;
        if (target is null || !target.IsAvailable)
        {
            NotifyCommandState();
            return;
        }

        IsPlanLoading = true;
        try
        {
            var plan = await createPlan(target, lifetime.Token);
            if (generation != Volatile.Read(ref planGeneration) || lifetime.IsCancellationRequested)
            {
                return;
            }
            foreach (var item in plan.Items)
            {
                PlanItems.Add(new MarketInstallPlanItemViewModel(main, item));
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or KeyNotFoundException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            if (generation == Volatile.Read(ref planGeneration))
            {
                DialogError = $"{main.Loc["OperationFailed"]}: {exception.Message}";
            }
        }
        finally
        {
            if (generation == Volatile.Read(ref planGeneration))
            {
                IsPlanLoading = false;
                NotifyCommandState();
            }
        }
    }

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
        IsSubmitting = true;
        try
        {
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
        finally
        {
            IsSubmitting = false;
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
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanInstall));
        CancelCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
    }
}

public sealed class MarketInstallPlanItemViewModel
{
    public MarketInstallPlanItemViewModel(MainViewModel main, ModInstallPlanItem item)
    {
        var marketItem = item.Kind == ModInstallPlanItemKind.Loader
            ? null
            : main.ProjectMarketMod(item.Id);
        PrimaryName = marketItem?.PrimaryName ?? item.Name;
        SecondaryName = marketItem?.SecondaryName ?? string.Empty;
        Version = item.Version;
        Reason = item.Reason;
        KindText = item.Kind switch
        {
            ModInstallPlanItemKind.Loader => main.Loc["Loader"],
            ModInstallPlanItemKind.Dependency => main.Loc["Dependency"],
            _ => main.Loc["MainMod"]
        };
        StateText = item.State switch
        {
            ModInstallPlanItemState.Satisfied => main.Loc["QueueStageSatisfied"],
            ModInstallPlanItemState.NeedsInstall => main.Loc["WillInstall"],
            ModInstallPlanItemState.NeedsUpdate => main.Loc["WillUpdate"],
            _ => main.Loc["QueueStateBlocked"]
        };
        IsSatisfied = item.State == ModInstallPlanItemState.Satisfied;
        IsBlocked = item.State == ModInstallPlanItemState.Blocked;
    }

    public string PrimaryName { get; }

    public string SecondaryName { get; }

    public bool HasSecondaryName => !string.IsNullOrWhiteSpace(SecondaryName);

    public string Version { get; }

    public string Reason { get; }

    public string KindText { get; }

    public string StateText { get; }

    public bool IsSatisfied { get; }

    public bool IsBlocked { get; }
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
