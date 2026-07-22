using System.Text;
using System.Text.RegularExpressions;

namespace Crystalfly.Core.Catalog;

public static partial class MarkdownSanitizer
{
    private const int MaximumLength = 1_048_576;

    public static string Sanitize(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (markdown.Length > MaximumLength)
        {
            throw new InvalidDataException("Markdown content exceeds the supported size.");
        }

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = DangerousBlockRegex().Replace(normalized, string.Empty);
        normalized = ImageRegex().Replace(normalized, match => match.Groups[1].Value);
        normalized = LinkRegex().Replace(normalized, match =>
            IsHttps(match.Groups[2].Value)
                ? $"[{match.Groups[1].Value}]({match.Groups[2].Value})"
                : match.Groups[1].Value);
        normalized = HtmlTagRegex().Replace(normalized, string.Empty);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (character is '\n' or '\t' || !char.IsControl(character))
            {
                builder.Append(character);
            }
        }
        return builder.ToString().Trim();
    }

    private static bool IsHttps(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    [GeneratedRegex("<(script|style|iframe|object|embed)\\b[^>]*>.*?</\\1\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DangerousBlockRegex();

    [GeneratedRegex("!\\[([^]\\r\\n]*)\\]\\([^\\r\\n)]*\\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex("""(?<!!)\[([^]\r\n]+)\]\(([^\s)]+)(?:\s+['"][^'"]*['"])?\)""")]
    private static partial Regex LinkRegex();

    [GeneratedRegex("</?[A-Za-z][^>\\r\\n]*>")]
    private static partial Regex HtmlTagRegex();
}
