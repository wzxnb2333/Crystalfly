using Crystalfly.Core.Logs;

namespace Crystalfly.Core.Tests.Logs;

public sealed class InstanceLogServiceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "crystalfly-log-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Discover_returns_existing_known_logs_without_duplicates()
    {
        var instance = Path.Combine(root, "instance");
        var localLow = Path.Combine(root, "local-low");
        Write(instance, "BepInEx", "LogOutput.log", "bepinex");
        Write(instance, "hollow_knight_Data", "Managed", "Mods", "ModLog.txt", "modding-api");
        Write(localLow, "Player.log", "player");

        var logs = InstanceLogService.Discover(instance, localLow);

        Assert.Equal(3, logs.Count);
        Assert.Contains(logs, log => log.Name == "BepInEx" && log.Path.EndsWith("LogOutput.log"));
        Assert.Contains(logs, log => log.Name == "Modding API" && log.Path.EndsWith("ModLog.txt"));
        Assert.Contains(logs, log => log.Name == "Player.log" && log.Path.EndsWith("Player.log"));
        Assert.Equal(logs.Count, logs.Select(log => log.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task ReadTailAsync_limits_bytes_and_drops_partial_first_line()
    {
        var path = Write(
            root,
            "large.log",
            string.Join('\n', Enumerable.Range(0, 40).Select(index => $"line-{index:00}")));

        var text = await InstanceLogService.ReadTailAsync(path, maxBytes: 55);

        Assert.DoesNotContain("line-00", text);
        Assert.False(text.StartsWith("ine-", StringComparison.Ordinal));
        Assert.EndsWith("line-39", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadTailAsync_returns_empty_for_missing_file()
    {
        var text = await InstanceLogService.ReadTailAsync(Path.Combine(root, "missing.log"));

        Assert.Empty(text);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Write(string basePath, params string[] segmentsAndContent)
    {
        var content = segmentsAndContent[^1];
        var path = Path.Combine([basePath, .. segmentsAndContent[..^1]]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
