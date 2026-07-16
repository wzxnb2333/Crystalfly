using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Packages;

namespace Crystalfly.Core.Tests.Packages;

public sealed class PackageInstallerTests
{
    [Fact]
    public async Task InstallFromFile_verifies_and_replaces_package_files()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "new"), ("docs/readme.txt", "docs"));
        var target = test.CreateDirectory("target");
        await File.WriteAllTextAsync(Path.Combine(target, "mod.dll"), "old");

        var result = await PackageInstaller.InstallFromFileAsync(
            package, target, test.CreateDirectory("transactions"), new FileInfo(package).Length, FileSha256(package));

        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(target, "mod.dll")));
        Assert.Equal("docs", await File.ReadAllTextAsync(Path.Combine(target, "docs", "readme.txt")));
        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public async Task InstallFromFile_rejects_wrong_size_or_hash_without_changing_target()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "new"));
        var target = test.CreateDirectory("target");
        await File.WriteAllTextAsync(Path.Combine(target, "mod.dll"), "old");
        var transactions = test.CreateDirectory("transactions");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package, target, transactions, new FileInfo(package).Length + 1, FileSha256(package)));
        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package, target, transactions, new FileInfo(package).Length, new string('0', 64)));

        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(target, "mod.dll")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(transactions));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/drive.txt")]
    [InlineData("folder/../../outside.txt")]
    [InlineData("mod.dll:payload")]
    [InlineData("mod.dll.")]
    [InlineData("mod.dll ")]
    public async Task InstallFromFile_rejects_unsafe_zip_paths(string entryName)
    {
        using var test = new TestDirectory();
        var package = test.CreateZip((entryName, "bad"));
        var target = test.CreateDirectory("target");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package,
            target,
            test.CreateDirectory("transactions"),
            new FileInfo(package).Length,
            FileSha256(package)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("file-directory-conflict")]
    public async Task InstallFromFile_rejects_ambiguous_zip_targets(string kind)
    {
        using var test = new TestDirectory();
        var package = kind == "duplicate"
            ? test.CreateZip(("Mod.dll", "one"), ("mod.dll", "two"))
            : test.CreateZip(("mods", "file"), ("mods/debug.dll", "nested"));
        var target = test.CreateDirectory("target");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package,
            target,
            test.CreateDirectory("transactions"),
            new FileInfo(package).Length,
            FileSha256(package)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public async Task InstallFromUri_requires_https_before_downloading()
    {
        using var test = new TestDirectory();

        await Assert.ThrowsAsync<ArgumentException>(() => PackageInstaller.InstallFromUriAsync(
            new Uri("http://example.invalid/mod.zip"),
            test.CreateDirectory("target"),
            test.CreateDirectory("transactions"),
            1,
            new string('0', 64)));
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(_root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateZip(params (string Name, string Content)[] entries)
        {
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, $"{Guid.NewGuid():N}.zip");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
                writer.Write(item.Content);
            }
            return path;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
