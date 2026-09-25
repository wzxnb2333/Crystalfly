using Crystalfly.Steam.Downloads;

namespace Crystalfly.Steam.Tests.Downloads;

public sealed class DownloadPathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"crystalfly-path-{Guid.NewGuid():N}");

    [Fact]
    public void ResolveUnderRootNormalizesSteamSeparators()
    {
        string actual = DownloadPath.ResolveUnderRoot(_root, "hollow_knight_Data/Managed/Assembly-CSharp.dll");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(_root, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll")),
            actual);
    }

    [Theory]
    [InlineData("../outside.dll")]
    [InlineData("folder/../../outside.dll")]
    [InlineData("C:/outside.dll")]
    [InlineData("\\\\server\\share\\outside.dll")]
    [InlineData("game.dat:stream")]
    public void ResolveUnderRootRejectsPathsOutsideStaging(string relativePath)
    {
        Assert.Throws<InvalidDataException>(() => DownloadPath.ResolveUnderRoot(_root, relativePath));
    }

    [Theory]
    [InlineData("linked/file.dll")]
    [InlineData("linked/nested/file.dll")]
    public void ResolveUnderRootRejectsReparsePointAncestors(string relativePath)
    {
        Directory.CreateDirectory(_root);
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        string staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(staging);
        Directory.CreateSymbolicLink(Path.Combine(staging, "linked"), outside);

        Assert.Throws<IOException>(() => DownloadPath.ResolveUnderRoot(staging, relativePath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public void ResolveUnderRootRejectsLinkedStagingRoot()
    {
        Directory.CreateDirectory(_root);
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        string staging = Path.Combine(_root, "staging");
        Directory.CreateSymbolicLink(staging, outside);

        Assert.Throws<IOException>(() => DownloadPath.ResolveUnderRoot(staging, "file.dll"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
