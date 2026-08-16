using Crystalfly.App.Updates;

namespace Crystalfly.App.Tests.Updates;

public sealed class EmbeddedApplicationReleaseNotesTests
{
    [Fact]
    public void Load_returns_the_packaged_release_notes()
    {
        string notes = EmbeddedApplicationReleaseNotes.Load("1.1.4");

        Assert.Contains("Crystalfly 1.1.4", notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_returns_empty_text_for_an_unknown_version()
    {
        Assert.Equal(string.Empty, EmbeddedApplicationReleaseNotes.Load("99.99.99"));
    }
}
