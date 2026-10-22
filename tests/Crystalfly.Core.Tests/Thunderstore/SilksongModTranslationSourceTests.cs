using System.Net;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.Core.Tests.Thunderstore;

public sealed class SilksongModTranslationSourceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Silksong.Translations", Guid.NewGuid().ToString("N"));
    internal static SilksongModTranslationCatalog Baseline(string original = "# Test\n", string chinese = "# 测试\n") => new()
    {
        UpdatedAt = DateTimeOffset.Parse("2026-10-05T00:00:00Z"),
        CategoryNames = new Dictionary<string, string> { ["Utility"] = "实用工具" },
        Mods = [new() { FullName = "Author-Test", DisplayName = "中文测试模组", SourceUrl = "https://thunderstore.io/c/hollow-knight-silksong/p/Author/Test/",
            Versions = [new() { Version = "1.0.0", Description = "测试功能说明", DescriptionSha256 = SilksongModTranslationSource.Hash("Original description"),
                Documents = new Dictionary<string, SilksongModDocumentTranslation> { ["README.md"] = new()
                { SourceSha256 = SilksongModTranslationSource.Hash(original), TranslationSha256 = SilksongModTranslationSource.Hash(chinese) },
                ["CHANGELOG.md"] = new() { Missing = true } } }] }]
    };

    [Fact]
    public async Task Offline_documents_use_verified_cache_without_requests_and_corrupt_translation_falls_back_to_original()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(_ => { calls++; throw new InvalidOperationException("Offline request"); }));
        var service = new SilksongModTranslationSource(client, root, baseline: Baseline());
        var directory = Path.Combine(root, "documents", "Author-Test", "1.0.0");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "README.en.md"), "# Test\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "README.zh-CN.md"), "# 测试\n");
        await service.RefreshAsync(offline: true);
        var document = await service.ReadDocumentAsync("Author-Test", "1.0.0", "README.md", offline: true);
        Assert.Equal("# 测试\n", document?.Chinese);
        await File.WriteAllTextAsync(Path.Combine(directory, "README.zh-CN.md"), "Changed cache");
        document = await service.ReadDocumentAsync("Author-Test", "1.0.0", "README.md", offline: true);
        Assert.Equal("# Test\n", document?.Original);
        Assert.Null(document?.Chinese);
        Assert.True((await service.ReadDocumentAsync("Author-Test", "1.0.0", "CHANGELOG.md", offline: true))?.Missing);
        Assert.Null(await service.ReadDocumentAsync("Author-Test", "0.9.0", "README.md", offline: true));
        Assert.Null(await service.ReadDocumentAsync("Other-Test", "1.0.0", "README.md", offline: true));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Invalid_remote_index_preserves_baseline_and_does_not_write_cache()
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent("{\"schemaVersion\":99,\"language\":\"zh-CN\"}") }));
        var service = new SilksongModTranslationSource(client, root, baseline: Baseline());
        await service.RefreshAsync(offline: false);
        Assert.Equal("中文测试模组", service.Find("Author-Test")?.DisplayName);
        Assert.False(File.Exists(Path.Combine(root, "index.json")));
    }

    [Fact]
    public async Task Downloaded_document_requires_matching_hash_before_it_is_cached()
    {
        using var client = new HttpClient(new Handler(request => new(HttpStatusCode.OK)
        { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.RequestUri!.AbsolutePath.EndsWith(".en.md", StringComparison.Ordinal) ? "# Test\n" : "Wrong Chinese text")) }));
        var service = new SilksongModTranslationSource(client, root, baseline: Baseline());
        var document = await service.ReadDocumentAsync("Author-Test", "1.0.0", "README.md", offline: false);
        Assert.Equal("# Test\n", document?.Original);
        Assert.Null(document?.Chinese);
        Assert.True(File.Exists(Path.Combine(root, "documents", "Author-Test", "1.0.0", "README.en.md")));
        Assert.False(File.Exists(Path.Combine(root, "documents", "Author-Test", "1.0.0", "README.zh-CN.md")));
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed_and_document_names_cannot_escape_cache()
    {
        var service = new SilksongModTranslationSource(cacheRoot: root, baseline: Baseline());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RefreshAsync(true, new CancellationToken(true)));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadDocumentAsync("Author-Test", "1.0.0", "../../README.md", true));
        Assert.Throws<InvalidDataException>(() => SilksongModTranslationSource.Validate(Baseline() with
        { Mods = [Baseline().Mods[0] with { FullName = "../../Test" }] }));
    }

    [Fact]
    public async Task Embedded_document_is_available_without_installing_or_downloading_a_package()
    {
        var service = new SilksongModTranslationSource();
        var entry = service.Catalog.Mods.SelectMany(mod => mod.Versions.Select(v => (mod, version: v)))
            .First(pair => pair.version.Documents.TryGetValue("README.md", out var document) && document.TranslationSha256 is not null);
        var result = await service.ReadDocumentAsync(entry.mod.FullName, entry.version.Version, "README.md", offline: true);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Original);
        Assert.NotNull(result.Chinese);
    }

    [Fact]
    public async Task Valid_online_updates_preserve_previous_versions_and_survive_offline_restart()
    {
        var baseline = Baseline();
        var update = baseline with { UpdatedAt = baseline.UpdatedAt.AddDays(1),
            Mods = [baseline.Mods[0] with { DisplayName = "更新译名", Versions = [baseline.Mods[0].Versions[0] with { Version = "2.0.0" }] }] };
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(update, CrystalflyJson.Options)) }));
        var source = new SilksongModTranslationSource(client, root, baseline: baseline);
        await source.RefreshAsync(false);
        Assert.Equal("更新译名", source.Find("Author-Test")!.DisplayName);
        Assert.Equal(2, source.Find("Author-Test")!.Versions.Count);
        var restarted = new SilksongModTranslationSource(cacheRoot: root, baseline: baseline);
        await restarted.RefreshAsync(true);
        Assert.Equal("更新译名", restarted.Find("Author-Test")!.DisplayName);
        Assert.Equal(2, restarted.Find("Author-Test")!.Versions.Count);
        await File.WriteAllTextAsync(Path.Combine(root, "index.json"), "{broken cache");
        await restarted.RefreshAsync(true);
        Assert.Equal("更新译名", restarted.Find("Author-Test")!.DisplayName);
    }

    [Fact]
    public async Task Older_cache_cannot_override_newer_embedded_translations()
    {
        var baseline = Baseline();
        Directory.CreateDirectory(root);
        await AtomicJsonStore.WriteAsync(Path.Combine(root, "index.json"), baseline with
        { UpdatedAt = baseline.UpdatedAt.AddDays(-1), Mods = [baseline.Mods[0] with { DisplayName = "旧译名" }] });
        var source = new SilksongModTranslationSource(cacheRoot: root, baseline: baseline);
        await source.RefreshAsync(true);
        Assert.Equal("中文测试模组", source.Find("Author-Test")!.DisplayName);
    }

    [Fact]
    public async Task Broken_document_update_keeps_a_verified_baseline_translation_of_the_same_original()
    {
        var baseline = Baseline();
        var version = baseline.Mods[0].Versions[0];
        var update = baseline with { UpdatedAt = baseline.UpdatedAt.AddDays(1), Mods = [baseline.Mods[0] with
        { Versions = [version with { Documents = new Dictionary<string, SilksongModDocumentTranslation>
        { ["README.md"] = version.Documents["README.md"] with { TranslationSha256 = new string('A', 64) } } }] }] };
        var directory = Path.Combine(root, "documents", "Author-Test", "1.0.0");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "README.en.md"), "# Test\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "README.zh-CN.md"), "# 测试\n");
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(update, CrystalflyJson.Options)) }));
        var source = new SilksongModTranslationSource(client, root, baseline: baseline);
        await source.RefreshAsync(false);
        var document = await source.ReadDocumentAsync("Author-Test", "1.0.0", "README.md", true);
        Assert.Equal("# 测试\n", document?.Chinese);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
