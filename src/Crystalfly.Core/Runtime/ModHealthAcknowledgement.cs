using System.Security.Cryptography;
using System.Text;

namespace Crystalfly.Core.Runtime;

public sealed record ModHealthAcknowledgement
{
    public required string Fingerprint { get; init; }

    public static ModHealthAcknowledgement Create(
        string instanceId,
        LaunchPreflightIssue issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(issue.SubjectModId);

        string canonicalValue = string.Join(
            '\u001f',
            Normalize(instanceId),
            Normalize(issue.SubjectModId),
            issue.Code.ToString(),
            NormalizePath(issue.RelativeFilePath),
            Normalize(issue.CurrentFileSha256));
        return new ModHealthAcknowledgement
        {
            Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue)))
        };
    }

    internal bool Matches(string instanceId, LaunchPreflightIssue issue) =>
        string.Equals(Fingerprint, Create(instanceId, issue).Fingerprint, StringComparison.Ordinal);

    private static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string NormalizePath(string? value) =>
        Normalize(value?.Replace('\\', '/'));
}
