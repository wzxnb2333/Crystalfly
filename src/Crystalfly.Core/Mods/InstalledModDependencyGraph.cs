using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public static class InstalledModDependencyGraph
{
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
}
