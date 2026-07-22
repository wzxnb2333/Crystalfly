using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class EmbeddedModActivityCatalog
{
    private const string ResourceName = "Crystalfly.Core.Data.mod-activity.v1.json";

    public static ModActivityCatalog Load()
    {
        using var stream = typeof(EmbeddedModActivityCatalog).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        return ModActivitySource.Validate(
            JsonSerializer.Deserialize<ModActivityCatalog>(stream, CrystalflyJson.Options)
            ?? throw new JsonException("Embedded Mod activity catalog did not contain a value."));
    }
}
