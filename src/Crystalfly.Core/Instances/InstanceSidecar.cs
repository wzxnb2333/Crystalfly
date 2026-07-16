using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Instances;

public static class InstanceSidecar
{
    public static Task SaveAsync(
        InstanceRecord record,
        CancellationToken cancellationToken = default) =>
        AtomicJsonStore.WriteAsync(GetPath(record.RootPath), record, cancellationToken);

    public static Task<InstanceRecord> LoadAsync(
        string instanceRoot,
        CancellationToken cancellationToken = default) =>
        AtomicJsonStore.ReadAsync<InstanceRecord>(GetPath(instanceRoot), cancellationToken);

    public static string GetPath(string instanceRoot) =>
        Path.Combine(instanceRoot, ".crystalfly", "instance.json");
}
