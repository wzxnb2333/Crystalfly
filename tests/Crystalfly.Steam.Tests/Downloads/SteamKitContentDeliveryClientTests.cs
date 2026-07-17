using System.Collections.Concurrent;
using System.Net;
using Crystalfly.Steam.Downloads;
using SteamKit2;

namespace Crystalfly.Steam.Tests.Downloads;

public sealed class SteamKitContentDeliveryClientTests
{
    [Fact]
    public async Task PublicDownloadStartsWithoutRequestingCdnToken()
    {
        var receivedTokens = new List<string?>();
        var tokenRequests = 0;

        (int value, string? authToken) = await SteamKitContentDeliveryClient.DownloadWithCdnAuthAsync(
            null,
            token =>
            {
                receivedTokens.Add(token);
                return Task.FromResult(7);
            },
            () =>
            {
                tokenRequests++;
                return Task.FromResult((EResult.OK, (string?)"unused"));
            });

        Assert.Equal(7, value);
        Assert.Null(authToken);
        Assert.Equal([null], receivedTokens);
        Assert.Equal(0, tokenRequests);
    }

    [Fact]
    public async Task ForbiddenDownloadRequestsTokenAndRetries()
    {
        var receivedTokens = new List<string?>();
        var tokenRequests = 0;

        (int value, string? authToken) = await SteamKitContentDeliveryClient.DownloadWithCdnAuthAsync(
            null,
            token =>
            {
                receivedTokens.Add(token);
                if (receivedTokens.Count == 1)
                {
                    throw new SteamKitWebRequestException(
                        "Forbidden",
                        new HttpResponseMessage(HttpStatusCode.Forbidden));
                }
                return Task.FromResult(9);
            },
            () =>
            {
                tokenRequests++;
                return Task.FromResult((EResult.OK, (string?)"granted"));
            });

        Assert.Equal(9, value);
        Assert.Equal("granted", authToken);
        Assert.Equal([null, "granted"], receivedTokens);
        Assert.Equal(1, tokenRequests);
    }

    [Fact]
    public async Task ConcurrentForbiddenDownloadsRefreshTokenOnceAndRetryWithNewToken()
    {
        const int downloadCount = 4;
        var allOldTokenAttemptsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldTokenAttempts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedTokens = new ConcurrentBag<string?>();
        var oldTokenAttempts = 0;
        var tokenRequests = 0;
        var tokenState = new SteamKitContentDeliveryClient.CdnAuthTokenState("old", 0);
        using var refreshGate = new SemaphoreSlim(1, 1);

        async Task<int> Download(string? token)
        {
            receivedTokens.Add(token);
            if (token == "old")
            {
                if (Interlocked.Increment(ref oldTokenAttempts) == downloadCount)
                    allOldTokenAttemptsStarted.SetResult();
                await releaseOldTokenAttempts.Task;
                throw CreateWebRequestException(HttpStatusCode.Forbidden);
            }

            return 7;
        }

        Task<int>[] downloads = Enumerable.Range(0, downloadCount)
            .Select(_ => SteamKitContentDeliveryClient.DownloadWithSharedCdnAuthAsync<int>(
                () => Volatile.Read(ref tokenState),
                state => Volatile.Write(ref tokenState, state),
                refreshGate,
                Download,
                () =>
                {
                    Interlocked.Increment(ref tokenRequests);
                    return Task.FromResult((EResult.OK, (string?)"new"));
                }))
            .ToArray();

        await allOldTokenAttemptsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseOldTokenAttempts.SetResult();
        int[] results = await Task.WhenAll(downloads);

        Assert.Equal(1, tokenRequests);
        SteamKitContentDeliveryClient.CdnAuthTokenState finalState = Volatile.Read(ref tokenState);
        Assert.Equal("new", finalState.Token);
        Assert.Equal(1, finalState.Generation);
        Assert.All(results, result => Assert.Equal(7, result));
        Assert.Equal(downloadCount, receivedTokens.Count(token => token == "old"));
        Assert.Equal(downloadCount, receivedTokens.Count(token => token == "new"));
    }

