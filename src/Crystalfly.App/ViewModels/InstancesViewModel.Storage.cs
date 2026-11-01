using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Instances;

namespace Crystalfly.App.ViewModels;

public sealed partial class InstancesViewModel
{
    private CancellationTokenSource? storageCancellation;
    [ObservableProperty]
    public partial string StorageUsageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StorageSavingsText { get; set; } = string.Empty;

    [RelayCommand]
    internal async Task RefreshStorageUsageAsync()
    {
        storageCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(dependencies.LifetimeCancellation);
        storageCancellation = cancellation;
        StorageUsageText = dependencies.Loc()["StorageCalculating"];
        StorageSavingsText = string.Empty;
        try
        {
            await Task.Delay(150, cancellation.Token);
            var records = Instances.Select(item => item.Record).ToArray();
            var versionRoot = dependencies.GetVersionRoot();
            var game = dependencies.GetSettings().SelectedGameId;
            var auxiliary = records.Select(record => Path.Combine(dependencies.GetVersionDataRoot(versionRoot), "instances", record.Id))
                .Concat(records.Select(record => Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(record.RootPath, record.Id))!))
                .Append(Path.Combine(versionRoot, ".crystalfly", "shared-content-v1", game.ToString())).ToArray();
            var usage = await Task.Run(() => InstanceStorageUsage.Measure(records.Select(record => record.RootPath), auxiliary,
                cancellation.Token), cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            StorageUsageText = string.Format(dependencies.Loc()[usage.UnreadableEntries > 0 ? "StoragePartial" : "StorageActual"], FormatBytes(usage.AllocatedBytes));
            StorageSavingsText = usage.UnreadableEntries > 0 ? dependencies.Loc()["StoragePartialHint"]
                : string.Format(dependencies.Loc()["StorageSavings"], FormatBytes(usage.SharedSavingsBytes));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (!cancellation.IsCancellationRequested) StorageUsageText = dependencies.Loc()["StorageUnavailable"];
        }
        finally
        {
            if (ReferenceEquals(storageCancellation, cancellation)) storageCancellation = null;
        }
    }

    private static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / (1024d * 1024 * 1024):0.00} GiB" : $"{bytes / (1024d * 1024):0.00} MiB";

    internal static IReadOnlyList<InstanceItemViewModel> BuildHierarchy(
        IEnumerable<InstanceItemViewModel> instances, string search, LocalizationViewModel loc)
    {
        var result = new List<InstanceItemViewModel>();
        foreach (var group in instances.GroupBy(item => (item.Record.GameId,
                     Build: item.IsKnownBuild ? item.Record.BuildId : item.DisplayVersion))
                     .OrderByDescending(group => group.Any(item => item.IsFavorite))
                     .ThenBy(group => group.Key.Build, StringComparer.CurrentCultureIgnoreCase))
        {
            var items = group.OrderByDescending(item => item.IsFavorite)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            var byId = items.ToDictionary(item => item.Id);
            var included = items.Where(item => string.IsNullOrWhiteSpace(search)
                || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.DisplayVersion.Contains(search, StringComparison.OrdinalIgnoreCase)).Select(item => item.Id).ToHashSet();
            foreach (var id in included.ToArray())
            {
                var seen = new HashSet<string> { id };
                var item = byId[id];
                while (item.Record.ParentInstanceId is { } parent && byId.TryGetValue(parent, out item) && seen.Add(parent))
                    included.Add(parent);
            }
            var visited = new HashSet<string>();
            var start = result.Count;
            foreach (var item in items.Where(item => item.Record.ParentInstanceId is not { } parent || !byId.ContainsKey(parent)))
                AddBranch(item, 0);
            // Invalid/cyclic legacy metadata must never hide an instance.
            foreach (var item in items) AddBranch(item, 0);
            var groupRows = result.Skip(start).ToArray();
            if (result.Count > start)
            {
                result[start].GroupHeading = items[0].DisplayVersion;
                result[start].GroupCount = groupRows.Length;
                SetTreeGeometry(result.Skip(start).ToArray());
            }

            void AddBranch(InstanceItemViewModel item, int depth)
            {
                if (!included.Contains(item.Id) || !visited.Add(item.Id)) return;
                item.GroupHeading = string.Empty;
                item.HierarchyDepth = depth;
                item.HierarchyMargin = new(0, 4, 0, 4);
                item.IsChildInstance = depth > 0;
                item.HasChildren = items.Any(child => child.Record.ParentInstanceId == item.Id && child.Id != item.Id);
                var mode = loc[item.Record.UsesSharedContent ? "InstanceSharedMode" : "InstanceIndependentMode"];
                var parentName = item.Record.ParentInstanceId is { } parent && byId.TryGetValue(parent, out var source)
                    ? source.Name : item.Record.ParentInstanceName;
                item.StorageRelationship = parentName is null ? mode : $"{mode} · {string.Format(loc["InstanceSource"], parentName)}";
                item.StorageSummary = item.HasChildren ? loc["InstanceOrigin"] : mode;
                result.Add(item);
                foreach (var child in items.Where(child => child.Record.ParentInstanceId == item.Id)) AddBranch(child, depth + 1);
            }
        }
        return result;
    }

    private static void SetTreeGeometry(InstanceItemViewModel[] rows)
    {
        // Parent exits and child entries must share the same capped lane, leaving room for the 8px bend.
        static int LaneX(int level) => 12 + Math.Min(level, 3) * 6;

        var continuations = new HashSet<int>();
        for (var index = 0; index < rows.Length; index++)
        {
            var item = rows[index];
            var depth = item.HierarchyDepth;
            continuations.RemoveWhere(level => level >= depth);
            var path = new System.Text.StringBuilder();
            foreach (var level in continuations.Where(level => level < depth - 1))
            {
                var x = LaneX(level);
                path.Append($"M{x},0 V64 ");
            }
            if (depth > 0)
            {
                var next = index + 1;
                while (next < rows.Length && rows[next].HierarchyDepth > depth) next++;
                var hasNextSibling = next < rows.Length && rows[next].HierarchyDepth == depth;
                var x = LaneX(depth - 1);
                path.Append($"M{x},0 V24 Q{x},32 {x + 8},32 H40 ");
                if (hasNextSibling)
                {
                    path.Append($"M{x},24 V64 ");
                    continuations.Add(depth - 1);
                }
                else continuations.Remove(depth - 1);
            }
            if (index + 1 < rows.Length && rows[index + 1].HierarchyDepth > depth)
            {
                var x = LaneX(depth);
                path.Append($"M40,32 H{x + 8} Q{x},32 {x},40 V64 ");
            }
            item.TreeGeometry = path.Length == 0 ? null : path.ToString();
        }
    }
}
