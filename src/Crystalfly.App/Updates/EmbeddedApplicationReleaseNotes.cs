using System.Reflection;

namespace Crystalfly.App.Updates;

public static class EmbeddedApplicationReleaseNotes
{
    public static string Load(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        using Stream? stream = typeof(EmbeddedApplicationReleaseNotes).Assembly.GetManifestResourceStream(
            $"Crystalfly.App.ReleaseNotes.{version}.md");
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
