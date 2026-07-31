using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Models;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class SpeedrunEnvironmentProvisionerTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Provision_verifies_the_zip_and_inner_dll_before_replacing_the_game_assembly()
    {
        string target = CreateInstanceFile("vanilla");
        string package = CreateZip(("Assembly-CSharp.dll", "patched"));
        SpeedrunProvisioningRequest request = Request(package, ContentSha256("patched"));

        TransactionJournal journal = await new SpeedrunEnvironmentProvisioner().ProvisionAsync(request);

        Assert.Equal(TransactionState.Committed, journal.State);
        Assert.Equal("patched", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Provision_rejects_an_inner_dll_hash_mismatch_without_changing_the_instance()
    {
        string target = CreateInstanceFile("vanilla");
        string package = CreateZip(("Assembly-CSharp.dll", "tampered"));
        SpeedrunProvisioningRequest request = Request(package, ContentSha256("expected"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SpeedrunEnvironmentProvisioner().ProvisionAsync(request));

        Assert.Equal("vanilla", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Provision_rejects_a_package_without_exactly_one_root_assembly()
    {
        string target = CreateInstanceFile("vanilla");
        string package = CreateZip(
            ("Assembly-CSharp.dll", "patched"),
            ("nested/Assembly-CSharp.dll", "patched"));
        SpeedrunProvisioningRequest request = Request(package, ContentSha256("patched"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SpeedrunEnvironmentProvisioner().ProvisionAsync(request));

        Assert.Equal("vanilla", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Provision_rejects_a_zip_hash_mismatch_without_opening_the_package()
    {
        string target = CreateInstanceFile("vanilla");
        string package = CreateZip(("Assembly-CSharp.dll", "patched"));
        SpeedrunProvisioningRequest request = Request(package, ContentSha256("patched"));
        SpeedrunAsset asset = Assert.Single(request.Catalog.SpeedrunAssets);
        request = request with
        {
            Catalog = request.Catalog with
            {
                SpeedrunAssets = [asset with { Sha256 = new string('A', 64) }]
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SpeedrunEnvironmentProvisioner().ProvisionAsync(request));

        Assert.Equal("vanilla", await File.ReadAllTextAsync(target));
    }

    private SpeedrunProvisioningRequest Request(string package, string dllSha256)
    {
        const string buildId = "1.4.3.2";
        const string templateId = "runtime-patches-1432";
        const string assetId = "runtime-patches-1432-v1.0.2";
        var asset = new SpeedrunAsset
        {
            Id = assetId,
            Name = "AssemblyPatches",
            Version = "1.0.2",
            DownloadUrl = "https://example.invalid/Assembly-CSharp-1432-windows.zip",
            SizeBytes = new FileInfo(package).Length,
            Sha256 = FileSha256(package),
            SupportedBuildIds = [buildId]
        };
        var template = new SpeedrunTemplate
        {
            Id = templateId,
            Name = "RuntimePatches 1.4.3.2",
            BuildId = buildId,
            IsOfficial = true,
            RulesRevision = RuntimePatchesPolicy.RulesRevision,
            FileManifestId = $"files-{templateId}",
            RequiredAssetIds = [assetId]
        };
        return new SpeedrunProvisioningRequest
        {
            Catalog = new GameCatalog
            {
                SpeedrunTemplates = [template],
                SpeedrunAssets = [asset],
                SpeedrunFileManifests =
                [
                    new SpeedrunFileManifest
                    {
                        Id = template.FileManifestId,
                        BuildId = buildId,
                        RulesRevision = template.RulesRevision,
                        Files =
                        [
                            new SpeedrunFileRule
                            {
                                RelativePath = "hollow_knight_Data/Managed/Assembly-CSharp.dll",
                                Sha256 = dllSha256,
                                Kind = SpeedrunFileKind.Tool,
                                AssetId = assetId,
                                AssetVersion = asset.Version
                            }
                        ]
                    }
                ]
            },
            TemplateId = templateId,
            InstanceRoot = Path.Combine(root, "instance"),
            TransactionRoot = Path.Combine(root, "transactions"),
            PackageCacheRoot = Path.Combine(root, "cache"),
            LocalPackagePaths = new Dictionary<string, string> { [assetId] = package }
        };
    }

    private string CreateInstanceFile(string content)
    {
        string path = Path.Combine(root, "instance", "hollow_knight_Data", "Managed", "Assembly-CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateZip(params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using Stream stream = entry.Open();
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes);
        }
        return path;
    }

    private static string ContentSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static string FileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
