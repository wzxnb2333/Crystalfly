using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crystalfly.Core.Saves;

/// <summary>
/// A single flattened key-value entry from a save game JSON document.
/// </summary>
public sealed record SaveEntry(string Path, string Value, string Kind)
{
    public const string KindString = "string";
    public const string KindNumber = "number";
    public const string KindBoolean = "boolean";
    public const string KindNull = "null";
}

/// <summary>
/// Flattens and rebuilds Hollow Knight save JSON for editing in a tabular UI.
/// </summary>
public static class SaveGameEditor
{
    public static IReadOnlyList<SaveEntry> Flatten(string json)
    {
        JsonNode node;
        try
        {
            node = JsonNode.Parse(json)
                ?? throw new InvalidDataException("Save data is not valid JSON.");
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new InvalidDataException("Save data is not valid JSON.", exception);
        }

        var entries = new List<SaveEntry>();
        FlattenNode(node, string.Empty, entries);
        return entries;
    }

    public static string Rebuild(string originalJson, IReadOnlyList<SaveEntry> entries)
    {
        var node = JsonNode.Parse(originalJson)
            ?? throw new InvalidDataException("Save data is not valid JSON.");
        foreach (var entry in entries)
        {
            SetByPath(node, entry.Path, entry);
        }

        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void FlattenNode(JsonNode node, string prefix, List<SaveEntry> entries)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    var path = prefix.Length == 0 ? property.Key : $"{prefix}.{property.Key}";
                    if (property.Value is null)
                    {
                        entries.Add(new SaveEntry(path, string.Empty, SaveEntry.KindNull));
                    }
                    else
                    {
                        FlattenNode(property.Value, path, entries);
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var path = $"{prefix}[{i}]";
                    if (array[i] is null)
                    {
                        entries.Add(new SaveEntry(path, string.Empty, SaveEntry.KindNull));
                    }
                    else
                    {
                        FlattenNode(array[i]!, path, entries);
                    }
                }

                break;
            case JsonValue value:
                entries.Add(CreateValueEntry(prefix, value));
                break;
        }
    }

    private static SaveEntry CreateValueEntry(string path, JsonValue value)
    {
        if (value.TryGetValue<bool>(out var boolVal))
        {
            return new SaveEntry(path, boolVal ? "true" : "false", SaveEntry.KindBoolean);
        }

        if (value.TryGetValue<double>(out var numVal))
        {
            return new SaveEntry(path, numVal.ToString(System.Globalization.CultureInfo.InvariantCulture), SaveEntry.KindNumber);
        }

        return new SaveEntry(path, value.ToString(), SaveEntry.KindString);
    }

    private static void SetByPath(JsonNode root, string path, SaveEntry entry)
    {
        var segments = ParsePath(path);
        JsonNode? current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            current = NavigateSegment(current, segments[i]);
            if (current is null)
            {
                return;
            }
        }

        var last = segments[^1];
        var newValue = CreateJsonNode(entry);
        if (last.IsIndex)
        {
            if (current is JsonArray arr && last.Index < arr.Count)
            {
                arr[last.Index] = newValue;
            }
        }
        else
        {
            if (current is JsonObject obj && obj.ContainsKey(last.Key!))
            {
                obj[last.Key!] = newValue;
            }
        }
    }

    private static JsonNode? NavigateSegment(JsonNode? node, PathSegment segment)
    {
        if (node is null)
        {
            return null;
        }

        return segment.IsIndex
            ? (node is JsonArray arr && segment.Index < arr.Count ? arr[segment.Index] : null)
            : (node is JsonObject obj && obj.ContainsKey(segment.Key!) ? obj[segment.Key!] : null);
    }

    private static JsonNode? CreateJsonNode(SaveEntry entry) => entry.Kind switch
    {
        SaveEntry.KindNull => null,
        SaveEntry.KindBoolean => JsonValue.Create(bool.Parse(entry.Value)),
        SaveEntry.KindNumber => JsonValue.Create(
            double.Parse(entry.Value, System.Globalization.CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(entry.Value)
    };

    private static List<PathSegment> ParsePath(string path)
    {
        var segments = new List<PathSegment>();
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] == '[')
            {
                var end = path.IndexOf(']', i);
                if (end < 0)
                {
                    break;
                }

                segments.Add(new PathSegment(int.Parse(path[(i + 1)..end], System.Globalization.CultureInfo.InvariantCulture)));
                i = end + 1;
                if (i < path.Length && path[i] == '.')
                {
                    i++;
                }
            }
            else
            {
                var end = path.IndexOfAny(['.', '['], i);
                if (end < 0)
                {
                    segments.Add(new PathSegment(path[i..]));
                    break;
                }

                segments.Add(new PathSegment(path[i..end]));
                i = end;
                if (path[i] == '.')
                {
                    i++;
                }
            }
        }

        return segments;
    }

    private readonly record struct PathSegment(int Index)
    {
        public string? Key { get; } = null;
        public bool IsIndex => Key is null;

        public PathSegment(string key) : this(0) => Key = key;
    }
}
