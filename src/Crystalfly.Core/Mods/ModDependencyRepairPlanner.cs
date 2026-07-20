using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public static class ModDependencyRepairPlanner
{
    public static ModDependencyRepairPlan CreatePlan(
        IReadOnlyList<InstalledModReceipt> installed,
        IReadOnlyList<ModManifest> catalog,
        string buildId,
        string loaderId)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(loaderId);

        var installedById = installed.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var catalogById = catalog.GroupBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);
        var repairableById = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        var cycleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var drafts = new Dictionary<string, RepairDraft>(StringComparer.OrdinalIgnoreCase);
        var requiredBy = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var orderedIds = new List<string>();

        foreach (var root in installed.Where(mod => mod.Enabled))
        {
            foreach (var dependency in root.Dependencies)
            {
                Visit(dependency, root.Id);
            }
        }

        var items = orderedIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id =>
            {
                var draft = drafts[id];
                return new ModDependencyRepairPlanItem
                {
                    ModId = id,
                    PackageId = draft.PackageId,
                    Name = draft.Name,
                    Version = draft.Version,
                    LoaderId = draft.LoaderId,
                    Action = draft.Action,
                    RequiredByModIds = requiredBy.GetValueOrDefault(id)?.Order(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
                    Reason = draft.Reason
                };
            })
            .ToArray();
        return new ModDependencyRepairPlan { BuildId = buildId, LoaderId = loaderId, Items = items };

        bool Visit(string id, string requiredById)
        {
            if (!requiredBy.TryGetValue(id, out var parents))
            {
                parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                requiredBy[id] = parents;
            }
            parents.Add(requiredById);

            if (states.TryGetValue(id, out var state))
            {
                if (state == VisitState.Visiting)
                {
                    var cycleStart = stack.FindIndex(candidate =>
                        string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase));
                    foreach (var cycleId in stack.Skip(cycleStart < 0 ? 0 : cycleStart))
                    {
                        cycleIds.Add(cycleId);
                    }
                    return false;
                }
                return repairableById[id];
            }

            states[id] = VisitState.Visiting;
            stack.Add(id);
            installedById.TryGetValue(id, out var receipt);
            catalogById.TryGetValue(id, out var candidates);
            var manifest = candidates?.FirstOrDefault(candidate =>
                string.Equals(candidate.LoaderId, loaderId, StringComparison.OrdinalIgnoreCase)
                && candidate.SupportedBuildIds.Contains(buildId, StringComparer.OrdinalIgnoreCase));
            var dependencies = receipt?.Dependencies ?? manifest?.Dependencies ?? [];
            var dependenciesRepairable = true;
            foreach (var dependency in dependencies)
            {
                dependenciesRepairable &= Visit(dependency, id);
            }

            stack.RemoveAt(stack.Count - 1);
            states[id] = VisitState.Visited;

            RepairDraft? draft = null;
            if (receipt is not null && receipt.Enabled)
            {
                if (!string.Equals(receipt.LoaderId, loaderId, StringComparison.OrdinalIgnoreCase))
                {
                    draft = Unresolved(receipt.Name, receipt.Version, receipt.LoaderId,
                        $"Installed dependency '{id}' uses loader '{receipt.LoaderId}'.");
                }
            }
            else if (receipt is not null)
            {
                draft = string.Equals(receipt.LoaderId, loaderId, StringComparison.OrdinalIgnoreCase)
                    && dependenciesRepairable
                    && !cycleIds.Contains(id)
                    ? new RepairDraft(
                        id, receipt.Name, receipt.Version, receipt.LoaderId,
                        ModDependencyRepairAction.ReEnable,
                        $"Installed dependency '{id}' will be re-enabled.")
                    : Unresolved(receipt.Name, receipt.Version, receipt.LoaderId,
                        $"Installed dependency '{id}' cannot be re-enabled automatically.");
            }
            else if (manifest is not null && dependenciesRepairable && !cycleIds.Contains(id))
            {
                draft = new RepairDraft(
                    manifest.Id, manifest.Name, manifest.Version, manifest.LoaderId,
                    ModDependencyRepairAction.DownloadAndInstall,
                    $"Dependency '{id}' will be downloaded and installed.");
            }
            else
            {
                var fallback = candidates?.FirstOrDefault();
                draft = Unresolved(
                    fallback?.Name ?? id,
                    fallback?.Version ?? "unknown",
                    fallback?.LoaderId ?? loaderId,
                    cycleIds.Contains(id)
                        ? $"Dependency cycle contains '{id}'."
                        : $"No compatible catalog package was found for dependency '{id}'.");
            }

            if (draft is not null)
            {
                drafts[id] = draft;
                orderedIds.Add(id);
            }
            var repairable = draft?.Action != ModDependencyRepairAction.Unresolved && dependenciesRepairable;
            repairableById[id] = repairable;
            return repairable;

            RepairDraft Unresolved(string name, string version, string itemLoaderId, string reason) =>
                new(id, name, version, itemLoaderId, ModDependencyRepairAction.Unresolved, reason);
        }
    }

    private sealed record RepairDraft(
        string PackageId,
        string Name,
        string Version,
        string LoaderId,
        ModDependencyRepairAction Action,
        string Reason);

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
