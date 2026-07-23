using System.Globalization;

namespace Crystalfly.Core.Configuration;

/// <summary>
/// Reads and writes the Hollow Knight <c>AppConfig.ini</c> file. The game stores only its
/// accessibility settings here as INI text; video, audio, language, and key bindings live in
/// the registry and binary saves and are intentionally out of scope. Writes are atomic
/// (temporary file plus <c>File.Replace</c> with a <c>.bak</c> fallback) mirroring
/// <see cref="Serialization.AtomicJsonStore"/>.
/// </summary>
public static class AppConfigService
{
    /// <summary>File name of the game configuration within an instance LocalLow directory.</summary>
    public const string FileName = "AppConfig.ini";

    /// <summary>Section that holds the accessibility settings.</summary>
    public const string AccessibilitySection = "Accessibility";

    /// <summary>Key for the reduced camera shake strength (0–1).</summary>
    public const string ReducedCameraShakeKey = "ReducedCameraShake";

    /// <summary>Key for the reduced controller rumble strength (0–1).</summary>
    public const string ReducedControllerRumbleKey = "ReducedControllerRumble";

    /// <summary>
    /// Loads the document at <paramref name="path"/>. A missing or empty file yields an empty
    /// document so callers can populate defaults and save.
    /// </summary>
    public static async Task<IniDocument> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return new IniDocument();
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return IniDocument.Parse(content);
    }

    /// <summary>
    /// Atomically writes <paramref name="document"/> to <paramref name="path"/>, creating the
    /// parent directory and keeping a <c>.bak</c> copy of the previous file when it exists.
    /// </summary>
    public static async Task SaveAsync(
        string path,
        IniDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var targetPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("Path must include a directory.", nameof(path));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream))
            {
                document.Write(writer);
                await writer.FlushAsync(cancellationToken);
            }

            if (File.Exists(targetPath))
            {
                File.Replace(temporaryPath, targetPath, targetPath + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Reads a floating-point value from the document, returning <paramref name="defaultValue"/>
    /// when the key is missing or not a valid invariant-culture number.
    /// </summary>
    public static double GetDouble(
        IniDocument document,
        string section,
        string key,
        double defaultValue)
    {
        var raw = document.GetValue(section, key);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    /// <summary>Writes a floating-point value to the document using invariant culture.</summary>
    public static void SetDouble(
        IniDocument document,
        string section,
        string key,
        double value) =>
        document.SetValue(section, key, value.ToString("0.###", CultureInfo.InvariantCulture));
}
