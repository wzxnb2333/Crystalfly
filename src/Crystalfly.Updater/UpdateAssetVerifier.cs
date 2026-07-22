using System.Security.Cryptography;

namespace Crystalfly.Updater;

internal static class UpdateAssetVerifier
{
    public static async Task<IDisposable> VerifyAndLockAsync(
        string path,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(expectedSha256);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Update asset SHA-256 is invalid.", exception);
        }
        if (expectedSize <= 0 || expectedHash.Length != SHA256.HashSizeInBytes)
        {
            throw new InvalidDataException("Update asset verification metadata is invalid.");
        }

        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            if (stream.Length != expectedSize)
            {
                throw new InvalidDataException("Update asset size changed after download verification.");
            }
            byte[] actualHash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                throw new InvalidDataException("Update asset hash changed after download verification.");
            }

            return stream;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
