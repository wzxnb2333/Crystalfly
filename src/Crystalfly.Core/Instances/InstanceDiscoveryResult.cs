using System.Collections;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Instances;

public sealed record InstanceDiscoveryIssue(string Path, Exception Error);

public sealed record InstanceDiscoveryResult(
    IReadOnlyList<InstanceRecord> Instances,
    IReadOnlyList<InstanceDiscoveryIssue> Issues) : IReadOnlyList<InstanceRecord>
{
    public int Count => Instances.Count;

    public InstanceRecord this[int index] => Instances[index];

    public IEnumerator<InstanceRecord> GetEnumerator() => Instances.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
