using System.Net;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Json.Schema;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class ModActivitySourceTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-mod-activity-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_returns_remote_catalog_and_updates_cache()
    {
        var remote = Catalog("remote000", "Remote", "2026-07-21T00:00:00Z");
        using var client = Client((_, _) => Task.FromResult(JsonResponse(remote)));
        var cachePath = Path.Combine(directory, "activity.json");

        var result = await ModActivitySource.LoadAsync(
            client,
            cachePath,
            Catalog("embedded0", "Embedded", "2026-07-20T00:00:00Z"),
            RemoteUri,
            CancellationToken.None);

        Assert.Equal(ModActivityLoadStatus.Remote, result.Status);
        AssertCatalogEqual(remote, result.Catalog);
        Assert.Null(result.Reason);
        AssertCatalogEqual(remote, await AtomicJsonStore.ReadAsync<ModActivityCatalog>(cachePath));
    }

    [Fact]
    public void Generated_catalog_matches_schema_and_embedded_fallback()
    {
        string root = FindRepositoryRoot();
        string catalogPath = Path.Combine(root, "catalog", "mod-activity.v1.json");
        string schemaPath = Path.Combine(root, "catalog", "mod-activity.v1.schema.json");
        var schema = JsonSchema.FromFile(schemaPath);

        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        EvaluationResults validation = schema.Evaluate(document.RootElement);
        var catalog = EmbeddedModActivityCatalog.Load();

        Assert.True(validation.IsValid, validation.ToString());
        Assert.InRange(catalog.Entries.Count, 640, 660);
        Assert.Equal(
            catalog.Entries.Select(entry => entry.Id).Order(StringComparer.Ordinal),
            catalog.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task LoadAsync_returns_valid_cache_when_remote_fails()
    {
        var cached = Catalog("cached00", "Cached", "2026-07-20T00:00:00Z");
        var cachePath = Path.Combine(directory, "activity.json");
        await AtomicJsonStore.WriteAsync(cachePath, cached);
        using var client = Client((_, _) => throw new HttpRequestException("offline"));

        var result = await ModActivitySource.LoadAsync(
            client,
            cachePath,
            Catalog("embedded0", "Embedded", "2026-07-19T00:00:00Z"),
            RemoteUri,
            CancellationToken.None);

        Assert.Equal(ModActivityLoadStatus.Cached, result.Status);
        AssertCatalogEqual(cached, result.Catalog);
        Assert.Contains("offline", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_returns_embedded_catalog_when_remote_and_cache_fail()
    {
        var embedded = Catalog("embedded0", "Embedded", "2026-07-19T00:00:00Z");
        using var client = Client((_, _) => throw new HttpRequestException("offline"));

        var result = await ModActivitySource.LoadAsync(
            client,
            Path.Combine(directory, "activity.json"),
            embedded,
            RemoteUri,
            CancellationToken.None);

        Assert.Equal(ModActivityLoadStatus.Embedded, result.Status);
        AssertCatalogEqual(embedded, result.Catalog);
        Assert.Contains("offline", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_rejects_schema_invalid_remote_without_overwriting_cache()
    {
        var cached = Catalog("cached00", "Cached", "2026-07-20T00:00:00Z");
        var cachePath = Path.Combine(directory, "activity.json");
        await AtomicJsonStore.WriteAsync(cachePath, cached);
        byte[] before = await File.ReadAllBytesAsync(cachePath);
        var invalidRemote = Catalog("bad", "Remote", "2026-07-21T00:00:00Z");
        using var client = Client((_, _) => Task.FromResult(JsonResponse(invalidRemote)));

        var result = await ModActivitySource.LoadAsync(
            client,
            cachePath,
            Catalog("embedded0", "Embedded", "2026-07-19T00:00:00Z"),
            RemoteUri,
            CancellationToken.None);

        Assert.Equal(ModActivityLoadStatus.Cached, result.Status);
        AssertCatalogEqual(cached, result.Catalog);
        Assert.Equal(before, await File.ReadAllBytesAsync(cachePath));
    }

    [Fact]
    public void Validate_rejects_entry_id_longer_than_schema_limit()
    {
        var catalog = Catalog("remote000", new string('a', 251), "2026-07-21T00:00:00Z");

        Assert.Throws<InvalidDataException>(() => ModActivitySource.Validate(catalog));
    }

    [Fact]
    public async Task LoadAsync_propagates_caller_cancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = Client(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(Catalog("remote000", "Remote", "2026-07-21T00:00:00Z"));
        });
        using var cancellation = new CancellationTokenSource();

        Task<ModActivityLoadResult> load = ModActivitySource.LoadAsync(
            client,
            Path.Combine(directory, "activity.json"),
            Catalog("embedded0", "Embedded", "2026-07-19T00:00:00Z"),
            RemoteUri,
            cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Uri RemoteUri { get; } = new("https://example.invalid/mod-activity.v1.json");

    private static ModActivityCatalog Catalog(string sourceRevision, string idSuffix, string generatedAt) => new()
    {
        GeneratedAt = DateTimeOffset.Parse(generatedAt),
        SourceRevision = sourceRevision,
        Entries =
        [
            new ModActivityEntry
            {
                Id = $"hkmod:{idSuffix}",
                AddedAt = DateTimeOffset.Parse("2026-07-18T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-07-18T00:00:00Z")
            }
        ]
    };

    private static HttpClient Client(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        new(new StubHandler(responseFactory));

    private static void AssertCatalogEqual(ModActivityCatalog expected, ModActivityCatalog actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.GeneratedAt, actual.GeneratedAt);
        Assert.Equal(expected.SourceRevision, actual.SourceRevision);
        Assert.Equal(expected.Entries, actual.Entries);
    }

    private static HttpResponseMessage JsonResponse(ModActivityCatalog catalog) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            CrystalflyJson.Serialize(catalog),
            Encoding.UTF8,
            "application/json")
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request, cancellationToken);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Crystalfly repository root was not found.");
    }
}
