using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class EmbeddedModTranslationCatalog
{
    private const string ResourceName = "Crystalfly.Core.Data.mod-translations.zh-CN.v1.json";

    public static ModTranslationCatalog Load()
    {
        using var stream = typeof(EmbeddedModTranslationCatalog).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        return ModTranslationSource.Validate(
            JsonSerializer.Deserialize<ModTranslationCatalog>(stream, CrystalflyJson.Options)
            ?? throw new JsonException("Embedded translation catalog did not contain a value."));
    }
}