    [Fact]
    public async Task ConcurrentForbiddenDownloadsRefreshOnceWhenSteamReturnsSameToken()
    {
        const int downloadCount = 4;
        var allInitialAttemptsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInitialAttempts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = new int[downloadCount];
        var initialAttempts = 0;
        var tokenRequests = 0;
        var tokenState = new SteamKitContentDeliveryClient.CdnAuthTokenState("same", 0);
        using var refreshGate = new SemaphoreSlim(1, 1);

        Task<int>[] downloads = Enumerable.Range(0, downloadCount)
            .Select(worker => SteamKitContentDeliveryClient.DownloadWithSharedCdnAuthAsync(
                () => Volatile.Read(ref tokenState),
                state => Volatile.Write(ref tokenState, state),
                refreshGate,
                async _ =>
                {
                    if (Interlocked.Increment(ref attempts[worker]) == 1)
                    {
                        if (Interlocked.Increment(ref initialAttempts) == downloadCount)
                            allInitialAttemptsStarted.SetResult();
                        await releaseInitialAttempts.Task;
                        throw CreateWebRequestException(HttpStatusCode.Forbidden);
                    }

                    return 7;
                },
                () =>
                {
                    Interlocked.Increment(ref tokenRequests);
                    return Task.FromResult((EResult.OK, (string?)"same"));
                }))
            .ToArray();

        await allInitialAttemptsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseInitialAttempts.SetResult();
        int[] results = await Task.WhenAll(downloads);

        Assert.Equal(1, tokenRequests);
        Assert.All(results, result => Assert.Equal(7, result));
    }

    [Fact]
    public async Task ConcurrentForbiddenDownloadsShareDeniedTokenRefreshFailure()
    {
        const int downloadCount = 4;
        var allInitialAttemptsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInitialAttempts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initialAttempts = 0;
        var tokenRequests = 0;
        var tokenState = new SteamKitContentDeliveryClient.CdnAuthTokenState("old", 0);
        using var refreshGate = new SemaphoreSlim(1, 1);

        Task<int>[] downloads = Enumerable.Range(0, downloadCount)
            .Select(_ => SteamKitContentDeliveryClient.DownloadWithSharedCdnAuthAsync<int>(
                () => Volatile.Read(ref tokenState),
                state => Volatile.Write(ref tokenState, state),
                refreshGate,
                async _ =>
                {
                    if (Interlocked.Increment(ref initialAttempts) == downloadCount)
                        allInitialAttemptsStarted.SetResult();
                    await releaseInitialAttempts.Task;
                    throw CreateWebRequestException(HttpStatusCode.Forbidden);
                },
                () =>
                {
                    Interlocked.Increment(ref tokenRequests);
                    return Task.FromResult((EResult.AccessDenied, (string?)null));
                }))
            .ToArray();

        await allInitialAttemptsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseInitialAttempts.SetResult();
        Exception[] failures = await Task.WhenAll(downloads.Select(async download =>
        {
            return await Assert.ThrowsAsync<InvalidOperationException>(() => download);
        }));

        Assert.Equal(1, tokenRequests);
        Assert.All(failures, failure => Assert.Contains("AccessDenied", failure.Message));
    }

    [Fact]
    public async Task NonForbiddenFailureDoesNotRequestToken()
    {
        var tokenRequests = 0;

        SteamKitWebRequestException exception = await Assert.ThrowsAsync<SteamKitWebRequestException>(() =>
            SteamKitContentDeliveryClient.DownloadWithCdnAuthAsync<int>(
                null,
                _ => throw CreateWebRequestException(HttpStatusCode.InternalServerError),
                () =>
                {
                    tokenRequests++;
                    return Task.FromResult((EResult.OK, (string?)"unused"));
                }));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(0, tokenRequests);
    }

    [Fact]
    public async Task DeniedTokenRequestPreservesForbiddenFailure()
    {
        var tokenRequests = 0;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SteamKitContentDeliveryClient.DownloadWithCdnAuthAsync<int>(
                null,
                _ => throw CreateWebRequestException(HttpStatusCode.Forbidden),
                () =>
                {
                    tokenRequests++;
                    return Task.FromResult((EResult.Fail, (string?)null));
                }));

        Assert.Equal("Steam denied the CDN token request: Fail.", exception.Message);
        Assert.IsType<SteamKitWebRequestException>(exception.InnerException);
        Assert.Equal(1, tokenRequests);
    }

    [Fact]
    public async Task ForbiddenRetryDoesNotRequestSecondToken()
    {
        var downloadAttempts = 0;
        var tokenRequests = 0;

        await Assert.ThrowsAsync<SteamKitWebRequestException>(() =>
            SteamKitContentDeliveryClient.DownloadWithCdnAuthAsync<int>(
                null,
                _ =>
                {
                    downloadAttempts++;
                    throw CreateWebRequestException(HttpStatusCode.Forbidden);
                },
                () =>
                {
                    tokenRequests++;
                    return Task.FromResult((EResult.OK, (string?)"granted"));
                }));

        Assert.Equal(2, downloadAttempts);
        Assert.Equal(1, tokenRequests);
    }

    private static SteamKitWebRequestException CreateWebRequestException(HttpStatusCode statusCode) =>
        new(statusCode.ToString(), new HttpResponseMessage(statusCode));

}
