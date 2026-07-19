using Crystalfly.Core.Logs;

namespace Crystalfly.Core.Tests.Logs;

public sealed class ApplicationLogTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-app-log-{Guid.NewGuid():N}");

    [Fact]
    public void Write_appends_timestamped_catalog_event()
    {
        var path = Path.Combine(directory, "crystalfly.log");

        ApplicationLog.Write(path, "mod-translation-catalog", "source=Remote count=649");

        var text = File.ReadAllText(path);
        Assert.Contains("mod-translation-catalog", text);
        Assert.Contains("source=Remote count=649", text);
    }

    [Fact]
    public void Write_rotates_a_log_that_exceeds_the_size_limit()
    {
        var path = Path.Combine(directory, "crystalfly.log");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, new string('x', ApplicationLog.MaxFileBytes + 1));

        ApplicationLog.Write(path, "mod-translation-catalog", "rotated");

        Assert.True(File.Exists(path + ".1"));
        Assert.Contains("rotated", File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
