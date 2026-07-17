using System.Runtime.ExceptionServices;
using System.Security.Cryptography;

namespace Crystalfly.Steam.Downloads;

public sealed class SteamDepotDownloadService(
    ISteamContentDeliveryClient content,
    Action<SteamDownloadProgress>? progress = null)
{
    private const int MaxConcurrentChunks = 4;

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
        string staging = Path.GetFullPath(request.StagingDirectory);
        string[] targets = manifest.Files
            .Select(file => DownloadPath.ResolveUnderRoot(staging, file.RelativePath))
            .ToArray();
        Directory.CreateDirectory(staging);
        foreach (string target in targets)
            File.Delete(target + ".crystalfly-part");

        SteamDepotChunk[][] chunksByFile = manifest.Files
            .Select(ValidateAndOrderChunks)
            .ToArray();
        long totalBytes = 0;
        try
        {
            foreach (SteamDepotFile file in manifest.Files)
                totalBytes = checked(totalBytes + file.Size);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("Manifest total size exceeds the supported range.", exception);
        }
        var aggregator = new DownloadProgressAggregator(totalBytes, progress);

        var completedFiles = new List<string>(manifest.Files.Count);
        for (int index = 0; index < manifest.Files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SteamDepotFile file = manifest.Files[index];
            string target = targets[index];
            string partial = target + ".crystalfly-part";
            SteamDepotChunk[] chunks = chunksByFile[index];
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Exception? operationFailure = null;
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
                    using var failureCancellation = new CancellationTokenSource();
                    using var parallelCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        failureCancellation.Token);
                    Exception? chunkFailure = null;
                    try
                    {
                        await Parallel.ForEachAsync(
                            chunks,
                            new ParallelOptions
                            {
                                MaxDegreeOfParallelism = MaxConcurrentChunks,
                                CancellationToken = parallelCancellation.Token
                            },
                            async (chunk, _) =>
                            {
                                parallelCancellation.Token.ThrowIfCancellationRequested();
                                try
                                {
                                    ReadOnlyMemory<byte> bytes = await content.DownloadChunkAsync(
                                        chunk,
                                        CancellationToken.None);
                                    if (bytes.Length != chunk.UncompressedLength)
                                        throw new InvalidDataException($"Invalid chunk data for {file.RelativePath}.");

                                    await RandomAccess.WriteAsync(
                                        output.SafeFileHandle,
                                        bytes,
                                        chunk.Offset,
                                        CancellationToken.None);
                                    aggregator.CompleteChunk(bytes.Length, file.RelativePath);
                                }
                                catch (Exception exception)
                                {
                                    if (Interlocked.CompareExchange(ref chunkFailure, exception, null) is null)
                                        failureCancellation.Cancel();
                                    throw;
                                }
                            });
                    }
                    catch when (Volatile.Read(ref chunkFailure) is not null)
                    {
                        ExceptionDispatchInfo.Capture(chunkFailure!).Throw();
                        throw;
                    }

                    await output.FlushAsync(CancellationToken.None);
                    output.Flush(flushToDisk: true);
                }

                await VerifyFileAsync(partial, file.Sha1, cancellationToken);
                File.Move(partial, target, overwrite: true);
                completedFiles.Add(file.RelativePath);
            }
            catch (Exception exception)
            {
                operationFailure = exception;
                throw;
            }
            finally
            {
                try
                {
                    File.Delete(partial);
                }
                catch (Exception cleanupException) when (operationFailure is not null)
                {
                    operationFailure.Data["Crystalfly.PartialCleanupError"] = cleanupException;
                }
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

    private static SteamDepotChunk[] ValidateAndOrderChunks(SteamDepotFile file)
    {
        if (file.Size < 0)
            throw new InvalidDataException($"Invalid file size for {file.RelativePath}.");

        SteamDepotChunk[] chunks = file.Chunks
            .OrderBy(static chunk => chunk.Offset)
            .ToArray();
        long previousEnd = 0;
        foreach (SteamDepotChunk chunk in chunks)
        {
            if (chunk.Offset < 0
                || chunk.UncompressedLength < 0
                || chunk.Offset > file.Size - chunk.UncompressedLength
                || chunk.Offset < previousEnd)
            {
                throw new InvalidDataException($"Invalid chunk layout for {file.RelativePath}.");
            }
            previousEnd = chunk.Offset + chunk.UncompressedLength;
        }
        return chunks;
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
