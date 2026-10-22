using System.Net;

namespace Crystalfly.Core.Thunderstore;

/// <summary>Redirects are checked before a request can leave the approved HTTPS origins.</summary>
public sealed class ThunderstoreHttpHandler(HttpMessageHandler transport) : DelegatingHandler(transport)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var uri = ThunderstoreCatalogSource.TrustedUri(request.RequestUri?.AbsoluteUri ?? "");
        for (var redirect = 0; redirect <= 5; redirect++)
        {
            using var next = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await base.SendAsync(next, token);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null) throw new HttpRequestException("Missing Thunderstore redirect location.");
            uri = ThunderstoreCatalogSource.TrustedUri(new Uri(uri, location).AbsoluteUri);
        }
        throw new HttpRequestException("Too many Thunderstore redirects.");
    }
}
