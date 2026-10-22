using System.Globalization;
using System.Net;
using Crystalfly.Core.Diagnostics;
using Sentry;
using Sentry.Extensibility;
using Sentry.Http;
using Sentry.Protocol.Envelopes;

namespace Crystalfly.App.Diagnostics;

// The SDK owns caching. This layer enforces the application network switch and backoff.
internal sealed class ReportingTransport(SentryOptions options, ReportingNetworkGate gate,
    HttpClient client, Func<bool> cacheIsSafe) : HttpTransportBase(options), ITransport
{
    private int failures;

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        await gate.WaitForNetworkOnlineAsync(cancellationToken).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, gate.RequestCancellation);
        linked.Token.ThrowIfCancellationRequested();
        if (!gate.Online || !cacheIsSafe()) throw new OperationCanceledException(linked.Token);
        using var buffer = new MemoryStream();
        await envelope.SerializeAsync(buffer, null, linked.Token).ConfigureAwait(false);
        if (buffer.Length > ErrorReport.MaxBytes || !ReportingWirePolicy.IsAllowed(buffer.ToArray())) return;
        using var processed = ProcessEnvelope(envelope);
        if (processed.Items.Count == 0) return;
        try
        {
            using var request = CreateRequest(processed);
            using var response = await client.SendAsync(request, linked.Token).ConfigureAwait(false);
            if (response.Headers.TryGetValues("X-Sentry-Rate-Limits", out var quotas))
            {
                foreach (var quota in string.Join(',', quotas).Split(','))
                {
                    if (double.TryParse(quota.Split(':')[0], CultureInfo.InvariantCulture, out var seconds)
                        && double.IsFinite(seconds) && seconds > 0)
                        gate.Defer(TimeSpan.FromSeconds(Math.Min(seconds, 7 * 24 * 3600)));
                }
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
                gate.Defer(delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1));
                throw new HttpRequestException("Error reporting is rate limited.");
            }
            if ((int)response.StatusCode >= 500) throw new HttpRequestException("Error reporting service unavailable.");
            await HandleResponseAsync(response, processed, linked.Token).ConfigureAwait(false);
            failures = 0;
        }
        catch (OperationCanceledException) when (!linked.IsCancellationRequested)
        {
            DeferRetry();
            throw new HttpRequestException("Error reporting timed out.");
        }
        catch (HttpRequestException)
        {
            DeferRetry();
            throw;
        }
    }

    private void DeferRetry() => gate.Defer(TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Min(failures++, 6)))));
}
