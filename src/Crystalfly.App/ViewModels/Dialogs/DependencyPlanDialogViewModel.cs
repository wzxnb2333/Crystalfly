using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.ViewModels.DependencyGraph;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed record DependencyPlanNodeViewModel(
    string ModId,
    string PrimaryName,
    string SecondaryName,
    string Status,
    DependencyGraphNodeState State,
    string? Action = null,
    IReadOnlyList<string>? PrerequisiteIds = null);

public sealed partial class DependencyPlanDialogViewModel : ViewModelBase, IDialogContext
{
    public DependencyPlanDialogViewModel(
        string title,
        string message,
        IReadOnlyList<DependencyPlanNodeViewModel> nodes,
        string confirmText,
        string cancelText,
        bool canConfirm,
        bool isDangerous)
    {
        Title = title;
        Message = message;
        Nodes = nodes;
        Graph = CreateGraph(nodes);
        ConfirmText = confirmText;
        CancelText = cancelText;
        CanConfirm = canConfirm;
        IsDangerous = isDangerous;
    }

    public string Title { get; }

    public string Message { get; }

    public IReadOnlyList<DependencyPlanNodeViewModel> Nodes { get; }

    public DependencyGraphModel Graph { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public bool CanConfirm { get; }

    public bool IsDangerous { get; }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (CanConfirm)
        {
            RequestClose?.Invoke(this, true);
        }
    }

    private static DependencyGraphModel CreateGraph(IReadOnlyList<DependencyPlanNodeViewModel> nodes)
    {
        var knownIds = nodes.Select(node => node.ModId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var definitions = nodes.Select(node => new DependencyGraphNodeDefinition(
            node.ModId,
            node.PrimaryName,
            node.SecondaryName,
            node.Status,
            node.State,
            node.Action));
        var edges = nodes.SelectMany(node => (node.PrerequisiteIds ?? [])
            .Where(knownIds.Contains)
            .Select(prerequisiteId => new DependencyGraphEdgeDefinition(prerequisiteId, node.ModId)));
        return DependencyGraphModel.Create(definitions, edges);
    }
}
