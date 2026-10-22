using System.Net;
using Avalonia.Headless.XUnit;
using Crystalfly.App.Services;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.Services;

public sealed class ThunderstorePackageIconLoaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.IconLoader", Guid.NewGuid().ToString("N"));
    private const string Png256 = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAADIElEQVR4nO3UIQEAIBDAQDKR6TNRF2IgduL81Naec4Gm9TsA+McAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAIMwAYLoegPsghvl8Uw8AAAAASUVORK5CYII=";

    [AvaloniaFact]
    public async Task Valid_icon_is_cached_and_remains_available_offline()
    {
        var requests = 0;
        using var client = new HttpClient(new Handler(() =>
        {
            requests++;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Convert.FromBase64String(Png256)) };
        }));
        var offline = false;
        var loader = new ThunderstorePackageIconLoader(client, root, () => offline);
        var first = await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png");
        Assert.NotNull(first);
        Assert.Equal((256, 256), (first!.PixelSize.Width, first.PixelSize.Height));
        first.Dispose();

        offline = true;
        var cached = await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png");
        Assert.NotNull(cached);
        Assert.Equal((256, 256), (cached!.PixelSize.Width, cached.PixelSize.Height));
        cached.Dispose();
        Assert.Equal(1, requests);
    }

    [AvaloniaFact]
    public async Task Oversized_cache_is_ignored_offline_without_a_network_request()
    {
        var requests = 0;
        using var client = new HttpClient(new Handler(() =>
        {
            requests++;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Convert.FromBase64String(Png256)) };
        }));
        var offline = false;
        var loader = new ThunderstorePackageIconLoader(client, root, () => offline);
        using (var first = await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png")) Assert.NotNull(first);
        var cache = Assert.Single(Directory.GetFiles(root));
        await File.WriteAllBytesAsync(cache, new byte[1024 * 1024 + 1]);

        offline = true;
        Assert.Null(await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png"));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Offline_mode_does_not_request_and_invalid_png_falls_back()
    {
        var requests = 0;
        using var client = new HttpClient(new Handler(() =>
        {
            requests++;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent("broken"u8.ToArray()) };
        }));
        var offline = true;
        var loader = new ThunderstorePackageIconLoader(client, root, () => offline);
        Assert.Null(await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png"));
        Assert.Equal(0, requests);

        offline = false;
        Assert.Null(await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png"));
        Assert.Equal(1, requests);
        Assert.Null(await loader.LoadAsync("https://example.com/icon.png"));
        Assert.Equal(1, requests);
    }

    [Theory]
    [InlineData("http://ccdn.thunderstore.io/icon.png")]
    [InlineData("https://example.com/icon.png")]
    [InlineData("https://thunderstore.io.evil.example/icon.png")]
    public async Task Non_thunderstore_or_non_https_icon_is_rejected_without_request(string url)
    {
        var requests = 0;
        using var client = new HttpClient(new Handler(() =>
        {
            requests++;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent("broken"u8.ToArray()) };
        }));
        var loader = new ThunderstorePackageIconLoader(client, root, () => false);
        Assert.Null(await loader.LoadAsync(url));
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task Oversized_chunked_icon_is_rejected_without_content_length()
    {
        var requests = 0;
        using var client = new HttpClient(new Handler(() =>
        {
            requests++;
            return new(HttpStatusCode.OK) { Content = new UnknownLengthContent(new byte[1024 * 1024 + 1]) };
        }));
        var loader = new ThunderstorePackageIconLoader(client, root, () => false);
        Assert.Null(await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png"));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Redirect_to_external_origin_is_rejected_before_second_request()
    {
        var requests = 0;
        using var client = new HttpClient(new ThunderstoreHttpHandler(new Handler(() =>
        {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://example.com/icon.png");
            return response;
        })));
        var loader = new ThunderstorePackageIconLoader(client, root, () => false);
        Assert.Null(await loader.LoadAsync("https://ccdn.thunderstore.io/icon.png"));
        Assert.Equal(1, requests);
    }

    private sealed class Handler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response());
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
