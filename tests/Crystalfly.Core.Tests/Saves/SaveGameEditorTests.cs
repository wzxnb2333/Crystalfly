using Crystalfly.Core.Saves;

namespace Crystalfly.Core.Tests.Saves;

public sealed class SaveGameEditorTests
{
    [Theory]
    [InlineData("9007199254740993")]
    [InlineData("9223372036854775807")]
    [InlineData("0.1234567890123456789012345678")]
    [InlineData("1e400")]
    public void Rebuild_preserves_exact_json_numbers(string number)
    {
        string json = "{\"value\":" + number + "}";
        SaveEntry entry = Assert.Single(SaveGameEditor.Flatten(json));

        Assert.Equal(number, entry.Value);
        using var document = System.Text.Json.JsonDocument.Parse(SaveGameEditor.Rebuild(json, [entry]));
        Assert.Equal(number, document.RootElement.GetProperty("value").GetRawText());
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("1,234")]
    public void Rebuild_rejects_non_json_numeric_input(string value)
    {
        Assert.Throws<FormatException>(() => SaveGameEditor.Rebuild(
            "{\"value\":1}", [new SaveEntry("value", value, SaveEntry.KindNumber)]));
    }

    [Theory]
    [InlineData("mod.value")]
    [InlineData("items[0]")]
    [InlineData("")]
    [InlineData("a\\b")]
    [InlineData("quote\"key")]
    public void Rebuild_edits_property_names_containing_path_syntax(string key)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, int> { [key] = 1 });
        SaveEntry entry = Assert.Single(SaveGameEditor.Flatten(json)) with { Value = "9" };

        using var document = System.Text.Json.JsonDocument.Parse(SaveGameEditor.Rebuild(json, [entry]));

        Assert.Equal(9, document.RootElement.GetProperty(key).GetInt32());
    }

    [Fact]
    public void Flatten_distinguishes_literal_keys_from_nested_paths()
    {
        const string json = """{"mod.value":1,"mod":{"value":2},"items[0]":3,"items":[4]}""";
        var entries = SaveGameEditor.Flatten(json);

        Assert.Equal(entries.Count, entries.Select(entry => entry.Path).Distinct().Count());
        string rebuilt = SaveGameEditor.Rebuild(json, entries);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(json), System.Text.Json.Nodes.JsonNode.Parse(rebuilt)));
    }

    [Fact]
    public void Rebuild_preserves_quoted_keys_with_nested_arrays_objects_and_null_values()
    {
        const string json = """{"mod.value":[{"a[b]":{"":null,"plain":2},"quote\"key":3}]}""";
        var entries = SaveGameEditor.Flatten(json).ToArray();
        string rebuilt = SaveGameEditor.Rebuild(json, entries);

        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(json), System.Text.Json.Nodes.JsonNode.Parse(rebuilt)));
    }

    [Fact]
    public void Flatten_produces_dot_paths_for_nested_objects()
    {
        const string json = """{"player":{"health":5,"position":{"x":1.5,"y":2.0}}}""";

        var entries = SaveGameEditor.Flatten(json);

        Assert.Contains(entries, e => e.Path == "player.health" && e.Value == "5" && e.Kind == SaveEntry.KindNumber);
        Assert.Contains(entries, e => e.Path == "player.position.x" && e.Value == "1.5" && e.Kind == SaveEntry.KindNumber);
        Assert.Contains(entries, e => e.Path == "player.position.y" && e.Value == "2.0" && e.Kind == SaveEntry.KindNumber);
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
