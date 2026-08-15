using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public enum OnboardingTaskState
{
    Done,
    Current,
    Attention,
    Locked
}

public sealed record OnboardingTaskItemViewModel(
    string Id,
    string Title,
    string Description,
    string StatusText,
    string ActionText,
    string ActionKey,
    OnboardingTaskState State)
{
    public bool IsDone => State == OnboardingTaskState.Done;

    public bool IsCurrent => State == OnboardingTaskState.Current;

    public bool IsAttention => State == OnboardingTaskState.Attention;

    public bool IsLocked => State == OnboardingTaskState.Locked;

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionKey);
}

public sealed partial class OnboardingDialogViewModel : ViewModelBase, IDialogContext
{
    private static readonly IReadOnlyList<OnboardingTaskItemViewModel> EmptyTasks = [];

    private readonly Func<string, string> translate;
    private readonly Func<string, Task>? runAction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentTitle))]
    [NotifyPropertyChangedFor(nameof(CurrentDescription))]
    [NotifyPropertyChangedFor(nameof(CurrentStatusText))]
    [NotifyPropertyChangedFor(nameof(CurrentActionText))]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    public partial OnboardingTaskItemViewModel? SelectedTask { get; set; }

    public OnboardingDialogViewModel(
        Func<string, string> translate,
        IReadOnlyList<OnboardingTaskItemViewModel>? tasks = null,
        Func<string, Task>? runAction = null)
    {
        this.translate = translate;
        this.runAction = runAction;
        Tasks = new ObservableCollection<OnboardingTaskItemViewModel>(
            tasks is { Count: > 0 } ? tasks : EmptyTasks);
        SelectedTask = Tasks.FirstOrDefault(task => !task.IsDone) ?? Tasks.FirstOrDefault();
    }

    public ObservableCollection<OnboardingTaskItemViewModel> Tasks { get; }

    public string CurrentTitle => SelectedTask?.Title ?? translate("OnboardingSection");

    public string CurrentDescription => SelectedTask?.Description ?? translate("OnboardingSectionHint");

    public string CurrentStatusText => SelectedTask?.StatusText ?? string.Empty;

    public string CurrentActionText => SelectedTask?.ActionText ?? translate("OnboardingReopen");

    public bool HasAction => SelectedTask?.HasAction == true;

    public string CompleteText => translate("OnboardingFinish");

    public string CloseText => translate("WindowClose");

    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, false);

    [RelayCommand]
    private void Dismiss() => Close();

    [RelayCommand]
    private async Task RunSelectedActionAsync()
    {
        if (SelectedTask?.ActionKey is not { Length: > 0 } actionKey || runAction is null)
        {
            return;
        }
        await runAction(actionKey);
        RequestClose?.Invoke(this, false);
    }

    [RelayCommand]
    private void Complete() => RequestClose?.Invoke(this, true);
}
