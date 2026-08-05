using CommunityToolkit.Mvvm.ComponentModel;

namespace Crystalfly.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public event Action<string>? ToastRequested;

    protected void NotifyToast(string message) => ToastRequested?.Invoke(message);
}
