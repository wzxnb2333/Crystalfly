using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Tests.Mods;

public sealed class GlobalModSettingsServiceTests
{
    [Fact]
    public void Resolve_file_uses_shared_LocalLow_only_while_the_game_is_running()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        var isolation = new LocalLowIsolationService(shared, storage);
        var mod = CreateOfficialMod("ExampleMod");

        var inactive = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: false));
        var active = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: true));

        Assert.Equal(
            Path.Combine(isolation.GetInstanceLocalLowPath("practice"), "ExampleMod.GlobalSettings.json"),
            inactive.ResolveFile("practice", mod).FilePath);
        Assert.Equal(
            Path.Combine(shared, "ExampleMod.GlobalSettings.json"),
            active.ResolveFile("practice", mod).FilePath);
    }

    [Fact]
    public async Task List_files_returns_existing_official_mod_settings_from_the_current_LocalLow()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        var isolation = new LocalLowIsolationService(shared, storage);
        var isolated = isolation.GetInstanceLocalLowPath("practice");
        await WriteAsync(isolated, "ExampleMod.GlobalSettings.json", "isolated");
        await WriteAsync(shared, "ExampleMod.GlobalSettings.json", "shared");
        var mod = CreateOfficialMod("ExampleMod");

        var inactive = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: false));
        var active = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: true));

        var inactiveFile = Assert.Single(inactive.ListFiles("practice", [mod]));
        var activeFile = Assert.Single(active.ListFiles("practice", [mod]));

        Assert.Equal("isolated", await File.ReadAllTextAsync(inactiveFile.FilePath));
        Assert.Equal("shared", await File.ReadAllTextAsync(activeFile.FilePath));
    }

    [Fact]
    public async Task Delete_creates_a_temporary_restore_point_then_removes_the_requested_settings_file()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        var isolation = new LocalLowIsolationService(shared, storage);
        var isolated = isolation.GetInstanceLocalLowPath("practice");
        await WriteAsync(isolated, "ExampleMod.GlobalSettings.json", "settings");
        await WriteAsync(isolated, "ExampleMod.GlobalSettings.json.bak", "backup");
        await WriteAsync(isolated, "OtherMod.GlobalSettings.json", "other-settings");
        var service = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: false));

        var deleted = await service.DeleteAsync("practice", [CreateOfficialMod("ExampleMod")]);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(Path.Combine(isolated, "ExampleMod.GlobalSettings.json")));
        Assert.False(File.Exists(Path.Combine(isolated, "ExampleMod.GlobalSettings.json.bak")));
        Assert.Equal("other-settings", await File.ReadAllTextAsync(
            Path.Combine(isolated, "OtherMod.GlobalSettings.json")));
        Assert.Empty(Directory.EnumerateDirectories(
            Path.Combine(storage, "transactions")));
    }

    [Fact]
    public async Task Delete_restores_earlier_files_when_a_later_delete_fails()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        var isolation = new LocalLowIsolationService(shared, storage);
        var isolated = isolation.GetInstanceLocalLowPath("practice");
        var alpha = Path.Combine(isolated, "Alpha.GlobalSettings.json");
        var beta = Path.Combine(isolated, "Beta.GlobalSettings.json");
        await WriteAsync(isolated, "Alpha.GlobalSettings.json", "alpha");
        await WriteAsync(isolated, "Beta.GlobalSettings.json", "beta");
        var service = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: false));

        await using (var lockStream = new FileStream(beta, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => service.DeleteAsync(
                "practice",
                [CreateOfficialMod("Alpha"), CreateOfficialMod("Beta")]));
        }

        Assert.Equal("alpha", await File.ReadAllTextAsync(alpha));
        Assert.Equal("beta", await File.ReadAllTextAsync(beta));
    }

    [Fact]
    public async Task Delete_honors_cancellation_without_removing_the_settings_file()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        var isolation = new LocalLowIsolationService(shared, storage);
        var isolated = isolation.GetInstanceLocalLowPath("practice");
        var path = Path.Combine(isolated, "ExampleMod.GlobalSettings.json");
        await WriteAsync(isolated, "ExampleMod.GlobalSettings.json", "settings");
        var service = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: false));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeleteAsync(
            "practice",
            [CreateOfficialMod("ExampleMod")],
            cancellation.Token));

        Assert.Equal("settings", await File.ReadAllTextAsync(path));
        Assert.False(Directory.Exists(Path.Combine(storage, "global-mod-settings", "staging")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("nested/name")]
    public void Resolve_file_rejects_a_mod_name_that_can_escape_ModSettings(string name)
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        var service = new GlobalModSettingsService(
            new LocalLowIsolationService(shared, storage),
            new StubProcessProbe(isRunning: false));

        Assert.Throws<InvalidDataException>(() => service.ResolveFile("practice", CreateOfficialMod(name)));
    }

    [Fact]
    public void Resolve_file_rejects_a_reparse_point_in_the_settings_path()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        var isolation = new LocalLowIsolationService(shared, storage);
        var isolated = isolation.GetInstanceLocalLowPath("practice");
        var external = test.CreateDirectory("external");
        Directory.CreateDirectory(isolated);
        Directory.CreateSymbolicLink(Path.Combine(isolated, "ExampleMod.GlobalSettings.json"), external);
        var service = new GlobalModSettingsService(
            isolation,
            new StubProcessProbe(isRunning: false));

        Assert.Throws<IOException>(() => service.ResolveFile("practice", CreateOfficialMod("ExampleMod")));
    }

    private static ModManifest CreateOfficialMod(string name) => new()
    {
        Id = $"hkmod:{name}",
        Name = name,
        SourceName = "HK ModLinks",
        Version = "1.0.0",
        DownloadUrl = "https://example.test/mod.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api-77"
    };

    private static async Task WriteAsync(string root, params string[] pathAndContent)
    {
        var content = pathAndContent[^1];
        var path = pathAndContent[..^1].Aggregate(root, Path.Combine);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private sealed class StubProcessProbe(bool isRunning) : IHollowKnightProcessProbe
    {
        public bool IsRunning() => isRunning;
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
