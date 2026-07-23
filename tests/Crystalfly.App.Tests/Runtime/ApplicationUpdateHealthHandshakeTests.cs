using Crystalfly.App.Runtime;

namespace Crystalfly.App.Tests.Runtime;

public sealed class ApplicationUpdateHealthHandshakeTests : IDisposable
{
    private readonly string testRoot = Path.Combine(Path.GetTempPath(), "Crystalfly.HealthHandshake.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Signal_writes_health_file_inside_sibling_recovery_directory()
    {
        string target = Path.Combine(testRoot, "Crystalfly");
        string health = Path.Combine(testRoot, ".crystalfly-update-recovery", "operation", "healthy");
        Directory.CreateDirectory(target);

        ApplicationUpdateHealthHandshake.Signal(health, target);

        Assert.True(File.Exists(health));
    }

    [Fact]
    public void Signal_ignores_health_path_outside_sibling_recovery_directory()
    {
        string target = Path.Combine(testRoot, "Crystalfly");
        string outside = Path.Combine(testRoot, "outside", "healthy");
        Directory.CreateDirectory(target);

        ApplicationUpdateHealthHandshake.Signal(outside, target);

        Assert.False(File.Exists(outside));
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
