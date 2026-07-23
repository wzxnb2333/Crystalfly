using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Updates;

namespace Crystalfly.App.Updates;

public enum ApplicationUpdateCheckStatus
{
    Offline,
    Disabled,
    NotDue,
    UpToDate,
    VersionSkipped,
    UpdateAvailable
}

public sealed record ApplicationUpdateCheckResult(
    ApplicationUpdateCheckStatus Status,
    UpdateManifest? Manifest = null,
    DateTimeOffset? CheckedAt = null);

public sealed class ApplicationUpdateService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
    private const int CopyBufferSize = 81920;

    private readonly HttpClient manifestHttpClient;
    private readonly HttpClient assetHttpClient;
    private readonly INetworkPolicy networkPolicy;
    private readonly Uri manifestUri;
    private readonly Version currentVersion;
    private readonly Func<string, UpdateManifest> verifyManifest;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim checkGate = new(1, 1);
    private DateTimeOffset? lastSuccessfulCheckAt;

    public ApplicationUpdateService(
        HttpClient httpClient,
        INetworkPolicy networkPolicy,
        Uri manifestUri,
        Version currentVersion,
        ReadOnlyMemory<byte> manifestPublicKey,
        TimeProvider? timeProvider = null)
        : this(
            httpClient,
            httpClient,
            networkPolicy,
            manifestUri,
            currentVersion,
            CreateManifestVerifier(manifestPublicKey),
            timeProvider ?? TimeProvider.System)
    {
    }

    public ApplicationUpdateService(
        HttpClient manifestHttpClient,
        HttpClient assetHttpClient,
        INetworkPolicy networkPolicy,
        Uri manifestUri,
        Version currentVersion,
        ReadOnlyMemory<byte> manifestPublicKey,
        TimeProvider? timeProvider = null)
        : this(
            manifestHttpClient,
            assetHttpClient,
            networkPolicy,
            manifestUri,
            currentVersion,
            CreateManifestVerifier(manifestPublicKey),
            timeProvider ?? TimeProvider.System)
    {
    }

    internal ApplicationUpdateService(
        HttpClient httpClient,
        INetworkPolicy networkPolicy,
        Uri manifestUri,
        Version currentVersion,
        Func<string, UpdateManifest> verifyManifest,
        TimeProvider timeProvider)
        : this(
            httpClient,
            httpClient,
            networkPolicy,
            manifestUri,
            currentVersion,
            verifyManifest,
            timeProvider)
    {
    }

    internal ApplicationUpdateService(
        HttpClient manifestHttpClient,
        HttpClient assetHttpClient,
        INetworkPolicy networkPolicy,
        Uri manifestUri,
        Version currentVersion,
        Func<string, UpdateManifest> verifyManifest,
        TimeProvider timeProvider)
    {
        this.manifestHttpClient = manifestHttpClient
            ?? throw new ArgumentNullException(nameof(manifestHttpClient));
        this.assetHttpClient = assetHttpClient
            ?? throw new ArgumentNullException(nameof(assetHttpClient));
        this.networkPolicy = networkPolicy ?? throw new ArgumentNullException(nameof(networkPolicy));
        this.manifestUri = manifestUri ?? throw new ArgumentNullException(nameof(manifestUri));
        this.currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
        this.verifyManifest = verifyManifest ?? throw new ArgumentNullException(nameof(verifyManifest));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        if (!manifestUri.IsAbsoluteUri || manifestUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The update manifest URI must be an absolute HTTPS URI.", nameof(manifestUri));
        }
    }

    public async Task<ApplicationUpdateCheckResult> CheckAsync(
        CrystalflySettings settings,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (networkPolicy.IsOffline)
        {
            return new(ApplicationUpdateCheckStatus.Offline);
        }
        if (!force && !settings.CheckForUpdates)
        {
            return new(ApplicationUpdateCheckStatus.Disabled);
        }
        await checkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (networkPolicy.IsOffline)
            {
                return new(ApplicationUpdateCheckStatus.Offline);
            }
            DateTimeOffset now = timeProvider.GetUtcNow();
            DateTimeOffset? previous = settings.LastUpdateCheckAt;
            if (lastSuccessfulCheckAt is { } processCheck
                && (previous is null || processCheck > previous))
            {
                previous = processCheck;
            }
            if (!force
                && previous is { } previousCheck
                && previousCheck <= now
                && now - previousCheck < CheckInterval)
            {
                return new(ApplicationUpdateCheckStatus.NotDue);
            }

            CancellationToken onlineCancellationToken;
            try
            {
                onlineCancellationToken = networkPolicy.GetOnlineCancellationToken();
            }
            catch (OfflineModeException)
            {
                return new(ApplicationUpdateCheckStatus.Offline);
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                onlineCancellationToken);

            try
            {
                using var response = await manifestHttpClient.GetAsync(
                    manifestUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedCancellation.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                string document = await ReadManifestDocumentAsync(
                    response.Content,
                    linkedCancellation.Token).ConfigureAwait(false);
                UpdateManifest manifest = verifyManifest(document);
                if (!Version.TryParse(manifest.Version, out var availableVersion))
                {
                    throw new InvalidDataException("The update manifest version is invalid.");
                }
                DateTimeOffset checkedAt = timeProvider.GetUtcNow();
                lastSuccessfulCheckAt = checkedAt;

                if (availableVersion <= currentVersion)
                {
                    return new(ApplicationUpdateCheckStatus.UpToDate, CheckedAt: checkedAt);
                }
                if (!force && string.Equals(
                    settings.SkippedUpdateVersion,
                    manifest.Version,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new(ApplicationUpdateCheckStatus.VersionSkipped, CheckedAt: checkedAt);
                }

                return new(ApplicationUpdateCheckStatus.UpdateAvailable, manifest, checkedAt);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && networkPolicy.IsOffline)
            {
                return new(ApplicationUpdateCheckStatus.Offline);
            }
        }
        finally
        {
            checkGate.Release();
        }
    }

    public async Task<string> DownloadAssetAsync(
        UpdateAsset asset,
        string tempDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempDirectory);

        if (asset.Size <= 0
            || !Uri.TryCreate(asset.Url, UriKind.Absolute, out var assetUri)
            || assetUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("The update asset metadata is invalid.");
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(asset.Sha256);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The update asset hash is invalid.", exception);
        }
        if (expectedHash.Length != SHA256.HashSizeInBytes)
        {
            throw new InvalidDataException("The update asset hash is invalid.");
        }

        Directory.CreateDirectory(tempDirectory);
        string extension = asset.Kind == UpdateAssetKind.Installer ? ".exe" : ".zip";
        string tempPath = Path.Combine(tempDirectory, $"Crystalfly-{Guid.NewGuid():N}{extension}");
        CancellationToken onlineCancellationToken = networkPolicy.GetOnlineCancellationToken();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            onlineCancellationToken);

        try
        {
            using var response = await assetHttpClient.GetAsync(
                assetUri,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellation.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is { } contentLength
                && contentLength != asset.Size)
            {
                throw new InvalidDataException("The update asset size does not match the manifest.");
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(
                linkedCancellation.Token).ConfigureAwait(false);
            await using var destination = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
            long totalBytes = 0;
            try
            {
                while (true)
                {
                    int bytesRead = await source.ReadAsync(
                        buffer.AsMemory(0, CopyBufferSize),
                        linkedCancellation.Token).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytes += bytesRead;
                    if (totalBytes > asset.Size)
                    {
                        throw new InvalidDataException("The update asset is larger than the manifest size.");
                    }

                    hash.AppendData(buffer, 0, bytesRead);
                    await destination.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        linkedCancellation.Token).ConfigureAwait(false);
                }

                linkedCancellation.Token.ThrowIfCancellationRequested();
                await destination.FlushAsync(linkedCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            byte[] actualHash = hash.GetHashAndReset();
            if (totalBytes != asset.Size
                || !CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                throw new InvalidDataException("The update asset does not match the manifest.");
            }

            return tempPath;
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }

    private static Func<string, UpdateManifest> CreateManifestVerifier(
        ReadOnlyMemory<byte> manifestPublicKey)
    {
        byte[] publicKey = manifestPublicKey.ToArray();
        return document => UpdateManifestVerifier.Verify(document, publicKey);
    }

    private static async Task<string> ReadManifestDocumentAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > UpdateManifestVerifier.MaxEnvelopeBytes)
        {
            throw new InvalidDataException("The update manifest envelope is too large.");
        }

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            while (true)
            {
                int bytesRead = await source.ReadAsync(
                    buffer.AsMemory(0, CopyBufferSize),
                    cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }
                if (destination.Length + bytesRead > UpdateManifestVerifier.MaxEnvelopeBytes)
                {
                    throw new InvalidDataException("The update manifest envelope is too large.");
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken).ConfigureAwait(false);
            }

            return Encoding.UTF8.GetString(destination.GetBuffer(), 0, checked((int)destination.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

}
