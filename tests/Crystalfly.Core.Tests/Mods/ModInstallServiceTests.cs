using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModInstallServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Vanilla_instance_requires_the_manifest_loader()
    {
        var service = CreateService(Instance(), [Manifest("feature", "modding-api-77")]);

        var result = await service.EvaluateAsync("feature");

        Assert.Equal(ModInstallReadiness.RequiresLoader, result.Status);
        Assert.Equal("modding-api-77", result.RequiredLoaderId);
        Assert.Contains("modding-api-77", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("unknown", InstancePurpose.General)]
    [InlineData("1.5.78.11833", InstancePurpose.OfficialSpeedrun)]
    public async Task Unknown_build_and_official_speedrun_instances_are_blocked(
        string buildId,
        InstancePurpose purpose)
    {
        var service = CreateService(
            Instance(buildId, purpose),
            [Manifest("feature", "modding-api-77", buildId)]);

        var result = await service.EvaluateAsync("feature");

        Assert.Equal(ModInstallReadiness.Blocked, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task Entire_dependency_closure_must_match_the_build_and_loader()
    {
        await WriteManagedLoaderAsync("modding-api-77", LoaderState.ModdingApi);
        var wrongBuild = CreateService(
            Instance(),
            [
                Manifest("feature", "modding-api-77", dependencies: ["library"]),
                Manifest("library", "modding-api-77", "1.4.3.2")
            ]);
        var wrongLoader = CreateService(
            Instance(),
            [
                Manifest("feature", "modding-api-77", dependencies: ["library"]),
                Manifest("library", "bepinex-5.4.23.4")
            ]);

        Assert.Equal(ModInstallReadiness.Blocked, (await wrongBuild.EvaluateAsync("feature")).Status);
        Assert.Equal(ModInstallReadiness.Blocked, (await wrongLoader.EvaluateAsync("feature")).Status);
    }

    [Fact]
    public async Task Exact_managed_loader_is_ready()
    {
        await WriteManagedLoaderAsync("modding-api-77", LoaderState.ModdingApi);
        var service = CreateService(Instance(), [Manifest("feature", "modding-api-77")]);

        var result = await service.EvaluateAsync("feature");

        Assert.Equal(ModInstallReadiness.Ready, result.Status);
        Assert.Equal("modding-api-77", result.RequiredLoaderId);
    }

    [Fact]
    public async Task Wrong_conflicting_and_drifted_loaders_are_blocked()
    {
        await WriteManagedLoaderAsync("modding-api-77", LoaderState.ModdingApi);
        var wrong = CreateService(Instance(), [Manifest("feature", "bepinex-5.4.23.4")]);
        Assert.Equal(ModInstallReadiness.Blocked, (await wrong.EvaluateAsync("feature")).Status);

        Directory.CreateDirectory(Path.Combine(InstanceRoot, "BepInEx"));
        var conflict = CreateService(Instance(), [Manifest("feature", "modding-api-77")]);
        Assert.Equal(ModInstallReadiness.Blocked, (await conflict.EvaluateAsync("feature")).Status);

        File.Delete(LoaderReceiptPath);
        Directory.Delete(Path.Combine(InstanceRoot, "BepInEx"), recursive: true);
        var drifted = CreateService(Instance(), [Manifest("feature", "modding-api-77")]);
        Assert.Equal(ModInstallReadiness.Blocked, (await drifted.EvaluateAsync("feature")).Status);
    }

    [Fact]
    public async Task Matching_external_BepInEx_can_install_only_a_BepInEx_plugin()
    {
        var source = typeof(LoaderStateDetector).Assembly.Location;
        var core = Path.Combine(InstanceRoot, "BepInEx", "core", "BepInEx.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(core)!);
        File.Copy(source, core);
        var version = AssemblyName.GetAssemblyName(source).Version!.ToString();
        var package = CreateZip(("plugin.dll", "plugin"));
        var manifest = Manifest("feature", $"bepinex-{version}", packagePath: package);
        using var client = new HttpClient(new PackageHandler(await File.ReadAllBytesAsync(package)));
        var service = CreateService(Instance(), [manifest], client);

        var result = await service.EvaluateAsync("feature");
        var installed = await service.InstallAsync("feature");

        Assert.Equal(ModInstallReadiness.Ready, result.Status);
        Assert.Equal(LoaderOwnership.External, result.Loader.Ownership);
        Assert.Equal("feature", Assert.Single(installed).Id);
        Assert.True(File.Exists(Path.Combine(InstanceRoot, "BepInEx", "plugins", "Feature", "plugin.dll")));
        Assert.False(Directory.Exists(Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Mods")));
    }

    [Fact]
    public async Task Install_rechecks_loader_before_downloading_or_writing()
    {
        await WriteManagedLoaderAsync("modding-api-77", LoaderState.ModdingApi);
        var package = CreateZip(("mod.dll", "mod"));
        var handler = new PackageHandler(await File.ReadAllBytesAsync(package));
        using var client = new HttpClient(handler);
        var service = CreateService(
            Instance(),
            [Manifest("feature", "modding-api-77", packagePath: package)],
            client);
        Assert.Equal(ModInstallReadiness.Ready, (await service.EvaluateAsync("feature")).Status);
        Directory.CreateDirectory(Path.Combine(InstanceRoot, "BepInEx"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync("feature"));

        Assert.Equal(0, handler.RequestCount);
        Assert.False(Directory.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Feature")));
    }

    private string InstanceRoot => Path.Combine(root, "instance");
    private string LoaderReceiptPath => Path.Combine(root, "state", "loader.json");

    private ModInstallService CreateService(
        InstanceRecord instance,
        IReadOnlyList<ModManifest> catalog,
        HttpClient? httpClient = null)
    {
        Directory.CreateDirectory(InstanceRoot);
        return new ModInstallService(
            instance,
            catalog,
            new LoaderManager(
                InstanceRoot,
                Path.Combine(root, "transactions"),
                LoaderReceiptPath),
            new ModManager(
                InstanceRoot,
                Path.Combine(root, "transactions"),
                Path.Combine(root, "state", "mods"),
                Path.Combine(root, "packages"),
                httpClient));
    }

    private async Task WriteManagedLoaderAsync(string id, LoaderState state)
    {
        var relative = state == LoaderState.ModdingApi
            ? "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll"
            : "BepInEx/core/BepInEx.dll";
        var path = Path.Combine(InstanceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "loader");
        await AtomicJsonStore.WriteAsync(LoaderReceiptPath, new InstalledPackageReceipt
        {
            PackageId = id,
            LoaderState = state,
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = relative,
                    Sha256 = FileSha256(path)
                }
            ]
        });
    }

    private InstanceRecord Instance(
        string buildId = "1.5.78.11833",
        InstancePurpose purpose = InstancePurpose.General) => new()
        {
            Id = "instance",
            Name = "Instance",
            RootPath = InstanceRoot,
            BuildId = buildId,
            Purpose = purpose,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static ModManifest Manifest(
        string id,
        string loaderId,
        string buildId = "1.5.78.11833",
        IReadOnlyList<string>? dependencies = null,
        string? packagePath = null) => new()
        {
            Id = id,
            Name = id == "feature" ? "Feature" : "Library",
            Version = "1.0",
            DownloadUrl = "https://example.invalid/mod.zip",
            SizeBytes = packagePath is null ? null : new FileInfo(packagePath).Length,
            Sha256 = packagePath is null ? new string('A', 64) : FileSha256(packagePath),
            LoaderId = loaderId,
            SupportedBuildIds = [buildId],
            Dependencies = dependencies ?? []
        };

    private string CreateZip(params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write(item.Content);
        }
        return path;
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class PackageHandler(byte[] package) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(package)
            });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
