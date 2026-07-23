using System.Diagnostics;

namespace Crystalfly.Updater.Tests;

public sealed class InstalledUpdateInstallerTests
{
    [Fact]
    public void CreateStartInfo_uses_silent_inno_switches()
    {
        string installerPath = Path.GetFullPath(@"C:\updates\Crystalfly-Setup.exe");

        ProcessStartInfo startInfo = InstalledUpdateInstaller.CreateStartInfo(installerPath);

        Assert.Equal(installerPath, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal("runas", startInfo.Verb);
        Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        Assert.Equal(
            ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-"],
            startInfo.ArgumentList);
    }

    [Theory]
    [InlineData(@"C:\updates\release.zip")]
    [InlineData(@"C:\updates\missing.exe")]
    public async Task RunAsync_rejects_nonexistent_or_non_executable_asset(string assetPath)
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            InstalledUpdateInstaller.RunAsync(assetPath, CancellationToken.None));

        Assert.Contains("installer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
