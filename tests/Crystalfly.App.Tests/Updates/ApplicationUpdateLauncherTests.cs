using Crystalfly.App.Updates;

namespace Crystalfly.App.Tests.Updates;

public sealed class ApplicationUpdateLauncherTests
{
    [Fact]
    public void CreateStartInfo_passes_verified_paths_and_parent_process()
    {
        string updater = Path.GetFullPath(@"C:\Temp\Crystalfly.Updater.exe");
        string target = Path.GetFullPath(@"C:\Apps\Crystalfly");
        string restart = Path.Combine(target, "Crystalfly.App.exe");
        var request = new ApplicationUpdateLaunchRequest(
            42,
            ApplicationInstallationMode.Portable,
            Path.GetFullPath(@"C:\Temp\release.zip"),
            1234,
            new string('A', 64),
            target,
            restart);

        var startInfo = ApplicationUpdateLauncher.CreateStartInfo(updater, request);

        Assert.Equal(updater, startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(
            [
                "--parent-pid", "42",
                "--mode", "Portable",
                "--asset", request.AssetPath,
                "--size", "1234",
                "--sha256", request.ExpectedSha256,
                "--target", target,
                "--restart", restart
            ],
            startInfo.ArgumentList);
    }

    [Fact]
    public void CleanupExpiredAssets_removes_only_old_update_files()
    {
        string root = Path.Combine(Path.GetTempPath(), $"Crystalfly-update-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string oldAsset = Path.Combine(root, "old.zip");
            string recentAsset = Path.Combine(root, "recent.exe");
            File.WriteAllText(oldAsset, "old");
            File.WriteAllText(recentAsset, "recent");
            DateTimeOffset now = new(2026, 7, 23, 8, 0, 0, TimeSpan.Zero);
            File.SetLastWriteTimeUtc(oldAsset, now.UtcDateTime.AddDays(-8));
            File.SetLastWriteTimeUtc(recentAsset, now.UtcDateTime.AddDays(-1));

            ApplicationUpdateLauncher.CleanupExpiredAssets(root, now, TimeSpan.FromDays(7));

            Assert.False(File.Exists(oldAsset));
            Assert.True(File.Exists(recentAsset));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
