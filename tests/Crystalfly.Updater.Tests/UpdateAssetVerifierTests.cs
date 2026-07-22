using System.Security.Cryptography;

namespace Crystalfly.Updater.Tests;

public sealed class UpdateAssetVerifierTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"Crystalfly-update-verifier-{Guid.NewGuid():N}");

    [Fact]
    public async Task VerifyAndLockAsync_keeps_verified_asset_locked_against_replacement()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "setup.exe");
        byte[] content = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(path, content);

        using IDisposable lease = await UpdateAssetVerifier.VerifyAndLockAsync(
            path,
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)),
            CancellationToken.None);

        Assert.Throws<IOException>(() => new FileStream(
            path,
            FileMode.Open,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete));
    }

    [Fact]
    public async Task VerifyAndLockAsync_rejects_content_changed_after_client_verification()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "setup.exe");
        await File.WriteAllBytesAsync(path, [9, 9, 9]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UpdateAssetVerifier.VerifyAndLockAsync(
                path,
                3,
                new string('A', 64),
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
