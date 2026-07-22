using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Downloads;

public sealed record PresetShareCreateResult(string Code, string DeleteToken);

public sealed class PresetShareClient
{
    private readonly HttpClient httpClient;
    private readonly INetworkPolicy networkPolicy;
    private readonly Uri baseUri;

    public PresetShareClient(HttpClient httpClient, INetworkPolicy networkPolicy, Uri baseUri)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.networkPolicy = networkPolicy ?? throw new ArgumentNullException(nameof(networkPolicy));
        this.baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        if (!baseUri.IsAbsoluteUri || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Preset sharing requires an absolute HTTPS service URL.", nameof(baseUri));
        }
    }

    public async Task<PresetShareCreateResult> CreateAsync(
        ModPreset preset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        using var linked = LinkedCancellation(cancellationToken);
        using var response = await httpClient.PostAsJsonAsync(
            new Uri(baseUri, "api/presets"),
            preset,
            CrystalflyJson.Options,
            linked.Token);
        var payload = await ReadSuccessAsync<CreateResponse>(response, linked.Token);
        if (!IsValidCode(payload.Code) || string.IsNullOrWhiteSpace(payload.DeleteToken))
        {
            throw new InvalidDataException("Preset sharing service returned an invalid creation response.");
        }
        return new PresetShareCreateResult(payload.Code, payload.DeleteToken);
    }

    public async Task<ModPreset> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureCode(code);
        using var linked = LinkedCancellation(cancellationToken);
        using var response = await httpClient.GetAsync(
            new Uri(baseUri, $"api/presets/{code}"),
            HttpCompletionOption.ResponseHeadersRead,
            linked.Token);
        var payload = await ReadSuccessAsync<GetResponse>(response, linked.Token);
        if (!string.Equals(payload.Code, code, StringComparison.Ordinal)
            || payload.Preset.SchemaVersion != ModPreset.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Preset sharing service returned a mismatched preset.");
        }
        return payload.Preset;
    }

    public async Task DeleteAsync(
        string code,
        string deleteToken,
        CancellationToken cancellationToken = default)
    {
        EnsureCode(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(deleteToken);
        using var linked = LinkedCancellation(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(baseUri, $"api/presets/{code}"));
        request.Headers.TryAddWithoutValidation("X-Delete-Token", deleteToken);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            linked.Token);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return;
        }
        await ThrowResponseAsync(response, linked.Token);
    }

    private CancellationTokenSource LinkedCancellation(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            networkPolicy.GetOnlineCancellationToken());

    private static async Task<T> ReadSuccessAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowResponseAsync(response, cancellationToken);
        }
        return await response.Content.ReadFromJsonAsync<T>(CrystalflyJson.Options, cancellationToken)
            ?? throw new InvalidDataException("Preset sharing service returned an empty response.");
    }

    private static async Task ThrowResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? reason = null;
        try
        {
            reason = JsonSerializer.Deserialize<ErrorResponse>(body, CrystalflyJson.Options)?.Error;
        }
        catch (JsonException)
        {
        }
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(reason)
                ? $"Preset sharing request failed with HTTP {(int)response.StatusCode}."
                : reason,
            null,
            response.StatusCode);
    }

    private static void EnsureCode(string code)
    {
        if (!IsValidCode(code))
        {
            throw new ArgumentException("Preset share code must contain 12 URL-safe characters.", nameof(code));
        }
    }

    private static bool IsValidCode(string? code) =>
        code is { Length: 12 }
        && code.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private sealed record CreateResponse(string Code, string DeleteToken);

    private sealed record GetResponse(string Code, ModPreset Preset);

    private sealed record ErrorResponse(string Error);
}
