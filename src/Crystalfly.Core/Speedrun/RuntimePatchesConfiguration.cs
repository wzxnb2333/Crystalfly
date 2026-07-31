using System.Text.Json;
using System.Text.Json.Serialization;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Speedrun;

public sealed record RuntimePatchesConfiguration
{
    public const string FileName = "assemblyPatchesConfiguration.json";

    private static readonly string[] PropertyNames =
    [
        "ScreenShakeModifier",
        "MiniSaveStates",
        "FasterIntroSkip",
        "TextMasher"
    ];

    [JsonPropertyName("ScreenShakeModifier")]
    public bool ScreenShakeModifier { get; init; }

    [JsonPropertyName("MiniSaveStates")]
    public bool MiniSaveStates { get; init; }

    [JsonPropertyName("FasterIntroSkip")]
    public bool FasterIntroSkip { get; init; }

    [JsonPropertyName("TextMasher")]
    public bool TextMasher { get; init; }

    public static Task WriteAsync(
        string path,
        RuntimePatchesConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        AtomicJsonStore.WriteAsync(path, configuration, cancellationToken);

    public static async Task<RuntimePatchesConfiguration> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReadFileAsync(path, cancellationToken);
        }
        catch (InvalidDataException) when (File.Exists(path + ".bak"))
        {
            return await ReadFileAsync(path + ".bak", cancellationToken);
        }
    }

    private static async Task<RuntimePatchesConfiguration> ReadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using JsonDocument document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("RuntimePatches configuration root must be an object.");
            }
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != PropertyNames.Length
                || properties.Select(static property => property.Name).Distinct(StringComparer.Ordinal).Count()
                    != PropertyNames.Length
                || PropertyNames.Any(name => !properties.Any(property =>
                    string.Equals(property.Name, name, StringComparison.Ordinal)
                    && property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)))
            {
                throw new InvalidDataException("RuntimePatches configuration fields are invalid.");
            }

            return JsonSerializer.Deserialize<RuntimePatchesConfiguration>(
                       document.RootElement.GetRawText(),
                       CrystalflyJson.Options)
                   ?? throw new InvalidDataException("RuntimePatches configuration is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RuntimePatches configuration JSON is invalid.", exception);
        }
    }
}
