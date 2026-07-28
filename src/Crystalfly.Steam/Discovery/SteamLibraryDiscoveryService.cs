using Microsoft.Win32;
using SteamKit2;
using System.Security;

namespace Crystalfly.Steam.Discovery;

public sealed record SteamLibraryCandidate(string GamePath, string LibraryPath, string DisplayName);

public interface ISteamInstallPathProvider
{
    IReadOnlyList<string> GetInstallPaths();
}

public sealed class WindowsSteamInstallPathProvider : ISteamInstallPathProvider
{
    public IReadOnlyList<string> GetInstallPaths()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        AddRegistryPath(paths, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath");
        AddRegistryPath(paths, RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Valve\Steam", "InstallPath");
        AddRegistryPath(paths, RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath");
        return paths.ToArray();
    }

    private static void AddRegistryPath(
        HashSet<string> paths,
        RegistryHive hive,
        RegistryView view,
        string keyPath,
        string valueName)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? key = baseKey.OpenSubKey(keyPath);
            if (key?.GetValue(valueName) is string path && !string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
        }
    }
}

public sealed class SteamLibraryDiscoveryService
{
    private const string AppManifestFileName = "appmanifest_367520.acf";
    private readonly ISteamInstallPathProvider _pathProvider;
    private readonly Func<string, Stream> _openRead;

    public SteamLibraryDiscoveryService(ISteamInstallPathProvider pathProvider)
        : this(pathProvider, File.OpenRead)
    {
    }

    internal SteamLibraryDiscoveryService(ISteamInstallPathProvider pathProvider, Func<string, Stream> openRead)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _openRead = openRead ?? throw new ArgumentNullException(nameof(openRead));
    }

    public Task<IReadOnlyList<SteamLibraryCandidate>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HashSet<string> libraries = new(StringComparer.OrdinalIgnoreCase);
        foreach (string steamRoot in _pathProvider.GetInstallPaths())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryNormalizePath(steamRoot, out string? normalizedRoot))
                continue;

            libraries.Add(normalizedRoot);
            AddConfiguredLibraries(normalizedRoot, libraries, cancellationToken);
        }

        List<SteamLibraryCandidate> candidates = [];
        HashSet<string> gamePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SteamLibraryCandidate? candidate = TryCreateCandidate(library);
            if (candidate is not null && gamePaths.Add(candidate.GamePath))
                candidates.Add(candidate);
        }

        candidates.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.GamePath, right.GamePath));
        return Task.FromResult<IReadOnlyList<SteamLibraryCandidate>>(candidates);
    }

    private void AddConfiguredLibraries(
        string steamRoot,
        HashSet<string> libraries,
        CancellationToken cancellationToken)
    {
        string libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        KeyValue? root = TryReadKeyValue(libraryFoldersPath);
        if (root is null)
            return;

        KeyValue section = string.Equals(root.Name, "libraryfolders", StringComparison.OrdinalIgnoreCase)
            ? root
            : root["libraryfolders"];
        foreach (KeyValue entry in section.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? configuredPath = entry["path"].AsString();
            if (string.IsNullOrWhiteSpace(configuredPath) && uint.TryParse(entry.Name, out _))
                configuredPath = entry.AsString();
            if (TryNormalizePath(configuredPath, out string? normalizedPath))
                libraries.Add(normalizedPath);
        }
    }

    private SteamLibraryCandidate? TryCreateCandidate(string library)
    {
        try
        {
            string manifestPath = Path.Combine(library, "steamapps", AppManifestFileName);
            KeyValue? root = TryReadKeyValue(manifestPath);
            if (root is null)
                return null;

            KeyValue appState = string.Equals(root.Name, "AppState", StringComparison.OrdinalIgnoreCase)
                ? root
                : root["AppState"];
            if (!string.Equals(appState["appid"].AsString(), "367520", StringComparison.Ordinal))
                return null;

            string? installDirectory = appState["installdir"].AsString();
            if (!IsSafeInstallDirectory(installDirectory))
                return null;

            string gamePath = Path.GetFullPath(Path.Combine(library, "steamapps", "common", installDirectory!));
            if (!File.Exists(Path.Combine(gamePath, "hollow_knight.exe")) ||
                !File.Exists(Path.Combine(gamePath, "hollow_knight_Data", "globalgamemanagers")))
            {
                return null;
            }

            string? displayName = appState["name"].AsString();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = Path.GetFileName(gamePath);

            return new SteamLibraryCandidate(gamePath, library, displayName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private KeyValue? TryReadKeyValue(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using Stream stream = _openRead(path);
            KeyValue value = new();
            return value.ReadAsText(stream) ? value : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryNormalizePath(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSafeInstallDirectory(string? installDirectory) =>
        !string.IsNullOrWhiteSpace(installDirectory) &&
        !Path.IsPathFullyQualified(installDirectory) &&
        !installDirectory.Contains(Path.DirectorySeparatorChar) &&
        !installDirectory.Contains(Path.AltDirectorySeparatorChar) &&
        !installDirectory.Contains(':', StringComparison.Ordinal) &&
        installDirectory is not "." and not "..";
}
