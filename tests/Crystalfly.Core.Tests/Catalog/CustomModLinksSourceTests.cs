using System.Net;
using System.Text;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class CustomModLinksSourceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Load_replaces_source_identity_and_binds_every_mod_to_exact_build_and_loader()
    {
        using var client = Client(_ => Response(ValidXml()));
        var definition = Definition();

        var result = await CustomModLinksSource.LoadAsync(
            definition,
            client,
            Path.Combine(root, "custom-modlinks.json"));

        Assert.Equal(CustomModLinksLoadStatus.Remote, result.Status);
        Assert.False(result.IsVerified);
        var mod = Assert.Single(result.Catalog.Mods);
        Assert.Equal("Custom ModLinks", mod.SourceName);
        Assert.Equal(definition.BuildId, Assert.Single(mod.SupportedBuildIds));
        Assert.Equal(definition.LoaderId, mod.LoaderId);
        Assert.Equal(new string('A', 64), mod.Sha256);
    }

    [Fact]
    public async Task Load_uses_only_identity_matching_custom_cache_after_remote_failure()
    {
        var cachePath = Path.Combine(root, "custom-modlinks.json");
        using (var client = Client(_ => Response(ValidXml())))
        {
            _ = await CustomModLinksSource.LoadAsync(Definition(), client, cachePath);
        }
        using var offline = Client(_ => throw new HttpRequestException("offline"));

        var cached = await CustomModLinksSource.LoadAsync(Definition(), offline, cachePath);
        Assert.Equal(CustomModLinksLoadStatus.Cached, cached.Status);

        await Assert.ThrowsAsync<InvalidDataException>(() => CustomModLinksSource.LoadAsync(
            Definition() with { BuildId = "other-build" },
            offline,
            cachePath));
    }

    [Fact]
    public async Task Load_ignores_structurally_incomplete_cache_before_using_remote_source()
    {
        var cachePath = Path.Combine(root, "incomplete.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(cachePath, "{\"definition\":null,\"catalog\":null}");
        using var client = Client(_ => Response(ValidXml()));

        var result = await CustomModLinksSource.LoadAsync(Definition(), client, cachePath);

        Assert.Equal(CustomModLinksLoadStatus.Remote, result.Status);
        Assert.Single(result.Catalog.Mods);
    }

    [Fact]
    public async Task Load_rejects_insecure_url_invalid_hash_and_real_cancellation()
    {
        using var client = Client(_ => Response(ValidXml()));
        await Assert.ThrowsAsync<ArgumentException>(() => CustomModLinksSource.LoadAsync(
            Definition() with { Url = "http://example.test/ModLinks.xml" },
            client,
            Path.Combine(root, "http.json")));

        using var invalid = Client(_ => Response(ValidXml(hash: "bad")));
        await Assert.ThrowsAsync<InvalidDataException>(() => CustomModLinksSource.LoadAsync(
            Definition(),
            invalid,
            Path.Combine(root, "invalid.json")));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CustomModLinksSource.LoadAsync(
            Definition(),
            client,
            Path.Combine(root, "cancel.json"),
            cancellation.Token));
    }

    [Fact]
    public async Task Load_rejects_xml_with_a_document_type_declaration()
    {
        var xml = $$"""
            <!DOCTYPE ModLinks [<!ENTITY name "Example">]>
            <ModLinks>
              <Manifest>
                <Name>&name;</Name>
                <Version>1.0.0</Version>
                <Links><Windows SHA256="{{new string('A', 64)}}">https://example.test/mod.zip</Windows></Links>
              </Manifest>
            </ModLinks>
            """;
        using var client = Client(_ => Response(xml));

        await Assert.ThrowsAsync<InvalidDataException>(() => CustomModLinksSource.LoadAsync(
            Definition(),
            client,
            Path.Combine(root, "dtd.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CustomModLinksDefinition Definition() => new()
    {
        Url = "https://example.test/ModLinks.xml",
        BuildId = "1.5.78.11833",
        LoaderId = "modding-api-77"
    };

    private static string ValidXml(string? hash = null) => $$"""
        <ModLinks>
          <Manifest>
            <Name>Example</Name>
            <DisplayName>Example Mod</DisplayName>
            <Version>1.0.0</Version>
            <Links><Windows SHA256="{{hash ?? new string('A', 64)}}">https://example.test/mod.zip</Windows></Links>
          </Manifest>
        </ModLinks>
        """;

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new StubHandler(response));

    private static HttpResponseMessage Response(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/xml")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }
}
