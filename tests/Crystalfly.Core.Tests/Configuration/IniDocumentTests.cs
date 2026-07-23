using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Tests.Configuration;

public sealed class IniDocumentTests
{
    [Fact]
    public void Parse_reads_sections_keys_comments_and_blanks()
    {
        var content = """
            [Accessibility]
            ; shake comment
            ReducedCameraShake=0.2

            ReducedControllerRumble=0.4
            [Video]
            # hash comment
            Width=1920
            """;

        var document = IniDocument.Parse(content);

        Assert.Equal("0.2", document.GetValue("Accessibility", "ReducedCameraShake"));
        Assert.Equal("0.4", document.GetValue("Accessibility", "ReducedControllerRumble"));
        Assert.Equal("1920", document.GetValue("Video", "Width"));
        Assert.Equal(2, document.Sections.Count);
    }

    [Fact]
    public void Round_trip_preserves_comments_blanks_and_key_order()
    {
        var content = """
            [Accessibility]
            ; shake controls camera movement
            ReducedCameraShake=0.2

            ReducedControllerRumble=0.4
            """;
        var document = IniDocument.Parse(content);

        var serialized = document.ToString();
        var reparsed = IniDocument.Parse(serialized);

        Assert.Equal("0.2", reparsed.GetValue("Accessibility", "ReducedCameraShake"));
        Assert.Equal("0.4", reparsed.GetValue("Accessibility", "ReducedControllerRumble"));
        var section = reparsed.GetSection("Accessibility");
        Assert.NotNull(section);
        Assert.Contains(section!.Entries, entry => entry.Kind == IniEntryKind.Comment);
        Assert.Contains(section.Entries, entry => entry.Kind == IniEntryKind.Blank);
    }

    [Fact]
    public void SetValue_updates_existing_key_without_losing_unknown_fields()
    {
        var document = IniDocument.Parse("""
            [Accessibility]
            ReducedCameraShake=0.2
            CustomTweak=7
            [Mod]
            Enabled=true
            """);

        document.SetValue("Accessibility", "ReducedCameraShake", "0.9");

        Assert.Equal("0.9", document.GetValue("Accessibility", "ReducedCameraShake"));
        Assert.Equal("7", document.GetValue("Accessibility", "CustomTweak"));
        Assert.Equal("true", document.GetValue("Mod", "Enabled"));
    }

    [Fact]
    public void SetValue_creates_section_and_key_when_missing()
    {
        var document = new IniDocument();

        document.SetValue("Accessibility", "ReducedCameraShake", "0.5");

        Assert.Equal("0.5", document.GetValue("Accessibility", "ReducedCameraShake"));
    }

    [Fact]
    public void Remove_deletes_key_and_RemoveSection_deletes_section()
    {
        var document = IniDocument.Parse("""
            [Accessibility]
            ReducedCameraShake=0.2
            [Video]
            Width=1920
            """);

        Assert.True(document.Remove("Accessibility", "ReducedCameraShake"));
        Assert.Null(document.GetValue("Accessibility", "ReducedCameraShake"));
        Assert.True(document.RemoveSection("Video"));
        Assert.Null(document.GetSection("Video"));
        Assert.False(document.Remove("Missing", "Key"));
    }

    [Fact]
    public void Parse_tolerates_malformed_lines_by_keeping_them_as_comments()
    {
        var document = IniDocument.Parse("""
            [Accessibility]
            this line has no equals sign
            ReducedCameraShake=0.2
            """);

        Assert.Equal("0.2", document.GetValue("Accessibility", "ReducedCameraShake"));
        var section = document.GetSection("Accessibility");
        Assert.Contains(section!.Entries, entry => entry.Kind == IniEntryKind.Comment);
    }

    [Fact]
    public void Parse_empty_content_yields_empty_document()
    {
        Assert.Empty(IniDocument.Parse(null).Sections);
        Assert.Empty(IniDocument.Parse(string.Empty).Sections);
    }

    [Fact]
    public void Keys_before_any_section_header_use_default_section()
    {
        var document = IniDocument.Parse("Orphan=1");

        Assert.Equal("1", document.GetValue(IniDocument.DefaultSectionName, "Orphan"));
    }
}
