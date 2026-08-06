using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModHealthServiceTests : IDisposable
{
    private const string InstallRoot = "hollow_knight_Data/Managed/Mods/Test";
    private const string ExternalRoot = "hollow_knight_Data/Managed/Mods/External";
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"crystalfly-mod-health-{Guid.NewGuid():N}");

    [Fact]
    public async Task Hash_cache_reuses_result_for_unchanged_file_across_assessments()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var filePath = await WriteAsync(instanceRoot, $"{InstallRoot}/Test.dll", "original-content");
        var receipt = Receipt(await HashAsync(filePath));
        var service = new ModHealthService(instanceRoot);

        Assert.Equal(ModHealthStatus.Healthy, (await service.AssessAsync(receipt, [receipt])).Status);

        // Same size, same LastWriteTimeUtc, different content: the cached hash must be reused.
        var lastWrite = File.GetLastWriteTimeUtc(filePath);
        await File.WriteAllTextAsync(filePath, "tampered-content");
        File.SetLastWriteTimeUtc(filePath, lastWrite);

        Assert.Equal(ModHealthStatus.Healthy, (await service.AssessAsync(receipt, [receipt])).Status);
    }

    [Fact]
    public async Task Hash_cache_invalidates_when_file_size_changes()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var filePath = await WriteAsync(instanceRoot, $"{InstallRoot}/Test.dll", "original-content");
        var receipt = Receipt(await HashAsync(filePath));
        var service = new ModHealthService(instanceRoot);

        Assert.Equal(ModHealthStatus.Healthy, (await service.AssessAsync(receipt, [receipt])).Status);

        await File.WriteAllTextAsync(filePath, "tampered");

        Assert.Equal(ModHealthStatus.ModifiedFile, (await service.AssessAsync(receipt, [receipt])).Status);
    }

    [Fact]
    public async Task Hash_cache_invalidates_when_last_write_time_changes()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var filePath = await WriteAsync(instanceRoot, $"{InstallRoot}/Test.dll", "original-content");
        var receipt = Receipt(await HashAsync(filePath));
        var service = new ModHealthService(instanceRoot);

        Assert.Equal(ModHealthStatus.Healthy, (await service.AssessAsync(receipt, [receipt])).Status);

        await File.WriteAllTextAsync(filePath, "tampered-content");
        File.SetLastWriteTimeUtc(filePath, File.GetLastWriteTimeUtc(filePath).AddMinutes(1));

        Assert.Equal(ModHealthStatus.ModifiedFile, (await service.AssessAsync(receipt, [receipt])).Status);
    }

    [Fact]
    public async Task Hash_cache_invalidates_when_file_is_deleted()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var filePath = await WriteAsync(instanceRoot, $"{InstallRoot}/Test.dll", "original-content");
        var receipt = Receipt(await HashAsync(filePath));
        var service = new ModHealthService(instanceRoot);

        Assert.Equal(ModHealthStatus.Healthy, (await service.AssessAsync(receipt, [receipt])).Status);

        File.Delete(filePath);

        Assert.Equal(
            ModHealthStatus.CriticalFileMissing,
            (await service.AssessAsync(receipt, [receipt])).Status);

        // A restored file must be rehashed and reported healthy again.
        await File.WriteAllTextAsync(filePath, "original-content");
        Assert.Equal(ModHealthStatus.Healthy, (await service.AssessAsync(receipt, [receipt])).Status);
    }

    [Fact]
    public async Task Hash_cache_persists_across_service_instances_for_the_same_instance_root()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var filePath = await WriteAsync(instanceRoot, $"{InstallRoot}/Test.dll", "original-content");
        var receipt = Receipt(await HashAsync(filePath));

        Assert.Equal(
            ModHealthStatus.Healthy,
            (await new ModHealthService(instanceRoot).AssessAsync(receipt, [receipt])).Status);

        // Same size, same LastWriteTimeUtc, different content.
        var lastWrite = File.GetLastWriteTimeUtc(filePath);
        await File.WriteAllTextAsync(filePath, "tampered-content");
        File.SetLastWriteTimeUtc(filePath, lastWrite);

        // A brand-new service instance must still see the cached hash.
        var report = await new ModHealthService(instanceRoot).AssessAsync(receipt, [receipt]);
        Assert.Equal(ModHealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Hash_cache_is_keyed_by_full_path_and_does_not_leak_between_instances()
    {
        var instanceA = Path.Combine(root, "instance-a");
        var instanceB = Path.Combine(root, "instance-b");
        var relativePath = $"{InstallRoot}/Test.dll";
        var aPath = await WriteAsync(instanceA, relativePath, "aaaaaaaa");
        var bPath = await WriteAsync(instanceB, relativePath, "bbbbbbbb");
        var aReceipt = Receipt(await HashAsync(aPath));
        var bReceipt = Receipt(await HashAsync(bPath));

        var aReport = await new ModHealthService(instanceA).AssessAsync(aReceipt, [aReceipt]);
        // Identical size and LastWriteTimeUtc for both files: only the full path distinguishes them.
        File.SetLastWriteTimeUtc(bPath, File.GetLastWriteTimeUtc(aPath));
        var bReport = await new ModHealthService(instanceB).AssessAsync(bReceipt, [bReceipt]);

        Assert.Equal(ModHealthStatus.Healthy, aReport.Status);
        Assert.Equal(ModHealthStatus.Healthy, bReport.Status);
        Assert.Equal(await HashAsync(aPath), aReport.CurrentFileSha256ByPath[relativePath]);
        Assert.Equal(await HashAsync(bPath), bReport.CurrentFileSha256ByPath[relativePath]);
    }

    [Fact]
    public async Task Assess_hashes_all_files_in_parallel_and_reports_missing_modified_and_extra()
    {
        var instanceRoot = Path.Combine(root, "instance-parallel");
        var receipt = new InstalledModReceipt
        {
            Id = "parallel",
            Name = "Parallel",
            Version = "1",
            LoaderId = "modding-api-77",
            InstallRoot = InstallRoot,
            EntryFiles = [],
            Files = []
        };
        var healthyPaths = new List<(string Relative, string Full)>(capacity: 23);
        for (int i = 0; i < 23; i++)
        {
            var relative = $"{InstallRoot}/file{i:00}.dll";
            var full = await WriteAsync(instanceRoot, relative, $"content-{i}");
            healthyPaths.Add((relative, full));
        }
        var missingRelative = $"{InstallRoot}/missing.dll";
        var modifiedRelative = $"{InstallRoot}/modified.dll";
        var modifiedFull = await WriteAsync(instanceRoot, modifiedRelative, "current");
        var extraRelative = $"{InstallRoot}/extra.dll";
        var extraFull = await WriteAsync(instanceRoot, extraRelative, "extra");
        var files = healthyPaths
            .Select(pair => new InstalledFileReceipt
            {
                RelativePath = pair.Relative,
                Sha256 = HashData(File.ReadAllBytes(pair.Full))
            })
            .Append(new InstalledFileReceipt
            {
                RelativePath = missingRelative,
                Sha256 = new string('A', 64)
            })
            .Append(new InstalledFileReceipt
            {
                RelativePath = modifiedRelative,
                Sha256 = new string('B', 64)
            })
            .ToArray();
        receipt = receipt with { Files = files };

        var report = await new ModHealthService(instanceRoot).AssessAsync(receipt, [receipt]);

        Assert.Equal(ModHealthStatus.CriticalFileMissing, report.Status);
        Assert.Equal([missingRelative], report.MissingFiles);
        Assert.Equal([modifiedRelative], report.ModifiedFiles);
        Assert.Equal([extraRelative], report.ExtraFiles);
        Assert.Equal(25, report.CurrentFileSha256ByPath.Count);
        foreach (var (relative, full) in healthyPaths)
        {
            Assert.Equal(HashData(await File.ReadAllBytesAsync(full)), report.CurrentFileSha256ByPath[relative]);
        }
        Assert.Equal(
            HashData(await File.ReadAllBytesAsync(modifiedFull)),
            report.CurrentFileSha256ByPath[modifiedRelative]);
        Assert.Equal(
            HashData(await File.ReadAllBytesAsync(extraFull)),
            report.CurrentFileSha256ByPath[extraRelative]);
    }

    [Fact]
    public async Task AssessExternalAsync_hashes_files_and_detects_disappearance()
    {
        var instanceRoot = Path.Combine(root, "instance-external");
        var firstRelative = $"{ExternalRoot}/First.dll";
        var secondRelative = $"{ExternalRoot}/Second.dll";
        var thirdRelative = $"{ExternalRoot}/Third.dll";
        await WriteAsync(instanceRoot, firstRelative, "first");
        var secondFull = await WriteAsync(instanceRoot, secondRelative, "second");
        await WriteAsync(instanceRoot, thirdRelative, "third");
        var external = External(firstRelative, secondRelative, thirdRelative);
        var service = new ModHealthService(instanceRoot);

        var report = await service.AssessExternalAsync(external);

        Assert.Equal(ModHealthStatus.UnmanagedExternal, report.Status);
        Assert.Equal(3, report.CurrentFileSha256ByPath.Count);
        Assert.Equal(await HashAsync(secondFull), report.CurrentFileSha256ByPath[secondRelative]);

        File.Delete(secondFull);
        var disappeared = await service.AssessExternalAsync(external);
        Assert.Equal(ModHealthStatus.Indeterminate, disappeared.Status);
        Assert.Contains(secondRelative, disappeared.Detail);
    }

    [Fact]
    public async Task AssessExternalAsync_reuses_cached_hash_for_unchanged_file()
    {
        var instanceRoot = Path.Combine(root, "instance-external");
        var relative = $"{ExternalRoot}/External.dll";
        var full = await WriteAsync(instanceRoot, relative, "original-content");
        var external = External(relative);
        var service = new ModHealthService(instanceRoot);

        var first = await service.AssessExternalAsync(external);
        Assert.Equal(await HashAsync(full), first.CurrentFileSha256ByPath[relative]);

        // Same size, same LastWriteTimeUtc, different content: the cached hash must be reused.
        var lastWrite = File.GetLastWriteTimeUtc(full);
        await File.WriteAllTextAsync(full, "tampered-content");
        File.SetLastWriteTimeUtc(full, lastWrite);

        var second = await service.AssessExternalAsync(external);
        Assert.Equal(first.CurrentFileSha256ByPath[relative], second.CurrentFileSha256ByPath[relative]);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ModDiscoveryEntry External(params string[] files) => new()
    {
        Id = "external",
        Name = "External",
        LoaderId = "modding-api-77",
        InstallRoot = ExternalRoot,
        Enabled = true,
        Ownership = ModOwnership.External,
        Files = files,
        EntryFiles = [files[0]]
    };

    private static InstalledModReceipt Receipt(string sha256) => new()
    {
        Id = "test",
        Name = "Test",
        Version = "1.0",
        LoaderId = "modding-api-77",
        InstallRoot = InstallRoot,
        Ownership = ModOwnership.Managed,
        Files = [new InstalledFileReceipt { RelativePath = $"{InstallRoot}/Test.dll", Sha256 = sha256 }],
        EntryFiles = [$"{InstallRoot}/Test.dll"]
    };

    private static async Task<string> WriteAsync(string instanceRoot, string relativePath, string content)
    {
        var path = Path.Combine(instanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    private static string HashData(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}
