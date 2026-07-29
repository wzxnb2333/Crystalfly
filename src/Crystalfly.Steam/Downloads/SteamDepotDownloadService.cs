using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;

namespace Crystalfly.Steam.Downloads;

public sealed class SteamDepotDownloadService(
    ISteamContentDeliveryClient content,
    Action<SteamDownloadProgress>? progress = null)
{
    private const int MaxConcurrentChunks = 16;

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
        IReadOnlyList<SteamDepotFile> files = request.RepairSourceDirectory is null
            ? manifest.Files
            : await SelectRepairFilesAsync(
                manifest.Files,
                request.RepairSourceDirectory,
                request.RepairSha256,
                cancellationToken);
        string staging = Path.GetFullPath(request.StagingDirectory);
        string[] targets = files
            .Select(file => DownloadPath.ResolveUnderRoot(staging, file.RelativePath))
            .ToArray();
        string appIdTarget = DownloadPath.ResolveUnderRoot(staging, "steam_appid.txt");
        bool manifestProvidesAppId = targets.Contains(appIdTarget, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(staging);
        foreach (string target in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Delete(target + ".crystalfly-part");
        }
        if (!manifestProvidesAppId)
        {
            File.Delete(appIdTarget);
            File.Delete(appIdTarget + ".crystalfly-part");
        }

        SteamDepotChunk[][] chunksByFile = files
            .Select(ValidateAndOrderChunks)
            .ToArray();
        long totalBytes = 0;
        try
        {
            foreach (SteamDepotFile file in files)
                totalBytes = checked(totalBytes + file.Size);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("Manifest total size exceeds the supported range.", exception);
        }
        var aggregator = new DownloadProgressAggregator(totalBytes, progress);

        var completedFiles = new List<string>(files.Count);
        for (int index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SteamDepotFile file = files[index];
            string target = targets[index];
            string partial = target + ".crystalfly-part";
            SteamDepotChunk[] chunks = chunksByFile[index];
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

        if (!manifestProvidesAppId
            && (request.RepairSourceDirectory is null
                || !File.Exists(Path.Combine(request.RepairSourceDirectory, "steam_appid.txt"))))
            await WriteSteamAppIdAsync(appIdTarget, cancellationToken);

        return new SteamDownloadResult(
            SteamProduct.HollowKnightAppId,
            SteamProduct.HollowKnightWindowsDepotId,
            manifest.Id,
            staging,
            completedFiles,
            totalBytes);
    }

    private static async Task<IReadOnlyList<SteamDepotFile>> SelectRepairFilesAsync(
        IReadOnlyList<SteamDepotFile> manifestFiles,
        string sourceDirectory,
        IReadOnlyDictionary<string, string>? repairSha256,
        CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Repair source '{source}' was not found.");
        }
        var expectedHashes = (repairSha256 ?? new Dictionary<string, string>())
            .ToDictionary(
                pair => pair.Key.Replace('\\', '/'),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        var selected = new List<SteamDepotFile>();
        foreach (var file in manifestFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = DownloadPath.ResolveUnderRoot(source, file.RelativePath);
            if (!File.Exists(target))
            {
                selected.Add(file);
                continue;
            }
            if (!expectedHashes.TryGetValue(file.RelativePath.Replace('\\', '/'), out var expected))
            {
                continue;
            }
            await using var stream = new FileStream(
                target,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                selected.Add(file);
            }
        }
        return selected;
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

    private static async Task WriteSteamAppIdAsync(string target, CancellationToken cancellationToken)
    {
        string partial = target + ".crystalfly-part";
        Exception? operationFailure = null;
        try
        {
            byte[] content = Encoding.ASCII.GetBytes(
                SteamProduct.HollowKnightAppId.ToString(CultureInfo.InvariantCulture));
            await using (var output = new FileStream(
                partial,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            {
                await output.WriteAsync(content, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, target, overwrite: true);
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
                operationFailure.Data["Crystalfly.AppIdCleanupError"] = cleanupException;
            }
        }
    }
}
