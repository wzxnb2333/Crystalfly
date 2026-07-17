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
