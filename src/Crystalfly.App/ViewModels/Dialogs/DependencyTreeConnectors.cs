namespace Crystalfly.App.ViewModels.Dialogs;

public enum TreeConnectorKind
{
    None,
    Continue,
    Branch,
    LastBranch
}

internal static class DependencyTreeConnectors
{
    private const string RootParentKey = "\u0000<root>";

    public static void Assign(IReadOnlyList<DependencyPlanNodeViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        var childrenByParent = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var parentKey = node.ParentModId ?? RootParentKey;
            if (!childrenByParent.TryGetValue(parentKey, out var siblings))
            {
                siblings = [];
                childrenByParent[parentKey] = siblings;
            }

            siblings.Add(node.ModId);
        }

        var isLastChild = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var siblings in childrenByParent.Values)
        {
            for (var index = 0; index < siblings.Count; index++)
            {
                isLastChild[siblings[index]] = index == siblings.Count - 1;
            }
        }

        var nodesById = new Dictionary<string, DependencyPlanNodeViewModel>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            nodesById[node.ModId] = node;
        }
        foreach (var node in nodes)
        {
            var connectors = new TreeConnectorKind[node.Depth];
            if (node.Depth > 0)
            {
                connectors[^1] = isLastChild.GetValueOrDefault(node.ModId)
                    ? TreeConnectorKind.LastBranch
                    : TreeConnectorKind.Branch;

                var ancestorId = node.ParentModId;
                for (var column = node.Depth - 2;
                     column >= 0
                        && ancestorId is not null
                        && nodesById.TryGetValue(ancestorId, out var ancestor);
                     column--)
                {
                    connectors[column] = isLastChild.GetValueOrDefault(ancestorId)
                        ? TreeConnectorKind.None
                        : TreeConnectorKind.Continue;
                    ancestorId = ancestor.ParentModId;
                }
            }

            node.Connectors = connectors;
        }
    }
}
