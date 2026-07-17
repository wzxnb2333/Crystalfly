using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Tests.Configuration;

public sealed class CrystalflyPathsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-paths-{Guid.NewGuid():N}");

    [Fact]
    public void Resolve_uses_portable_data_only_when_flag_exists()
    {
        var executableDirectory = Directory.CreateDirectory(Path.Combine(root, "app")).FullName;
        var localAppData = Directory.CreateDirectory(Path.Combine(root, "local")).FullName;

        var installed = CrystalflyPaths.Resolve(executableDirectory, localAppData);
        File.WriteAllText(Path.Combine(executableDirectory, "portable.flag"), string.Empty);
        var portable = CrystalflyPaths.Resolve(executableDirectory, localAppData);

        Assert.Equal(Path.Combine(localAppData, "Crystalfly"), installed.ApplicationDataRoot);
        Assert.False(installed.IsPortable);
        Assert.Equal(Path.Combine(executableDirectory, "Data"), portable.ApplicationDataRoot);
        Assert.True(portable.IsPortable);
        Assert.Equal(
            Path.Combine(root, "versions", ".crystalfly"),
            portable.GetVersionDataRoot(Path.Combine(root, "versions")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
