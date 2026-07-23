namespace Crystalfly.Updater;

internal enum UpdateMode
{
    Installed,
    Portable
}

internal sealed record UpdaterOptions(
    int ParentProcessId,
    UpdateMode Mode,
    string AssetPath,
    long ExpectedSize,
    string ExpectedSha256,
    string TargetDirectory,
    string? RestartExecutablePath,
    TimeSpan ParentExitTimeout)
{
    private static readonly TimeSpan DefaultParentExitTimeout = TimeSpan.FromMinutes(2);

    public static UpdaterOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        Dictionary<string, string> values = ParsePairs(args);

        string parentPidText = Required(values, "--parent-pid");
        if (!int.TryParse(parentPidText, out int parentProcessId) || parentProcessId <= 0)
        {
            throw new ArgumentException("--parent-pid must be a positive integer.");
        }

        string modeText = Required(values, "--mode");
        if (!Enum.TryParse(modeText, ignoreCase: true, out UpdateMode mode))
        {
            throw new ArgumentException("--mode must be Installed or Portable.");
        }

        string assetPath = GetFullPath(Required(values, "--asset"), "--asset");
        if (!long.TryParse(
                Required(values, "--size"),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out long expectedSize)
            || expectedSize is <= 0 or > int.MaxValue)
        {
            throw new ArgumentException("--size must be a positive supported asset size.");
        }
        string expectedSha256 = Required(values, "--sha256");
        try
        {
            if (expectedSha256.Length != 64 || Convert.FromHexString(expectedSha256).Length != 32)
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("--sha256 must be a 64-character SHA-256 value.");
        }
        string targetDirectory = GetFullPath(Required(values, "--target"), "--target");
        string? restartExecutablePath = values.TryGetValue("--restart", out string? restart)
            ? GetFullPath(RequiredValue(restart, "--restart"), "--restart")
            : null;

        if (restartExecutablePath is not null && !PathSafety.IsStrictDescendant(targetDirectory, restartExecutablePath))
        {
            throw new ArgumentException("--restart must be inside --target.");
        }

        TimeSpan timeout = DefaultParentExitTimeout;
        if (values.TryGetValue("--timeout-seconds", out string? timeoutText))
        {
            if (!int.TryParse(timeoutText, out int timeoutSeconds) || timeoutSeconds <= 0)
            {
                throw new ArgumentException("--timeout-seconds must be a positive integer.");
            }

            timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        return new UpdaterOptions(
            parentProcessId,
            mode,
            assetPath,
            expectedSize,
            expectedSha256.ToUpperInvariant(),
            targetDirectory,
            restartExecutablePath,
            timeout);
    }

    private static Dictionary<string, string> ParsePairs(string[] args)
    {
        if (args.Length == 0 || args.Length % 2 != 0)
        {
            throw new ArgumentException("Arguments must be supplied as name/value pairs.");
        }

        HashSet<string> supported =
        [
            "--parent-pid",
            "--mode",
            "--asset",
            "--size",
            "--sha256",
            "--target",
            "--restart",
            "--timeout-seconds"
        ];
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < args.Length; index += 2)
        {
            string name = args[index];
            if (!supported.Contains(name))
            {
                throw new ArgumentException($"Unknown argument: {name}");
            }

            if (!values.TryAdd(name, args[index + 1]))
            {
                throw new ArgumentException($"Duplicate argument: {name}");
            }
        }

        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out string? value))
        {
            throw new ArgumentException($"Missing required argument: {name}");
        }

        return RequiredValue(value, name);
    }

    private static string RequiredValue(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must not be empty.");
        }

        return value;
    }

    private static string GetFullPath(string path, string name)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"{name} is not a valid path.", exception);
        }
    }
}
