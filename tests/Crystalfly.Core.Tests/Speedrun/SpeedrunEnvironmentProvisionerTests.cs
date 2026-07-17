using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class SpeedrunEnvironmentProvisionerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Provision_replaces_declared_screen_shake_targets_from_verified_raw_asset()
    {
        var target = CreateInstanceFile("hollow_knight_Data/Managed/Assembly-CSharp.dll", "vanilla");
        var package = CreateFile("packages/screen-shake.dll", "screen-shake");
        var asset = Asset("screen-shake-modifier-1221", "1.2.2.1", package);
        var request = Request(
            Template("single-run-1221", "1.2.2.1", asset.Id),
            asset,
            [Rule("hollow_knight_Data/Managed/Assembly-CSharp.dll", asset)],
            new Dictionary<string, string> { [asset.Id] = package });

        var journal = await new SpeedrunEnvironmentProvisioner().ProvisionAsync(request);

        Assert.Equal(TransactionState.Committed, journal.State);
        Assert.Equal("screen-shake", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Provision_selects_requested_load_normaliser_entry()
    {
        var target = CreateInstanceFile("hollow_knight_Data/Managed/Assembly-CSharp.dll", "vanilla");
        var package = CreateZip(
            ("1578 LoadNormaliser/Assembly-CSharp loadNormaliser1s UI.dll", "one"),
            ("1578 LoadNormaliser/Assembly-CSharp loadNormaliser2s UI.dll", "two"),
            ("1578 LoadNormaliser/Assembly-CSharp loadNormaliser3s UI.dll", "three"),
            ("1578 LoadNormaliser/Assembly-CSharp loadNormaliser5s UI.dll", "five"));
        var asset = Asset("load-normaliser-1.1", "1.5.78.11833", package);
        var template = Template("race-1578", "1.5.78.11833", asset.Id) with
        {
            LoadNormaliserAvailable = true,
            RequiresLoadNormaliserSelection = true,
            AllowedLoadNormaliserSeconds = [1, 2, 3, 5]
        };
        var request = Request(
            template,
            asset,
            [Rule("hollow_knight_Data/Managed/Assembly-CSharp.dll", asset)],
            new Dictionary<string, string> { [asset.Id] = package }) with
        {
            LoadNormaliserSeconds = 3
        };

        await new SpeedrunEnvironmentProvisioner().ProvisionAsync(request);

        Assert.Equal("three", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Provision_rejects_invalid_load_normaliser_selection_without_changing_instance()
    {
        var target = CreateInstanceFile("hollow_knight_Data/Managed/Assembly-CSharp.dll", "vanilla");
        var package = CreateZip(("1578 LoadNormaliser/Assembly-CSharp loadNormaliser3s UI.dll", "three"));
        var asset = Asset("load-normaliser-1.1", "1.5.78.11833", package);
        var template = Template("race-1578", "1.5.78.11833", asset.Id) with
        {
            LoadNormaliserAvailable = true,
            RequiresLoadNormaliserSelection = true,
            AllowedLoadNormaliserSeconds = [1, 2, 3, 5]
        };
        var request = Request(
            template,
            asset,
            [Rule("hollow_knight_Data/Managed/Assembly-CSharp.dll", asset)],
            new Dictionary<string, string> { [asset.Id] = package }) with
        {
            LoadNormaliserSeconds = 4
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SpeedrunEnvironmentProvisioner().ProvisionAsync(request));

        Assert.Equal("vanilla", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Provision_rolls_back_all_declared_targets_when_later_write_fails()
    {
        var first = CreateInstanceFile("a.dll", "old-a");
        var lockedPath = CreateInstanceFile("z.dll", "old-z");
        var package = CreateFile("packages/screen-shake.dll", "new");
        var asset = Asset("screen-shake-modifier-1221", "1.2.2.1", package);
        var request = Request(
            Template("single-run-1221", "1.2.2.1", asset.Id),
            asset,
            [Rule("a.dll", asset), Rule("z.dll", asset)],
            new Dictionary<string, string> { [asset.Id] = package });

        await using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() =>
                new SpeedrunEnvironmentProvisioner().ProvisionAsync(request));
        }

        Assert.Equal("old-a", await File.ReadAllTextAsync(first));
        Assert.Equal("old-z", await File.ReadAllTextAsync(lockedPath));
    }

    [Fact]
    public async Task Provision_surfaces_manual_recovery_requirement_before_instance_cleanup()
    {
        _ = CreateInstanceFile("a.dll", "old-a");
        var lockedPath = CreateInstanceFile("z.dll", "old-z");
        var package = CreateFile("packages/screen-shake.dll", "new");
        var asset = Asset("screen-shake-modifier-1221", "1.2.2.1", package);
        var request = Request(
            Template("single-run-1221", "1.2.2.1", asset.Id),
            asset,
            [Rule("a.dll", asset), Rule("z.dll", asset)],
            new Dictionary<string, string> { [asset.Id] = package });
        var restorePoint = Directory.CreateDirectory(Path.Combine(
            request.TransactionRoot,
            "existing-needs-attention")).FullName;
        var journalPath = Path.Combine(restorePoint, "journal.json");
        await AtomicJsonStore.WriteAsync(journalPath, new TransactionJournal
        {
            Id = "existing-needs-attention",
            Operation = "provision-speedrun-environment",
            State = TransactionState.NeedsAttention,
            CreatedAt = DateTimeOffset.UtcNow,
            RootPath = Path.GetFullPath(request.InstanceRoot),
            RestorePointPath = restorePoint,
            Error = "manual recovery required"
        });

        await using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<SpeedrunRecoveryRequiredException>(() =>
                new SpeedrunEnvironmentProvisioner().ProvisionAsync(request));
        }

        Assert.True(Directory.Exists(request.InstanceRoot));
        Assert.True(File.Exists(journalPath));
    }

    private SpeedrunProvisioningRequest Request(
        SpeedrunTemplate template,
        SpeedrunAsset asset,
        IReadOnlyList<SpeedrunFileRule> rules,
        IReadOnlyDictionary<string, string> localPackages) => new()
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
                        BuildId = template.BuildId,
                        RulesRevision = template.RulesRevision,
                        Files = rules
                    }
                ]
            },
            TemplateId = template.Id,
            InstanceRoot = Path.Combine(root, "instance"),
            TransactionRoot = Path.Combine(root, "transactions"),
            PackageCacheRoot = Path.Combine(root, "cache"),
            LocalPackagePaths = localPackages
        };

    private static SpeedrunTemplate Template(string id, string buildId, string assetId) => new()
    {
        Id = id,
        Name = id,
        BuildId = buildId,
        IsOfficial = false,
        RulesRevision = "rules-test",
        FileManifestId = $"files-{id}",
        RequiredAssetIds = [assetId]
    };

    private static SpeedrunAsset Asset(string id, string buildId, string package) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0",
        DownloadUrl = "https://example.invalid/package",
        SizeBytes = new FileInfo(package).Length,
        Sha256 = FileSha256(package),
        SupportedBuildIds = [buildId]
    };

    private static SpeedrunFileRule Rule(string target, SpeedrunAsset asset) => new()
    {
        RelativePath = target,
        Sha256 = new string('0', 64),
        Kind = SpeedrunFileKind.Tool,
        AssetId = asset.Id,
        AssetVersion = asset.Version
    };

    private string CreateInstanceFile(string relativePath, string content)
    {
        var path = Path.Combine(root, "instance", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateFile(string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

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

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
