namespace Crystalfly.Core.Runtime;

public static class ProtocolCommandParser
{
    public const int MaxUriLength = 2048;
    public const int MaxUriBytes = 4096;
    private const int MaxParameters = 8;
    private const int MaxIdentifierLength = 256;

    public static ProtocolCommand Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxUriLength
            || System.Text.Encoding.UTF8.GetByteCount(value) > MaxUriBytes)
        {
            throw Invalid("The Crystalfly command is empty or too long.");
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "crystalfly", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !uri.IsDefaultPort)
        {
            throw Invalid("The external command URI is invalid.");
        }

        var parameters = ParseQuery(uri.Query);
        var route = $"{uri.Host}{uri.AbsolutePath.TrimEnd('/')}".ToLowerInvariant();
        return route switch
        {
            "mod/download" => WithInstanceAndMod(
                ProtocolCommandKind.DownloadMod,
                parameters,
                ["instance", "id"]),
            "mod/reinstall-all" => WithInstance(
                ProtocolCommandKind.ReinstallAllMods,
                parameters),
            "app/reset-settings" => WithoutParameters(
                ProtocolCommandKind.ResetApplicationSettings,
                parameters),
            "modlinks/official" => WithoutParameters(
                ProtocolCommandKind.UseOfficialModLinks,
                parameters),
            "modlinks/custom" => CustomModLinks(parameters),
            "mod/settings/delete" => WithInstanceAndMod(
                ProtocolCommandKind.DeleteModSettings,
                parameters,
                ["instance", "id"]),
            "mod/settings/delete-all" => WithInstance(
                ProtocolCommandKind.DeleteAllModSettings,
                parameters),
            "instance/launch" => Launch(parameters),
            "mod/open" => WithInstanceAndMod(
                ProtocolCommandKind.OpenModLocation,
                parameters,
                ["instance", "id"]),
            "modpack" => ImportPreset(parameters),
            _ => throw Invalid("The external command is not supported.")
        };
    }

    private static ProtocolCommand WithInstance(
        ProtocolCommandKind kind,
        IReadOnlyDictionary<string, string> parameters)
    {
        RequireExactKeys(parameters, ["instance"]);
        return new ProtocolCommand
        {
            Kind = kind,
            InstanceId = Identifier(parameters, "instance")
        };
    }

    private static ProtocolCommand WithInstanceAndMod(
        ProtocolCommandKind kind,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyCollection<string> keys)
    {
        RequireExactKeys(parameters, keys);
        return new ProtocolCommand
        {
            Kind = kind,
            InstanceId = Identifier(parameters, "instance"),
            ModId = Identifier(parameters, "id")
        };
    }

    private static ProtocolCommand Launch(IReadOnlyDictionary<string, string> parameters)
    {
        RequireExactKeys(parameters, ["id"]);
        return new ProtocolCommand
        {
            Kind = ProtocolCommandKind.LaunchInstance,
            InstanceId = Identifier(parameters, "id")
        };
    }

    private static ProtocolCommand ImportPreset(IReadOnlyDictionary<string, string> parameters)
    {
        RequireExactKeys(parameters, ["code"]);
        var code = parameters["code"];
        if (code.Length != 12
            || code.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw Invalid("The preset share code must contain 12 URL-safe characters.");
        }
        return new ProtocolCommand
        {
            Kind = ProtocolCommandKind.ImportPresetShare,
            ShareCode = code
        };
    }

    private static ProtocolCommand CustomModLinks(IReadOnlyDictionary<string, string> parameters)
    {
        RequireExactKeys(parameters, ["url", "build", "loader"]);
        if (!Uri.TryCreate(parameters["url"], UriKind.Absolute, out var source)
            || source.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(source.UserInfo)
            || !string.IsNullOrEmpty(source.Fragment))
        {
            throw Invalid("A custom ModLinks source must use an absolute HTTPS URL.");
        }
        return new ProtocolCommand
        {
            Kind = ProtocolCommandKind.UseCustomModLinks,
            SourceUrl = source.AbsoluteUri,
            BuildId = Identifier(parameters, "build"),
            LoaderId = Identifier(parameters, "loader")
        };
    }

    private static ProtocolCommand WithoutParameters(
        ProtocolCommandKind kind,
        IReadOnlyDictionary<string, string> parameters)
    {
        RequireExactKeys(parameters, []);
        return new ProtocolCommand { Kind = kind };
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }
        var parts = query.AsSpan(1).ToString().Split('&', StringSplitOptions.None);
        if (parts.Length > MaxParameters || parts.Any(string.IsNullOrEmpty))
        {
            throw Invalid("The external command has too many or empty parameters.");
        }
        foreach (var part in parts)
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                throw Invalid("The external command contains a malformed parameter.");
            }
            string key;
            string decoded;
            try
            {
                key = Uri.UnescapeDataString(part[..separator]);
                decoded = Uri.UnescapeDataString(part[(separator + 1)..]);
            }
            catch (UriFormatException exception)
            {
                throw new ProtocolCommandException($"The external command contains invalid escaping: {exception.Message}");
            }
            if (string.IsNullOrWhiteSpace(key)
                || key.Length > 32
                || decoded.Length > MaxUriLength
                || decoded.Any(char.IsControl)
                || !result.TryAdd(key, decoded))
            {
                throw Invalid("The external command contains an invalid or duplicate parameter.");
            }
        }
        return result;
    }

    private static string Identifier(IReadOnlyDictionary<string, string> parameters, string key)
    {
        var value = parameters[key];
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxIdentifierLength
            || value is "." or ".."
            || value.IndexOfAny(['/', '\\']) >= 0
            || value.Any(char.IsControl))
        {
            throw Invalid($"The '{key}' identifier is invalid.");
        }
        return value;
    }

    private static void RequireExactKeys(
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyCollection<string> expected)
    {
        if (parameters.Count != expected.Count
            || parameters.Keys.Any(key => !expected.Contains(key, StringComparer.OrdinalIgnoreCase)))
        {
            throw Invalid("The external command contains missing or unsupported parameters.");
        }
    }

    private static ProtocolCommandException Invalid(string message) => new(message);
}
