using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceDirectoryTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "crystalfly-instance-directory",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PrepareVersionRootRegistration_creates_metadata_root_for_an_empty_directory()
    {
        var versionRoot = Path.Combine(root, "library");
        Directory.CreateDirectory(versionRoot);

        InstanceDirectory.PrepareVersionRootRegistration(versionRoot);

        Assert.True(Directory.Exists(Path.Combine(versionRoot, ".crystalfly")));
    }

    [Fact]
    public void PrepareVersionRootRegistration_rejects_a_non_empty_directory()
    {
        var versionRoot = Path.Combine(root, "library");
        Directory.CreateDirectory(versionRoot);
        File.WriteAllText(Path.Combine(versionRoot, "keep.txt"), "keep");

        Assert.Throws<InvalidDataException>(() =>
            InstanceDirectory.PrepareVersionRootRegistration(versionRoot));
        Assert.False(Directory.Exists(Path.Combine(versionRoot, ".crystalfly")));
    }

    [Fact]
    public void PrepareVersionRootRegistration_allows_an_existing_metadata_directory()
    {
        var versionRoot = Path.Combine(root, "library");
        Directory.CreateDirectory(Path.Combine(versionRoot, ".crystalfly"));

        InstanceDirectory.PrepareVersionRootRegistration(versionRoot);

        Assert.True(Directory.Exists(Path.Combine(versionRoot, ".crystalfly")));
    }

    [Fact]
    public void PrepareVersionRootRegistration_preserves_a_registered_non_empty_library()
    {
        var versionRoot = Path.Combine(root, "library");
        var metadata = Directory.CreateDirectory(Path.Combine(versionRoot, ".crystalfly")).FullName;
        var gameFile = Path.Combine(versionRoot, "keep.txt");
        var metadataFile = Path.Combine(metadata, "keep.txt");
        File.WriteAllText(gameFile, "game");
        File.WriteAllText(metadataFile, "metadata");

        InstanceDirectory.PrepareVersionRootRegistration(versionRoot, isRegisteredVersionRoot: true);

        Assert.Equal("game", File.ReadAllText(gameFile));
        Assert.Equal("metadata", File.ReadAllText(metadataFile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrepareVersionRootRegistration_rejects_a_missing_directory(bool isRegisteredVersionRoot)
    {
        var versionRoot = Path.Combine(root, "missing");

        Assert.Throws<DirectoryNotFoundException>(() =>
            InstanceDirectory.PrepareVersionRootRegistration(versionRoot, isRegisteredVersionRoot));
        Assert.False(Directory.Exists(versionRoot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrepareVersionRootRegistration_rejects_a_folder_nested_in_a_version_root(bool isRegisteredVersionRoot)
    {
        var versionRoot = Path.Combine(root, "library");
        Directory.CreateDirectory(Path.Combine(versionRoot, ".crystalfly"));
        var nested = Path.Combine(versionRoot, "nested");
        Directory.CreateDirectory(nested);

        Assert.Throws<InvalidDataException>(() =>
            InstanceDirectory.PrepareVersionRootRegistration(nested, isRegisteredVersionRoot));
        Assert.False(Directory.Exists(Path.Combine(nested, ".crystalfly")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrepareVersionRootRegistration_rejects_a_complete_game_installation(bool isRegisteredVersionRoot)
    {
        var gameRoot = Path.Combine(root, "Hollow Knight");
        Directory.CreateDirectory(Path.Combine(gameRoot, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(gameRoot, "hollow_knight.exe"), "exe");
        File.WriteAllText(Path.Combine(gameRoot, "hollow_knight_Data", "globalgamemanagers"), "data");

        Assert.Throws<InvalidDataException>(() =>
            InstanceDirectory.PrepareVersionRootRegistration(gameRoot, isRegisteredVersionRoot));
        Assert.False(Directory.Exists(Path.Combine(gameRoot, ".crystalfly")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("name.")]
    [InlineData("name ")]
    [InlineData("CON")]
    [InlineData("NUL.txt")]
    [InlineData("COM1")]
    [InlineData("LPT9.log")]
    [InlineData("COM\u00B9")]
    [InlineData("COM\u00B9.txt")]
    [InlineData("COM\u00B2")]
    [InlineData("COM\u00B2.txt")]
    [InlineData("COM\u00B3")]
    [InlineData("COM\u00B3.txt")]
    [InlineData("LPT\u00B9")]
    [InlineData("LPT\u00B9.txt")]
    [InlineData("LPT\u00B2")]
    [InlineData("LPT\u00B2.txt")]
    [InlineData("LPT\u00B3")]
    [InlineData("LPT\u00B3.txt")]
    [InlineData(@"nested\child")]
    [InlineData("nested/child")]
    public void ResolveUnderRoot_rejects_invalid_windows_directory_names(string name) =>
        Assert.Throws<ArgumentException>(() => InstanceDirectory.ResolveUnderRoot(root, name));

    [Fact]
    public void ResolveUnderRoot_rejects_names_longer_than_255_utf16_code_units()
    {
        var name = new string('a', 256);

        Assert.Throws<ArgumentException>(() => InstanceDirectory.ResolveUnderRoot(root, name));
    }
}
