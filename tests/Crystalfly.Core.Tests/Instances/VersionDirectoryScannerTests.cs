using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class VersionDirectoryScannerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-scan-{Guid.NewGuid():N}");

    [Fact]
    public void Scan_returns_only_direct_children_and_ignores_metadata_directory()
    {
        var first = Directory.CreateDirectory(Path.Combine(root, "1.2.2.1")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(root, "1.5.78.11833")).FullName;
        Directory.CreateDirectory(Path.Combine(first, "nested-copy"));
        Directory.CreateDirectory(Path.Combine(root, ".crystalfly"));
        File.WriteAllText(Path.Combine(root, "readme.txt"), "not a version");

        var result = VersionDirectoryScanner.Scan(root);

        Assert.Equal([first, second], result);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
