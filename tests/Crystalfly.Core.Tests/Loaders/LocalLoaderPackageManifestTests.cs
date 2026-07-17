using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Loaders;

public sealed class LocalLoaderPackageManifestTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Load_returns_an_unverified_local_loader_after_validating_the_package()
    {
        var package = CreateZip();
        var manifestPath = await WriteManifestAsync(package);

        var result = await LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1");

        Assert.Equal(Path.GetFullPath(package), result.PackagePath);
        Assert.Equal(LoaderState.ModdingApi, result.LoaderState);
        Assert.False(result.IsVerified);
        Assert.Equal("Unverified", result.VerificationStatus);
        Assert.Equal("local-loader", result.Manifest.Id);
        Assert.Equal(new FileInfo(package).Length, result.Manifest.SizeBytes);
        Assert.Equal(FileSha256(package), result.Manifest.Sha256);
        Assert.Equal(new Uri(package).AbsoluteUri, result.Manifest.DownloadUrl);
        Assert.Equal(["build-1"], result.Manifest.SupportedBuildIds);
        Assert.Equal(["hollow_knight_Data/Managed/loader.dll"], result.Manifest.ManagedFiles);
    }

    [Fact]
    public async Task Load_applies_loader_specific_prefix_when_matching_managed_files()
    {
        var package = CreateZip(("BepInEx/core/BepInEx.dll", "loader"));
        var manifestPath = await WriteManifestAsync(
            package,
            loaderState: "bepInEx",
            managedFiles: ["BepInEx/core/BepInEx.dll"]);

        var result = await LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1");

        Assert.Equal(LoaderState.BepInEx, result.LoaderState);
        Assert.Equal(["BepInEx/core/BepInEx.dll"], result.Manifest.ManagedFiles);
    }

    [Theory]
    [InlineData("hollow_knight_Data/Managed/Assembly-CSharp.dll")]
    [InlineData("hollow_knight_Data/Managed/loader.dll,hollow_knight_Data/Managed/extra.dll")]
    public async Task Load_rejects_managed_files_that_do_not_exactly_match_zip_files(string declared)
    {
        var package = CreateZip(("loader.dll", "loader"));
        var manifestPath = await WriteManifestAsync(
            package,
            managedFiles: declared.Split(','));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1"));
    }

    [Theory]
    [InlineData(2, "moddingApi")]
    [InlineData(1, "vanilla")]
    [InlineData(1, "conflict")]
    [InlineData(1, "drifted")]
    [InlineData(1, "unknown")]
    public async Task Load_rejects_unsupported_schema_or_loader_state(int schemaVersion, string loaderState)
    {
        var package = CreateZip();
        var manifestPath = await WriteManifestAsync(package, schemaVersion: schemaVersion, loaderState: loaderState);

        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1"));
    }

    [Theory]
    [InlineData("other-build", 0, null)]
    [InlineData("build-1", 1, null)]
    [InlineData("build-1", 0, "invalid")]
    [InlineData("build-1", 0, "0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Load_rejects_build_size_and_hash_mismatches(
        string expectedBuildId,
        long sizeOffset,
        string? sha256)
    {
        var package = CreateZip();
        var manifestPath = await WriteManifestAsync(package, sizeOffset: sizeOffset, sha256: sha256);

        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, expectedBuildId));
    }

    [Theory]
    [InlineData("missing.zip")]
    [InlineData("package.dll")]
    [InlineData("../package.zip")]
    public async Task Load_rejects_missing_non_zip_or_traversing_package_paths(string packageFile)
    {
        var package = CreateZip();
        if (packageFile == "package.dll")
        {
            package = Path.ChangeExtension(package, ".dll");
            File.Move(Path.ChangeExtension(package, ".zip"), package);
        }
        var manifestPath = await WriteManifestAsync(package, packageFile: packageFile);

        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1"));
    }

    [Fact]
    public async Task Load_rejects_absolute_package_path()
    {
        var package = CreateZip();
        var manifestPath = await WriteManifestAsync(package, packageFile: package);

        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1"));
    }

    [Fact]
    public async Task Load_rejects_a_file_with_zip_extension_but_invalid_content()
    {
        Directory.CreateDirectory(_root);
        var package = Path.Combine(_root, "invalid.zip");
        await File.WriteAllTextAsync(package, "not a zip");
        var manifestPath = await WriteManifestAsync(package);

        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1"));
    }

    [Fact]
    public async Task Load_rejects_package_path_that_contains_a_reparse_point()
    {
        var package = CreateZip("outside.zip");
        var links = Path.Combine(_root, "links");
        Directory.CreateDirectory(links);
        var link = Path.Combine(links, "package.zip");
        File.CreateSymbolicLink(link, package);
        var manifestPath = await WriteManifestAsync(link, packageFile: "links/package.zip");

        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1"));
    }

    [Fact]
    public async Task Load_rejects_unsafe_managed_file_paths()
    {
        var package = CreateZip();
        var manifestPath = await WriteManifestAsync(
            package,
            managedFiles: ["../Assembly-CSharp.dll"]);

        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            LocalLoaderPackageManifest.LoadAsync(manifestPath, "build-1"));
    }

    private string CreateZip(string fileName = "package.zip") =>
        CreateZip(fileName, ("loader.dll", "loader"));

    private string CreateZip(params (string Name, string Content)[] entries) =>
        CreateZip("package.zip", entries);

    private string CreateZip(string fileName, params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(item.Content);
        }
        return path;
    }

    private async Task<string> WriteManifestAsync(
        string package,
        int schemaVersion = 1,
        string loaderState = "moddingApi",
        string? packageFile = null,
        long sizeOffset = 0,
        string? sha256 = null,
        IReadOnlyList<string>? managedFiles = null)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            schemaVersion,
            id = "local-loader",
            name = "Local Loader",
            version = "1.0",
            loaderState,
            packageFile = packageFile ?? Path.GetFileName(package),
            sizeBytes = new FileInfo(package).Length + sizeOffset,
            sha256 = sha256 ?? FileSha256(package),
            supportedBuildIds = new[] { "build-1" },
            managedFiles = managedFiles ?? ["hollow_knight_Data/Managed/loader.dll"]
        }));
        return path;
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
