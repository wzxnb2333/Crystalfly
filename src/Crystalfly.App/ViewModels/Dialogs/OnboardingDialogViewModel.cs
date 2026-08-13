using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class OnboardingDialogViewModel : ViewModelBase, IDialogContext
{
    private sealed record OnboardingStep(string TitleKey, string DescriptionKey);

    private static readonly OnboardingStep[] Steps =
    [
        new("OnboardingStepWelcomeTitle", "OnboardingStepWelcomeDescription"),
        new("OnboardingStepImportTitle", "OnboardingStepImportDescription"),
        new("OnboardingStepSelectInstanceTitle", "OnboardingStepSelectInstanceDescription"),
        new("OnboardingStepLoaderTitle", "OnboardingStepLoaderDescription"),
        new("OnboardingStepModsTitle", "OnboardingStepModsDescription"),
        new("OnboardingStepLaunchTitle", "OnboardingStepLaunchDescription"),
        new("OnboardingStepExtraTitle", "OnboardingStepExtraDescription"),
        new("OnboardingStepFinishTitle", "OnboardingStepFinishDescription")
    ];

    private readonly Func<string, string> translate;
    private int currentIndex;

    public OnboardingDialogViewModel(Func<string, string> translate)
    {
        this.translate = translate;
    }

    public int StepCount => Steps.Length;

    public string CurrentTitle => translate(Steps[currentIndex].TitleKey);

    public string CurrentDescription => translate(Steps[currentIndex].DescriptionKey);

    public string StepPosition => string.Format(translate("OnboardingStepPosition"), currentIndex + 1, Steps.Length);

    public string NextText => translate(IsLastStep ? "OnboardingFinish" : "OnboardingNext");

    public string BackText => translate("OnboardingBack");

    public string SkipText => translate("OnboardingSkip");

    public bool CanGoBack => currentIndex > 0;

    public bool CanGoNext => currentIndex < Steps.Length - 1;

    public bool IsLastStep => currentIndex == Steps.Length - 1;

    public event EventHandler<object?>? RequestClose;

    public void Close() => Skip();

    private void AdvanceTo(int index)
    {
        currentIndex = Math.Clamp(index, 0, Steps.Length - 1);
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentDescription));
        OnPropertyChanged(nameof(StepPosition));
        OnPropertyChanged(nameof(NextText));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(IsLastStep));
    }

    [RelayCommand]
    private void Back()
    {
        if (CanGoBack)
        {
            AdvanceTo(currentIndex - 1);
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (CanGoNext)
        {
            AdvanceTo(currentIndex + 1);
            return;
        }
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Skip() => RequestClose?.Invoke(this, false);
}
