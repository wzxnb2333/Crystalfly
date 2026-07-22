using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public sealed class ModContentSource(HttpClient httpClient, string cacheRoot)
{
    private readonly HttpClient httpClient = httpClient;
    private readonly string cacheRoot = Path.GetFullPath(cacheRoot);

    public async Task<ModContentLoadResult> LoadAsync(
        ModManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!TryParseGitHubRepository(manifest.RepositoryUrl, out var owner, out var repository))
        {
            return new ModContentLoadResult(
                ModContentLoadStatus.Unavailable,
                null,
                "A verified GitHub repository URL is required.");
        }

        var repositoryUrl = $"https://github.com/{owner}/{repository}";
        var cachePath = CachePath(repositoryUrl);
        ModContentDocument? cached = await ReadCacheAsync(cachePath, repositoryUrl, cancellationToken);
        try
        {
            var readme = await FetchTextAsync(
                new Uri($"https://api.github.com/repos/{owner}/{repository}/readme"),
                cached?.ReadmeETag,
                "application/vnd.github.raw+json",
                static content => content,
                cancellationToken);
            var release = await FetchTextAsync(
                new Uri($"https://api.github.com/repos/{owner}/{repository}/releases/latest"),
                cached?.ReleaseETag,
                "application/vnd.github+json",
                ParseReleaseBody,
                cancellationToken);
            var document = new ModContentDocument
            {
                RepositoryUrl = repositoryUrl,
                ReadmeMarkdown = readme.NotModified
                    ? cached?.ReadmeMarkdown
                    : SanitizeNullable(readme.Content),
                ReadmeETag = readme.NotModified ? cached?.ReadmeETag : readme.ETag,
                ReleaseNotesMarkdown = release.NotModified
                    ? cached?.ReleaseNotesMarkdown
                    : SanitizeNullable(release.Content),
                ReleaseETag = release.NotModified ? cached?.ReleaseETag : release.ETag,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await AtomicJsonStore.WriteAsync(cachePath, document, cancellationToken);
            return new ModContentLoadResult(ModContentLoadStatus.Remote, document, null);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            return cached is null
                ? new ModContentLoadResult(
                    ModContentLoadStatus.Unavailable,
                    null,
                    exception.Message)
                : new ModContentLoadResult(
                    ModContentLoadStatus.Cached,
                    cached,
                    exception.Message);
        }
    }

    private async Task<FetchResult> FetchTextAsync(
        Uri uri,
        string? etag,
        string accept,
        Func<string, string?> project,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Crystalfly/0.4");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new FetchResult(null, etag, true);
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new FetchResult(null, null, false);
        }
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return new FetchResult(
            project(content),
            response.Headers.ETag?.ToString(),
            false);
    }

    private async Task<ModContentDocument?> ReadCacheAsync(
        string cachePath,
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var cached = await AtomicJsonStore.ReadAsync<ModContentDocument>(
                cachePath,
                cancellationToken);
            if (cached.SchemaVersion != ModContentDocument.CurrentSchemaVersion
                || !string.Equals(cached.RepositoryUrl, repositoryUrl, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Mod content cache identity is invalid.");
            }
            return cached;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException)
        {
            return null;
        }
    }

    private string CachePath(string repositoryUrl)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(repositoryUrl)))
            .ToLowerInvariant();
        return Path.Combine(cacheRoot, $"{hash}.json");
    }

    private static string? ParseReleaseBody(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("body", out var body)
            && body.ValueKind == JsonValueKind.String
                ? body.GetString()
                : null;
    }

    private static string? SanitizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : MarkdownSanitizer.Sanitize(value);

    private static bool TryParseGitHubRepository(
        string? value,
        out string owner,
        out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return false;
        }
        owner = segments[0];
        repository = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];
        return owner.Length != 0 && repository.Length != 0;
    }

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private sealed record FetchResult(string? Content, string? ETag, bool NotModified);
}
