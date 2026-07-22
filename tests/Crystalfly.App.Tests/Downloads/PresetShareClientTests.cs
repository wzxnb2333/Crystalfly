using System.Net;
using System.Text;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;

namespace Crystalfly.App.Tests.Downloads;

public sealed class PresetShareClientTests
{
    [Fact]
    public async Task Create_fetch_and_delete_use_the_versioned_portable_preset_contract()
    {
        var requests = new List<HttpRequestMessage>();
        using var client = new HttpClient(new Handler(async request =>
        {
            requests.Add(await CloneAsync(request));
            return request.Method.Method switch
            {
                "POST" => Json(HttpStatusCode.Created,
                    "{\"code\":\"A1B2C3D4E5F6\",\"deleteToken\":\"token\"}"),
                "GET" => Json(HttpStatusCode.OK,
                    "{\"code\":\"A1B2C3D4E5F6\",\"preset\":{\"schemaVersion\":1,\"id\":\"shared\",\"name\":\"Shared\",\"gameBuildId\":\"build\",\"loaderId\":\"modding-api-77\",\"applyMode\":\"append\",\"entries\":[]}}"),
                "DELETE" => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => throw new InvalidOperationException()
            };
        }));
        var share = new PresetShareClient(
            client,
            new NetworkPolicy(),
            new Uri("https://share.example.test/"));
        var preset = Preset();

        var created = await share.CreateAsync(preset);
        var fetched = await share.GetAsync(created.Code);
        await share.DeleteAsync(created.Code, created.DeleteToken);

        Assert.Equal("A1B2C3D4E5F6", created.Code);
        Assert.Equal("Shared", fetched.Name);
        Assert.Contains("\"gameBuildId\": \"build\"", await requests[0].Content!.ReadAsStringAsync());
        Assert.Equal("token", requests[2].Headers.GetValues("X-Delete-Token").Single());
    }

    [Fact]
    public async Task Offline_mode_blocks_share_requests_before_http_send()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            calls++;
            return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
        }));
        var share = new PresetShareClient(
            client,
            new NetworkPolicy(isOffline: true),
            new Uri("https://share.example.test/"));

        await Assert.ThrowsAsync<OfflineModeException>(() => share.CreateAsync(Preset()));
        Assert.Equal(0, calls);
    }

    private static ModPreset Preset() => new()
    {
        Id = "preset",
        Name = "Preset",
        GameBuildId = "build",
        LoaderId = "modding-api-77",
        ApplyMode = ModPresetApplyMode.Append
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (request.Content is not null)
        {
            clone.Content = new StringContent(await request.Content.ReadAsStringAsync());
        }
        return clone;
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
