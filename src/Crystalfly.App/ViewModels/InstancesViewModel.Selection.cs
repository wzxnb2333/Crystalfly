using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Crystalfly.App.ViewModels;

public sealed partial class InstancesViewModel
{
    public ObservableCollection<InstanceItemViewModel> SelectedInstances { get; } = [];

    public InstanceItemViewModel? ListSelectedInstance
    {
        get => dependencies.GetSelectedInstance();
        set
        {
            if (!IsSelectingInstances && value is not null) dependencies.SetSelectedInstance(value);
        }
    }

    internal void RefreshListSelectedInstance()
    {
        if (!IsSelectingInstances) OnPropertyChanged(nameof(ListSelectedInstance));
    }

    [ObservableProperty]
    public partial bool IsSelectingInstances { get; set; }

    public SelectionMode InstanceListSelectionMode => IsSelectingInstances ? SelectionMode.Multiple | SelectionMode.Toggle : SelectionMode.Single;

    public int SelectedInstanceCount => SelectedInstances.Count;

    public bool HasSelectedInstances => SelectedInstanceCount > 0;

    public string InstanceSelectionText => string.Format(dependencies.Loc()["SelectedInstanceCount"], SelectedInstanceCount);

    partial void OnIsSelectingInstancesChanged(bool value)
    {
        ClearInstanceSelection();
        OnPropertyChanged(nameof(InstanceListSelectionMode));
        RefreshListSelectedInstance();
    }

    [RelayCommand]
    private void ToggleInstanceSelectionMode() => IsSelectingInstances = !IsSelectingInstances;

    [RelayCommand]
    private void SelectAllInstances()
    {
        if (!IsSelectingInstances) return;
        foreach (var instance in VisibleInstances)
            if (!SelectedInstances.Contains(instance)) SelectedInstances.Add(instance);
    }

    [RelayCommand]
    private void ClearInstanceSelection()
    {
        SelectedInstances.Clear();
    }

    private void NotifyInstanceSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedInstanceCount));
        OnPropertyChanged(nameof(HasSelectedInstances));
        OnPropertyChanged(nameof(InstanceSelectionText));
    }

    [RelayCommand]
    private async Task DeleteSelectedInstancesAsync(InstanceItemViewModel[]? confirmedInstances)
    {
        if (confirmedInstances is not { Length: > 0 } || dependencies.IsMutationBlocked()
            || SelectedGameDirectory?.IsSteam == true
            || confirmedInstances.DistinctBy(instance => instance.Id).Count() != confirmedInstances.Length
            || !IsSelectingInstances
            || confirmedInstances.Any(instance => !VisibleInstances.Contains(instance) || !SelectedInstances.Contains(instance)))
            return;

        var deleted = 0;
        dependencies.SetIsBusy(true);
        try
        {
            foreach (var instance in confirmedInstances)
            {
                await DeleteInstanceAsync(instance, batch: true);
                if (Instances.Contains(instance)) break;
                deleted++;
            }
        }
        finally
        {
            foreach (var instance in confirmedInstances)
            {
                if (VisibleInstances.Contains(instance) && !SelectedInstances.Contains(instance)) SelectedInstances.Add(instance);
            }
            dependencies.SetCurrentPage("Versions");
            dependencies.SetIsBusy(false);
        }
        if (deleted > 0)
        {
            var message = string.Format(dependencies.Loc()["DeletedInstanceCount"], deleted, confirmedInstances.Length);
            dependencies.SetStatusMessage(message);
            NotifyToast(message);
        }
    }
}
