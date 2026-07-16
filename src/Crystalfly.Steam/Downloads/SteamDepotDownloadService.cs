using System.Security.Cryptography;

namespace Crystalfly.Steam.Downloads;

public sealed class SteamDepotDownloadService(
    ISteamContentDeliveryClient content,
    Action<SteamDownloadProgress>? progress = null)
{
    public async Task<SteamDownloadResult> DownloadAsync(
        SteamDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StagingDirectory);
        if (!string.Equals(request.Branch, "public", StringComparison.Ordinal))
            throw new NotSupportedException("Only the public Steam branch is supported.");

        SteamDepotManifest manifest = await content.GetManifestAsync(
            SteamProduct.HollowKnightAppId,
            SteamProduct.HollowKnightWindowsDepotId,
            request.Branch,
            request.ManifestId,
            cancellationToken);
        long totalBytes = manifest.Files.Sum(static file => file.Size);
        var aggregator = new DownloadProgressAggregator(totalBytes, progress);
        string staging = Path.GetFullPath(request.StagingDirectory);
        string[] targets = manifest.Files
            .Select(file => DownloadPath.ResolveUnderRoot(staging, file.RelativePath))
            .ToArray();
        Directory.CreateDirectory(staging);

        var completedFiles = new List<string>(manifest.Files.Count);
        for (int index = 0; index < manifest.Files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SteamDepotFile file = manifest.Files[index];
            string target = targets[index];
            string partial = target + ".crystalfly-part";
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Delete(partial);
            try
            {
                await using (var output = new FileStream(
                    partial,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    131072,
                    FileOptions.Asynchronous | FileOptions.RandomAccess))
                {
                    output.SetLength(file.Size);
                    foreach (SteamDepotChunk chunk in file.Chunks.OrderBy(static chunk => chunk.Offset))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ReadOnlyMemory<byte> bytes = await content.DownloadChunkAsync(chunk, CancellationToken.None);
                        if (bytes.Length != chunk.UncompressedLength || chunk.Offset < 0 || chunk.Offset + bytes.Length > file.Size)
                            throw new InvalidDataException($"Invalid chunk data for {file.RelativePath}.");

                        output.Position = chunk.Offset;
                        await output.WriteAsync(bytes, CancellationToken.None);
                        aggregator.CompleteChunk(bytes.Length, file.RelativePath);
                    }

                    await output.FlushAsync(CancellationToken.None);
                    output.Flush(flushToDisk: true);
                }

                await VerifyFileAsync(partial, file.Sha1, cancellationToken);
                File.Move(partial, target, overwrite: true);
                completedFiles.Add(file.RelativePath);
            }
            finally
            {
                File.Delete(partial);
            }
        }

        return new SteamDownloadResult(
            SteamProduct.HollowKnightAppId,
            SteamProduct.HollowKnightWindowsDepotId,
            manifest.Id,
            staging,
            completedFiles,
            totalBytes);
    }

    private static async Task VerifyFileAsync(string path, string expectedSha1, CancellationToken cancellationToken)
    {
        await using FileStream input = File.OpenRead(path);
        byte[] actual = await SHA1.HashDataAsync(input, cancellationToken);
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedSha1);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Manifest contains an invalid SHA-1 hash.", exception);
        }

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            throw new InvalidDataException($"SHA-1 verification failed for {path}.");
    }
}
