using Crystalfly.Core.Saves;

namespace Crystalfly.Core.Tests.Saves;

public sealed class SaveGameEditorTests
{
    [Fact]
    public void Flatten_produces_dot_paths_for_nested_objects()
    {
        const string json = """{"player":{"health":5,"position":{"x":1.5,"y":2.0}}}""";

        var entries = SaveGameEditor.Flatten(json);

        Assert.Contains(entries, e => e.Path == "player.health" && e.Value == "5" && e.Kind == SaveEntry.KindNumber);
        Assert.Contains(entries, e => e.Path == "player.position.x" && e.Value == "1.5" && e.Kind == SaveEntry.KindNumber);
        Assert.Contains(entries, e => e.Path == "player.position.y" && e.Value == "2" && e.Kind == SaveEntry.KindNumber);
    }

    [Fact]
    public void Flatten_produces_index_paths_for_arrays()
    {
        const string json = """{"items":["sword","shield","charm"]}""";

        var entries = SaveGameEditor.Flatten(json);

        Assert.Contains(entries, e => e.Path == "items[0]" && e.Value == "sword" && e.Kind == SaveEntry.KindString);
        Assert.Contains(entries, e => e.Path == "items[1]" && e.Value == "shield");
        Assert.Contains(entries, e => e.Path == "items[2]" && e.Value == "charm");
    }

    [Fact]
    public void Flatten_handles_booleans_and_nulls()
    {
        const string json = """{"active":true,"deleted":false,"missing":null}""";

        var entries = SaveGameEditor.Flatten(json);

        Assert.Contains(entries, e => e.Path == "active" && e.Value == "true" && e.Kind == SaveEntry.KindBoolean);
        Assert.Contains(entries, e => e.Path == "deleted" && e.Value == "false" && e.Kind == SaveEntry.KindBoolean);
        Assert.Contains(entries, e => e.Path == "missing" && e.Kind == SaveEntry.KindNull);
    }

    [Fact]
    public void Flatten_handles_nested_arrays_in_objects()
    {
        const string json = """{"scenes":[{"name":"crossroads"},{"name":"greenpath"}]}""";

        var entries = SaveGameEditor.Flatten(json);

        Assert.Contains(entries, e => e.Path == "scenes[0].name" && e.Value == "crossroads");
        Assert.Contains(entries, e => e.Path == "scenes[1].name" && e.Value == "greenpath");
    }

    [Fact]
    public void Rebuild_preserves_structure_and_types()
    {
        const string json = """{"health":5,"name":"knight","active":true,"items":["a","b"]}""";
        var entries = SaveGameEditor.Flatten(json);

        var rebuilt = SaveGameEditor.Rebuild(json, entries);
        var reFlattened = SaveGameEditor.Flatten(rebuilt);

        Assert.Equal(entries.Count, reFlattened.Count);
        foreach (var original in entries)
        {
            Assert.Contains(reFlattened, e => e.Path == original.Path && e.Value == original.Value && e.Kind == original.Kind);
        }
    }

    [Fact]
    public void Rebuild_reflects_modified_values()
    {
        const string json = """{"health":5,"geo":100}""";
        var entries = SaveGameEditor.Flatten(json).ToList();
        var healthIndex = entries.FindIndex(e => e.Path == "health");
        entries[healthIndex] = entries[healthIndex] with { Value = "9" };

        var rebuilt = SaveGameEditor.Rebuild(json, entries);
        var result = SaveGameEditor.Flatten(rebuilt);

        Assert.Contains(result, e => e.Path == "health" && e.Value == "9");
        Assert.Contains(result, e => e.Path == "geo" && e.Value == "100");
    }

    [Fact]
    public void Rebuild_reflects_modified_string_values()
    {
        const string json = """{"playerName":"knight"}""";
        var entries = SaveGameEditor.Flatten(json).ToList();
        entries[0] = entries[0] with { Value = "hornet" };

        var rebuilt = SaveGameEditor.Rebuild(json, entries);

        Assert.Contains("hornet", rebuilt);
    }

    [Fact]
    public void Rebuild_reflects_modified_boolean_values()
    {
        const string json = """{"godmode":false}""";
        var entries = SaveGameEditor.Flatten(json).ToList();
        entries[0] = entries[0] with { Value = "true" };

        var rebuilt = SaveGameEditor.Rebuild(json, entries);
        var result = SaveGameEditor.Flatten(rebuilt);

        Assert.Contains(result, e => e.Path == "godmode" && e.Value == "true" && e.Kind == SaveEntry.KindBoolean);
    }

    [Fact]
    public void Flatten_rejects_invalid_json()
    {
        Assert.Throws<InvalidDataException>(() => SaveGameEditor.Flatten("not json"));
    }
}
