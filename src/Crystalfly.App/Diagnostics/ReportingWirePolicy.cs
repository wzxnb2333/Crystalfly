using System.Text;
using System.Text.Json;
using Crystalfly.Core.Diagnostics;

namespace Crystalfly.App.Diagnostics;

// Cache replay bypasses BeforeSend, so the transport validates it as well.
internal static class ReportingWirePolicy
{
    private static readonly HashSet<string> RootKeys = ["event_id", "timestamp", "platform", "release",
        "environment", "level", "logger", "logentry", "exception", "tags", "fingerprint", "sdk"];
    private static readonly HashSet<string> NestedKeys = ["values", "type", "value", "module",
        "stacktrace", "frames", "function", "in_app", "formatted", "message", "params",
        "operation", "os_version", "architecture", "report_id", "name", "version",
        "packages", "integrations", "mechanism", "handled", "exception_id", "parent_id"];

    public static bool IsAllowed(byte[] bytes)
    {
        try
        {
            if (bytes.Length > ErrorReport.MaxBytes) return false;
            var lines = Encoding.UTF8.GetString(bytes).TrimEnd('\n').Split('\n');
            if (lines.Length != 3) return false;
            using var header = JsonDocument.Parse(lines[0]);
            if (header.RootElement.EnumerateObject().Any(property => property.Name is not ("event_id" or "sent_at" or "sdk")
                || !Check(property.Value))) return false;
            using var item = JsonDocument.Parse(lines[1]);
            if (item.RootElement.GetProperty("type").GetString() != "event"
                || item.RootElement.EnumerateObject().Any(property => property.Name is not ("type" or "length"))) return false;
            using var payload = JsonDocument.Parse(lines[2]);
            if (!payload.RootElement.TryGetProperty("timestamp", out var time)
                || !time.TryGetDateTimeOffset(out var timestamp)
                || timestamp > DateTimeOffset.UtcNow.AddMinutes(5)
                || timestamp < DateTimeOffset.UtcNow - ErrorReport.Retention) return false;
            return payload.RootElement.EnumerateObject().All(property => RootKeys.Contains(property.Name)
                && Check(property.Value));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool Check(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().All(property => NestedKeys.Contains(property.Name) && Check(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().All(Check),
        JsonValueKind.String => element.GetString() is { Length: <= 512 } text
            && !text.Contains('\\') && !text.Contains('/') && !text.Contains('@')
            && !text.Contains("Bearer ", StringComparison.OrdinalIgnoreCase),
        _ => true
    };
}
