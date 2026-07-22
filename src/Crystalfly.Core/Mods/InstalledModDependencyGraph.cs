using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public static class InstalledModDependencyGraph
{
    public static ModRemovalImpactPlan CreateRemovalPlan(
        IReadOnlyList<InstalledModReceipt> installed,
        IEnumerable<string> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(selectedIds);
        var byId = installed.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var targets = selectedIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => byId.TryGetValue(id, out var mod)
                ? mod.Id
                : throw new KeyNotFoundException($"Mod '{id}' is not installed."))
            .ToArray();
        var targetSet = targets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(targetSet, StringComparer.OrdinalIgnoreCase);
        var nodes = targets.Select(id => ToNode(byId[id], ModRemovalImpactKind.WillRemove, null, 0)).ToList();
        var pending = new Queue<(string ModId, int Depth)>(targets.Select(id => (id, 0)));

        while (pending.TryDequeue(out var current))
        {
            foreach (var dependent in installed.Where(mod =>
                         mod.Enabled
                         && !targetSet.Contains(mod.Id)
                         && mod.Dependencies.Contains(current.ModId, StringComparer.OrdinalIgnoreCase)))
            {
                if (!visited.Add(dependent.Id))
                {
                    continue;
                }
                var depth = current.Depth + 1;
                nodes.Add(ToNode(
                    dependent,
                    ModRemovalImpactKind.DependencyWillBeMissing,
                    current.ModId,
                    depth));
                pending.Enqueue((dependent.Id, depth));
            }
        }

        return new ModRemovalImpactPlan { TargetModIds = targets, Nodes = nodes };
    }

    public static IReadOnlyList<InstalledModReceipt> FindExternalDependents(
        IReadOnlyList<InstalledModReceipt> installed,
        IEnumerable<string> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(selectedIds);
        var selected = selectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var affected = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        bool changed;
        do
        {
            changed = false;
            foreach (var mod in installed)
            {
                if (!affected.Contains(mod.Id) && mod.Dependencies.Any(affected.Contains))
                {
                    changed |= affected.Add(mod.Id);
                }
            }
        }
        while (changed);

        return installed
            .Where(mod => affected.Contains(mod.Id) && !selected.Contains(mod.Id))
            .ToArray();
    }

    public static IReadOnlyList<InstalledModReceipt> OrderDependentsFirst(
        IReadOnlyList<InstalledModReceipt> installed,
        IEnumerable<string> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(selectedIds);
        var selected = selectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byId = installed
            .Where(mod => selected.Contains(mod.Id))
            .ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var result = new List<InstalledModReceipt>(byId.Count);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in byId.Values)
        {
            Visit(mod);
        }
        result.Reverse();
        return result;

        void Visit(InstalledModReceipt mod)
        {
            if (!visited.Add(mod.Id))
            {
                return;
            }
            foreach (var dependency in mod.Dependencies)
            {
                if (byId.TryGetValue(dependency, out var selectedDependency))
                {
                    Visit(selectedDependency);
                }
            }
            result.Add(mod);
        }
    }

    public static IReadOnlyList<InstalledModReceipt> FindUnusedDependencies(
        IReadOnlyList<InstalledModReceipt> removed,
        IReadOnlyList<InstalledModReceipt> remaining)
    {
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(remaining);
        var remainingById = remaining.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(removed.SelectMany(mod => mod.Dependencies));
        while (pending.TryDequeue(out var id))
        {
            if (!candidates.Add(id) || !remainingById.TryGetValue(id, out var dependency))
            {
                continue;
            }
            foreach (var transitiveDependency in dependency.Dependencies)
            {
                pending.Enqueue(transitiveDependency);
            }
        }

        var unused = candidates
            .Where(id => remainingById.TryGetValue(id, out var mod)
                && mod.Ownership == ModOwnership.Managed
                && !mod.IsLocal
                && !mod.Pinned)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool changed;
        do
        {
            changed = false;
            foreach (var id in unused.ToArray())
            {
                if (remaining.Any(mod =>
                    !unused.Contains(mod.Id)
                    && mod.Dependencies.Contains(id, StringComparer.OrdinalIgnoreCase)))
                {
                    unused.Remove(id);
                    changed = true;
                }
            }
        }
        while (changed);

        return remaining.Where(mod => unused.Contains(mod.Id))
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ModRemovalImpactNode ToNode(
        InstalledModReceipt mod,
        ModRemovalImpactKind kind,
        string? relatedToModId,
        int depth) => new()
        {
            ModId = mod.Id,
            ReceiptName = mod.Name,
            InstallRoot = mod.InstallRoot,
            Enabled = mod.Enabled,
            Kind = kind,
            RelatedToModId = relatedToModId,
            Depth = depth
        };
}
