using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class ThunderstoreConfirmationDialogViewModel : ViewModelBase, IDialogContext, IDisposable
{
    public ThunderstoreConfirmationDialogViewModel(ThunderstoreViewModel model)
    {
        Model = model;
        model.PropertyChanged += ModelChanged;
    }

    public ThunderstoreViewModel Model { get; }
    public event EventHandler<object?>? RequestClose;
    public void Close() => Cancel();
    [RelayCommand]
    private void Cancel()
    {
        if (Model.IsLoading) return;
        Model.CancelPreviewCommand.Execute(null);
        Model.CancelUninstallCommand.Execute(null);
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Model.HasConfirmation) && !Model.HasConfirmation)
            RequestClose?.Invoke(this, null);
    }

    public void Dispose() => Model.PropertyChanged -= ModelChanged;
}
