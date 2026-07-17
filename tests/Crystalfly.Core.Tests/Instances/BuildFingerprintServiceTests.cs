using System.Text;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Instances;

public sealed class BuildFingerprintServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-fingerprint-{Guid.NewGuid():N}");

    [Fact]
    public async Task Calculate_hashes_complete_build_fingerprint()
    {
        Directory.CreateDirectory(Path.Combine(root, "hollow_knight_Data"));
        await File.WriteAllBytesAsync(Path.Combine(root, "hollow_knight.exe"), Encoding.UTF8.GetBytes("exe"));
        await File.WriteAllBytesAsync(Path.Combine(root, "UnityPlayer.dll"), Encoding.UTF8.GetBytes("unity"));
        await File.WriteAllBytesAsync(Path.Combine(root, "hollow_knight_Data", "globalgamemanagers"), Encoding.UTF8.GetBytes("global"));

        var fingerprint = await BuildFingerprintService.CalculateAsync(root);

        Assert.Equal("9095BDB859308B62ACF04036FFD4ADFE366D7F737D276EB6C46AE434F3816C9B", fingerprint.ExecutableSha256);
        Assert.Equal("A5790B06F63B7C1646F0DE34B44FC108377A02FB07AA60B83AAFF44DEED06398", fingerprint.UnityPlayerSha256);
        Assert.Equal("8001C27439650C5C5A6B4ED94163B5DDEB4476362C71380E613FA20DFFFCEF50", fingerprint.GlobalGameManagersSha256);
    }

    [Fact]
    public void FindBuild_matches_all_unmodified_identity_files()
    {
        var fingerprint = new BuildFingerprint
        {
            ExecutableSha256 = "EXE",
            UnityPlayerSha256 = "UNITY",
            GlobalGameManagersSha256 = "GLOBAL"
        };
        var builds = new[]
        {
            new GameBuild
            {
                Id = "known",
                DisplayVersion = "Known",
                ManifestId = "1",
                ExecutableSha256 = "exe",
                UnityPlayerSha256 = "unity",
                GlobalGameManagersSha256 = "global"
            }
        };

        Assert.Equal("known", BuildFingerprintService.FindBuild(builds, fingerprint)?.Id);
        Assert.Null(BuildFingerprintService.FindBuild(
            builds,
            fingerprint with { GlobalGameManagersSha256 = "changed" }));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
