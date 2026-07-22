using Crystalfly.Core.Mods;

namespace Crystalfly.App.ViewModels;

public sealed record PresetApplyStepItemViewModel(
    PresetApplyStep Step,
    string Action,
    string State)
{
    public string ModId => Step.ModId;

    public string Version => Step.Version ?? string.Empty;

    public string LoaderId => Step.LoaderId ?? string.Empty;

    public bool IsBlocked => Step.State == PresetApplyStepState.Blocked;

    public bool IsUnresolved => Step.State == PresetApplyStepState.Unresolved;
}
