using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Serialization;

public sealed class AtomicJsonStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"crystalfly-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Overwrite_creates_backup_containing_previous_value()
    {
        var path = Path.Combine(directory, "instance.json");
        var original = CreateInstance("original");
        var updated = CreateInstance("updated");

        await AtomicJsonStore.WriteAsync(path, original);
        await AtomicJsonStore.WriteAsync(path, updated);

        Assert.Equal(updated, await AtomicJsonStore.ReadAsync<InstanceRecord>(path));
        Assert.Equal(original, await AtomicJsonStore.ReadAsync<InstanceRecord>(path + ".bak"));
    }

    [Fact]
    public async Task Read_uses_backup_when_main_file_is_corrupt()
    {
        var path = Path.Combine(directory, "instance.json");
        var recoverable = CreateInstance("recoverable");

        await AtomicJsonStore.WriteAsync(path, recoverable);
        await AtomicJsonStore.WriteAsync(path, CreateInstance("newer"));
        await File.WriteAllTextAsync(path, "{not-json");

        Assert.Equal(recoverable, await AtomicJsonStore.ReadAsync<InstanceRecord>(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static InstanceRecord CreateInstance(string name) => new()
    {
        Id = name,
        Name = name,
        RootPath = $@"D:\Games\{name}",
        BuildId = "1.2.2.1",
        CreatedAt = DateTimeOffset.Parse("2026-07-16T12:00:00Z")
    };
}
