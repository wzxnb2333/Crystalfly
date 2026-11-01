using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Thunderstore;

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

    public Task<PresetShareCreateResult> CreateAsync(ModPreset preset, CancellationToken cancellationToken = default) =>
        CreateDocumentAsync(preset, cancellationToken);

    public Task<PresetShareCreateResult> CreateAsync(ThunderstorePreset preset, CancellationToken cancellationToken = default) =>
        CreateDocumentAsync(preset, cancellationToken);

    private async Task<PresetShareCreateResult> CreateDocumentAsync<T>(T preset, CancellationToken cancellationToken)
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
        string input,
        CancellationToken cancellationToken = default)
    {
        var preset = await GetDocumentAsync<ModPreset>(input, cancellationToken);
        if (preset.GameId != GameId.HollowKnight || preset.SchemaVersion != ModPreset.CurrentSchemaVersion)
            throw new InvalidDataException("This Mod Pack does not belong to Hollow Knight.");
        return preset;
    }

    public async Task<ThunderstorePreset> GetSilksongAsync(string input, CancellationToken cancellationToken = default)
    {
        var preset = await GetDocumentAsync<ThunderstorePreset>(input, cancellationToken);
        if (preset.GameId != GameId.Silksong || preset.Packages is null)
            throw new InvalidDataException("This Mod Pack does not belong to Silksong.");
        return preset;
    }

    internal string ParseShareCode(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var value = input.Trim();
        if (IsValidCode(value)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && value.Length <= ProtocolCommandParser.MaxUriLength)
        {
            if (uri.Scheme == "crystalfly")
            {
                try
                {
                    var command = ProtocolCommandParser.Parse(value);
                    if (command.Kind == ProtocolCommandKind.ImportPresetShare) return command.ShareCode!;
                }
                catch (ProtocolCommandException) { }
            }
            if (uri.Scheme == Uri.UriSchemeHttps && uri.IdnHost == baseUri.IdnHost && uri.Port == baseUri.Port
                && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0)
            {
                var segments = uri.AbsolutePath.Split('/');
                if (segments.Length == 3 && segments[1] == "share" && IsValidCode(segments[2])) return segments[2];
            }
        }
        throw new ArgumentException("Enter a 12-character share code or a Crystalfly Mod Pack link.", nameof(input));
    }

    private async Task<T> GetDocumentAsync<T>(string input, CancellationToken cancellationToken)
    {
        var code = ParseShareCode(input);
        using var linked = LinkedCancellation(cancellationToken);
        using var response = await httpClient.GetAsync(
            new Uri(baseUri, $"api/presets/{code}"),
            HttpCompletionOption.ResponseHeadersRead,
            linked.Token);
        var payload = await ReadSuccessAsync<GetResponse>(response, linked.Token);
        if (!string.Equals(payload.Code, code, StringComparison.Ordinal)
            || payload.Preset.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Preset sharing service returned a mismatched preset.");
        }
        var expectedGame = typeof(T) == typeof(ThunderstorePreset) ? "silksong" : "hollowKnight";
        var actualGame = payload.Preset.TryGetProperty("gameId", out var game)
            ? game.ValueKind == JsonValueKind.String ? game.GetString() : null : "hollowKnight";
        if (actualGame != expectedGame)
            throw new InvalidDataException("The Mod Pack belongs to a different game. Select its game workspace and try again.");
        try
        {
            return payload.Preset.Deserialize<T>(CrystalflyJson.Options)
                ?? throw new InvalidDataException("Preset sharing service returned an empty preset.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Preset sharing service returned an invalid preset.", exception);
        }
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
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(CrystalflyJson.Options, cancellationToken)
                ?? throw new InvalidDataException("Preset sharing service returned an empty response.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Preset sharing service returned an invalid response.", exception);
        }
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

    private sealed record GetResponse(string Code, JsonElement Preset);

    private sealed record ErrorResponse(string Error);
}
