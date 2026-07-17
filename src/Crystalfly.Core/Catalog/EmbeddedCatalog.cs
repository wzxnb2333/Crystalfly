using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class EmbeddedCatalog
{
    private const string ResourceName = "Crystalfly.Core.Data.official-catalog.json";

    public static GameCatalog Load()
    {
        using var stream = typeof(EmbeddedCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded catalog resource '{ResourceName}' was not found.");
        return JsonSerializer.Deserialize<GameCatalog>(stream, CrystalflyJson.Options)
            ?? throw new JsonException("Embedded catalog did not contain a catalog value.");
    }
}
