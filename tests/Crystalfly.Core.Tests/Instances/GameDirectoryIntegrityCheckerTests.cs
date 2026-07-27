using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class GameDirectoryIntegrityCheckerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-game-integrity-{Guid.NewGuid():N}");

    [Fact]
    public void Inspect_accepts_core_files_without_UnityPlayer_and_ignores_extra_files()
    {
        CreateCoreFiles();
        Directory.CreateDirectory(Path.Combine(root, "skins"));
        File.WriteAllText(Path.Combine(root, "skins", "custom.png"), "modified texture");

        var report = GameDirectoryIntegrityChecker.Inspect(root);

        Assert.True(report.IsValid);
        Assert.False(report.UnityPlayerExists);
        Assert.Empty(report.MissingRequiredFiles);
    }

    [Fact]
    public void Inspect_reports_only_missing_required_core_files()
    {
        Directory.CreateDirectory(root);

        var report = GameDirectoryIntegrityChecker.Inspect(root);

        Assert.False(report.IsValid);
        Assert.Equal(
            ["hollow_knight.exe", "hollow_knight_Data/globalgamemanagers"],
            report.MissingRequiredFiles);
    }

    [Fact]
    public void Inspect_rejects_pending_download_and_missing_loader_input()
    {
        CreateCoreFiles();
        File.WriteAllText(Path.Combine(root, InstanceDirectory.PendingDownloadMarkerFileName), "{}");

        var report = GameDirectoryIntegrityChecker.Inspect(root, ["hollow_knight_Data/Managed/Assembly-CSharp.dll"]);

        Assert.False(report.IsValid);
        Assert.True(report.HasPendingDownload);
        Assert.Contains("hollow_knight_Data/Managed/Assembly-CSharp.dll", report.MissingRequiredFiles);
    }

    private void CreateCoreFiles()
    {
        Directory.CreateDirectory(Path.Combine(root, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(root, "hollow_knight.exe"), "exe");
        File.WriteAllText(Path.Combine(root, "hollow_knight_Data", "globalgamemanagers"), "data");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
