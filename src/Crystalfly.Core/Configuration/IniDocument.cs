using System.Text;

namespace Crystalfly.Core.Configuration;

/// <summary>
/// The kind of a single line entry inside an INI section.
/// </summary>
public enum IniEntryKind
{
    /// <summary>A <c>key=value</c> assignment.</summary>
    KeyValue,

    /// <summary>A comment line beginning with <c>;</c> or <c>#</c>.</summary>
    Comment,

    /// <summary>A blank or whitespace-only line.</summary>
    Blank
}

/// <summary>
/// A single line within an INI section: a key/value pair, a comment, or a blank line.
/// Comments and blanks are preserved verbatim so documents round-trip without data loss.
/// </summary>
public sealed class IniEntry
{
    public IniEntryKind Kind { get; }

    public string? Key { get; }

    public string Value { get; set; }

    private IniEntry(IniEntryKind kind, string? key, string value)
    {
        Kind = kind;
        Key = key;
        Value = value;
    }

    public static IniEntry CreateKeyValue(string key, string value) =>
        new(IniEntryKind.KeyValue, key, value);

    public static IniEntry CreateComment(string text) =>
        new(IniEntryKind.Comment, null, text);

    public static IniEntry CreateBlank() =>
        new(IniEntryKind.Blank, null, string.Empty);
}

/// <summary>
/// A named INI section holding an ordered list of entries. The section name is empty for
/// keys that appear before any <c>[section]</c> header.
/// </summary>
public sealed class IniSection
{
    public string Name { get; }

    public List<IniEntry> Entries { get; } = [];

    public IniSection(string name)
    {
        Name = name;
    }

    /// <summary>Gets the value of a key within this section, or <c>null</c> when absent.</summary>
    public string? GetValue(string key)
    {
        foreach (var entry in Entries)
        {
            if (entry.Kind == IniEntryKind.KeyValue
                && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets the value of a key, updating the existing entry in place or appending a new one.
    /// </summary>
    public void SetValue(string key, string value)
    {
        foreach (var entry in Entries)
        {
            if (entry.Kind == IniEntryKind.KeyValue
                && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                entry.Value = value;
                return;
            }
        }

        Entries.Add(IniEntry.CreateKeyValue(key, value));
    }

    /// <summary>Removes a key from this section. Returns <c>true</c> when an entry was removed.</summary>
    public bool Remove(string key)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Kind == IniEntryKind.KeyValue
                && string.Equals(Entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                Entries.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Enumerates the key/value pairs in this section in document order.</summary>
    public IEnumerable<IniEntry> KeyValues
    {
        get
        {
            foreach (var entry in Entries)
            {
                if (entry.Kind == IniEntryKind.KeyValue)
                {
                    yield return entry;
                }
            }
        }
    }
}

/// <summary>
/// An in-memory INI document that preserves section order, key order, comments, and blank
/// lines so editing a single value never discards unknown fields. Parsing is tolerant:
/// malformed lines are kept as comments rather than throwing.
/// </summary>
public sealed class IniDocument
{
    /// <summary>Section name used for keys that appear before any section header.</summary>
    public const string DefaultSectionName = "";

    private readonly List<IniSection> sections = [];

    public IReadOnlyList<IniSection> Sections => sections;

    /// <summary>Gets the section with the given name, or <c>null</c> when absent.</summary>
    public IniSection? GetSection(string name)
    {
        foreach (var section in sections)
        {
            if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return section;
            }
        }

        return null;
    }

    /// <summary>Gets the section with the given name, creating it when missing.</summary>
    public IniSection GetOrAddSection(string name)
    {
        var existing = GetSection(name);
        if (existing is not null)
        {
            return existing;
        }

        var section = new IniSection(name);
        sections.Add(section);
        return section;
    }

    /// <summary>Gets a value by section and key, or <c>null</c> when absent.</summary>
    public string? GetValue(string section, string key) =>
        GetSection(section)?.GetValue(key);

    /// <summary>Sets a value by section and key, creating the section and key when missing.</summary>
    public void SetValue(string section, string key, string value) =>
        GetOrAddSection(section).SetValue(key, value);

    /// <summary>Removes a key from a section. Returns <c>true</c> when an entry was removed.</summary>
    public bool Remove(string section, string key) =>
        GetSection(section)?.Remove(key) ?? false;

    /// <summary>Removes an entire section. Returns <c>true</c> when a section was removed.</summary>
    public bool RemoveSection(string name)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (string.Equals(sections[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                sections.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses INI content from a string. Empty input yields an empty document.</summary>
    public static IniDocument Parse(string? content)
    {
        var document = new IniDocument();
        if (string.IsNullOrEmpty(content))
        {
            return document;
        }

        using var reader = new StringReader(content);
        document.Read(reader);
        return document;
    }

    private void Read(TextReader reader)
    {
        IniSection? current = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                EnsureSection(ref current).Entries.Add(IniEntry.CreateBlank());
                continue;
            }

            if (trimmed[0] is ';' or '#')
            {
                EnsureSection(ref current).Entries.Add(IniEntry.CreateComment(line));
                continue;
            }

            if (trimmed[0] == '[' && trimmed[^1] == ']')
            {
                var name = trimmed[1..^1].Trim();
                current = GetOrAddSection(name);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                EnsureSection(ref current).Entries.Add(IniEntry.CreateKeyValue(key, value));
                continue;
            }

            // Tolerate malformed lines by preserving them as comments instead of throwing.
            EnsureSection(ref current).Entries.Add(IniEntry.CreateComment(line));
        }
    }

    private IniSection EnsureSection(ref IniSection? current)
    {
        if (current is not null)
        {
            return current;
        }

        current = GetOrAddSection(DefaultSectionName);
        return current;
    }

    /// <summary>Serializes the document back to INI text.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        Write(new StringWriter(builder));
        return builder.ToString();
    }

    /// <summary>Writes the document to a writer using <c>key=value</c> lines per section.</summary>
    public void Write(TextWriter writer)
    {
        for (var s = 0; s < sections.Count; s++)
        {
            var section = sections[s];
            if (section.Name.Length > 0)
            {
                writer.WriteLine($"[{section.Name}]");
            }

            foreach (var entry in section.Entries)
            {
                switch (entry.Kind)
                {
                    case IniEntryKind.KeyValue:
                        writer.WriteLine($"{entry.Key}={entry.Value}");
                        break;
                    case IniEntryKind.Comment:
                        writer.WriteLine(entry.Value);
                        break;
                    default:
                        writer.WriteLine();
                        break;
                }
            }

            var isLast = s == sections.Count - 1;
            var hasTrailingBlank = section.Entries.Count > 0
                && section.Entries[^1].Kind == IniEntryKind.Blank;
            if (!isLast && !hasTrailingBlank)
            {
                writer.WriteLine();
            }
        }
    }
}
