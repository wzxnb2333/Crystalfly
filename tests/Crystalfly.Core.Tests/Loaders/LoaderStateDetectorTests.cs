using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Loaders;

public sealed class LoaderStateDetectorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-loader-{Guid.NewGuid():N}");

    [Fact]
    public async Task Detect_distinguishes_vanilla_verified_loaders_and_conflicts()
    {
        Directory.CreateDirectory(root);
        Assert.Equal(LoaderState.Vanilla, await LoaderStateDetector.DetectAsync(root, null));

        var core = CreateFile("BepInEx/core/BepInEx.dll", "loader");
        var receipt = Receipt("bepinex-5.4.23.4", LoaderState.BepInEx, core, "loader");
        Assert.Equal(LoaderState.BepInEx, await LoaderStateDetector.DetectAsync(root, receipt));

        Directory.CreateDirectory(Path.Combine(root, "hollow_knight_Data", "Managed", "Mods"));
        Assert.Equal(LoaderState.Conflict, await LoaderStateDetector.DetectAsync(root, receipt));
    }

    [Fact]
    public async Task Detect_marks_unmanaged_or_changed_loader_files_as_drifted()
    {
        var hook = CreateFile("hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll", "hook");
        Assert.Equal(LoaderState.Drifted, await LoaderStateDetector.DetectAsync(root, null));

        var receipt = Receipt("modding-api-78", LoaderState.ModdingApi, hook, "hook");
        await File.WriteAllTextAsync(Path.Combine(root, hook), "changed");
        Assert.Equal(LoaderState.Drifted, await LoaderStateDetector.DetectAsync(root, receipt));
    }

    [Fact]
    public async Task Inspect_identifies_external_BepInEx_from_its_core_assembly_version()
    {
        var source = typeof(LoaderStateDetector).Assembly.Location;
        var core = Path.Combine(root, "BepInEx", "core", "BepInEx.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(core)!);
        File.Copy(source, core);
        var version = AssemblyName.GetAssemblyName(source).Version!.ToString();

        var inspection = await LoaderStateDetector.InspectAsync(root, null);

        Assert.Equal(LoaderState.BepInEx, inspection.State);
        Assert.Equal($"bepinex-{version}", inspection.PackageId);
        Assert.Equal(version, inspection.Version);
        Assert.False(inspection.IsVerified);
        Assert.Equal(LoaderOwnership.External, inspection.Ownership);
    }

    [Fact]
    public async Task Inspect_blocks_external_BepInEx_when_the_core_version_is_unreadable()
    {
        CreateFile("BepInEx/core/BepInEx.dll", "not a managed assembly");

        var inspection = await LoaderStateDetector.InspectAsync(root, null);

        Assert.Equal(LoaderState.Drifted, inspection.State);
        Assert.Null(inspection.PackageId);
        Assert.Null(inspection.Version);
        Assert.Equal(LoaderOwnership.External, inspection.Ownership);
    }

    [Fact]
    public async Task Inspect_keeps_external_ModdingApi_drifted()
    {
        CreateFile("hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll", "hook");

        var inspection = await LoaderStateDetector.InspectAsync(root, null);

        Assert.Equal(LoaderState.Drifted, inspection.State);
        Assert.Null(inspection.PackageId);
        Assert.Equal(LoaderOwnership.External, inspection.Ownership);
    }

    [Fact]
    public async Task Inspect_reports_managed_loader_identity_and_verification()
    {
        var hook = CreateFile("hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll", "hook");
        var receipt = Receipt("modding-api-78", LoaderState.ModdingApi, hook, "hook");

        var inspection = await LoaderStateDetector.InspectAsync(root, receipt);

        Assert.Equal(LoaderState.ModdingApi, inspection.State);
        Assert.Equal("modding-api-78", inspection.PackageId);
        Assert.Equal("78", inspection.Version);
        Assert.True(inspection.IsVerified);
        Assert.Equal(LoaderOwnership.Managed, inspection.Ownership);
    }

    private string CreateFile(string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return relativePath;
    }

    private static InstalledPackageReceipt Receipt(
        string id,
        LoaderState state,
        string path,
        string content) => new()
        {
            PackageId = id,
            LoaderState = state,
            Files =
        [
            new InstalledFileReceipt
            {
                RelativePath = path,
                Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            }
        ]
        };

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
