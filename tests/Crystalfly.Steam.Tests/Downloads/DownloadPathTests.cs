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

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
