using System.Text.Json;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.ViewModels.DependencyGraph;

public sealed record DependencyGraphNodePosition(double X, double Y);

public readonly record struct DependencyGraphLayoutApplicationResult(int AppliedCount, bool HadExpiredNodes);

public sealed class DependencyGraphLayout
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Dictionary<string, DependencyGraphNodePosition> Positions { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public static class DependencyGraphLayoutStore
{
    public static Task WriteAsync(
        string path,
        DependencyGraphLayout layout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(layout);
        return AtomicJsonStore.WriteAsync(path, layout, cancellationToken);
    }

    public static async Task<DependencyGraphLayout?> TryReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var layout = await AtomicJsonStore.ReadAsync<DependencyGraphLayout>(path, cancellationToken);
            return IsValid(layout) ? layout : null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsValid(DependencyGraphLayout layout) =>
        layout.SchemaVersion == DependencyGraphLayout.CurrentSchemaVersion
        && layout.Positions.All(pair =>
            !string.IsNullOrWhiteSpace(pair.Key)
            && double.IsFinite(pair.Value.X)
            && double.IsFinite(pair.Value.Y));
}
