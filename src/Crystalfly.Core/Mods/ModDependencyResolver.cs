using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public static class ModDependencyResolver
{
    public static IReadOnlyList<ModManifest> ResolveInstallOrder(
        IEnumerable<ModManifest> mods,
        IEnumerable<string> requestedIds)
    {
        var byId = mods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ModManifest>();

        foreach (var id in requestedIds)
        {
            Visit(id);
        }
        return result;

        void Visit(string id)
        {
            if (!byId.TryGetValue(id, out var mod))
            {
                throw new KeyNotFoundException($"Mod dependency '{id}' was not found.");
            }
            if (states.TryGetValue(id, out var state))
            {
                if (state == VisitState.Visiting)
                {
                    throw new InvalidDataException($"Mod dependency cycle contains '{id}'.");
                }
                return;
            }

            states[id] = VisitState.Visiting;
            foreach (var dependency in mod.Dependencies)
            {
                Visit(dependency);
            }
            states[id] = VisitState.Visited;
            result.Add(mod);
        }
    }

    public static IReadOnlyList<ModManifest> FindDependents(
        IEnumerable<ModManifest> mods,
        string dependencyId)
    {
        var all = mods.ToArray();
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { dependencyId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var mod in all)
            {
                if (!dependencies.Contains(mod.Id) && mod.Dependencies.Any(dependencies.Contains))
                {
                    dependencies.Add(mod.Id);
                    changed = true;
                }
            }
        }
        dependencies.Remove(dependencyId);
        return all.Where(mod => dependencies.Contains(mod.Id)).ToArray();
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
