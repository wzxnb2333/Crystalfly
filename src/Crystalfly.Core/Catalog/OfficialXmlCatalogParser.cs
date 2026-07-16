using System.Xml.Linq;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Catalog;

public static class OfficialXmlCatalogParser
{
    public static IReadOnlyList<ModManifest> ParseMods(
        string xml,
        string loaderId,
        string buildId)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("ModLinks XML has no root element.");
        var ns = root.Name.Namespace;
        return root.Elements(ns + "Manifest")
            .Select(manifest =>
            {
                var name = RequiredValue(manifest, ns + "Name");
                var link = manifest.Element(ns + "Link")
                    ?? throw new InvalidDataException($"Mod '{name}' has no download link.");
                return new ModManifest
                {
                    Id = OfficialModId(name),
                    Name = name,
                    Version = RequiredValue(manifest, ns + "Version"),
                    DownloadUrl = HttpsUrl(link.Value),
                    Sha256 = Sha256(link.Attribute("SHA256")?.Value),
                    LoaderId = loaderId,
                    SupportedBuildIds = [buildId],
                    Dependencies = manifest.Element(ns + "Dependencies")?
                        .Elements(ns + "Dependency")
                        .Select(dependency => OfficialModId(dependency.Value.Trim()))
                        .Where(dependency => dependency.Length > "hkmod:".Length)
                        .ToArray() ?? []
                };
            })
            .ToArray();
    }

    public static LoaderManifest ParseApi(string xml, string buildId)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("ApiLinks XML has no root element.");
        var ns = root.Name.Namespace;
        var manifest = root.Element(ns + "Manifest")
            ?? throw new InvalidDataException("ApiLinks XML has no manifest.");
        var version = RequiredValue(manifest, ns + "Version");
        var windows = manifest.Element(ns + "Links")?.Element(ns + "Windows")
            ?? throw new InvalidDataException("ApiLinks XML has no Windows package.");
        return new LoaderManifest
        {
            Id = $"modding-api-{version}",
            Name = "Modding API",
            Version = version,
            DownloadUrl = HttpsUrl(windows.Value),
            Sha256 = Sha256(windows.Attribute("SHA256")?.Value),
            SupportedBuildIds = [buildId],
            ManagedFiles = manifest.Element(ns + "Files")?
                .Elements(ns + "File")
                .Select(file => file.Value.Trim().Replace('\\', '/'))
                .Where(file => file.Length > 0)
                .ToArray() ?? []
        };
    }

    private static string OfficialModId(string name) => $"hkmod:{name.Trim()}";

    private static string RequiredValue(XElement element, XName name)
    {
        var value = element.Element(name)?.Value.Trim();
        return string.IsNullOrEmpty(value)
            ? throw new InvalidDataException($"Missing XML value '{name.LocalName}'.")
            : value;
    }

    private static string HttpsUrl(string value)
    {
        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? trimmed
            : throw new InvalidDataException($"Package URL must use HTTPS: '{trimmed}'.");
    }

    private static string Sha256(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized?.Length != 64)
        {
            throw new InvalidDataException("Package SHA-256 must contain 64 hexadecimal characters.");
        }
        try
        {
            _ = Convert.FromHexString(normalized);
            return normalized;
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Package SHA-256 contains non-hexadecimal characters.", exception);
        }
    }
}
