using System.Diagnostics;

namespace Crystalfly.App.Updates;

public enum ApplicationInstallationMode
{
    Installed,
    Portable
}

public sealed record ApplicationUpdateLaunchRequest(
    int ParentProcessId,
    ApplicationInstallationMode Mode,
    string AssetPath,
    long ExpectedSize,
    string ExpectedSha256,
    string TargetDirectory,
    string? RestartExecutablePath);

public static class ApplicationUpdateLauncher
{
    public static void CleanupExpiredAssets(
        string directory,
        DateTimeOffset now,
        TimeSpan maximumAge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (maximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAge));
        }
        if (!Directory.Exists(directory))
        {
            return;
        }

        DateTime threshold = now.UtcDateTime - maximumAge;
        foreach (string path in Directory.EnumerateFiles(directory))
        {
            string extension = Path.GetExtension(path);
            if (extension is not (".zip" or ".exe"))
            {
                continue;
            }
            try
            {
                if (File.GetLastWriteTimeUtc(path) < threshold)
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public static ProcessStartInfo CreateStartInfo(
        string updaterExecutablePath,
        ApplicationUpdateLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ParentProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        string updater = Path.GetFullPath(updaterExecutablePath);
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.TargetDirectory));
        string asset = Path.GetFullPath(request.AssetPath);
        string? restart = request.RestartExecutablePath is null
            ? null
            : Path.GetFullPath(request.RestartExecutablePath);
        if (restart is not null && !IsStrictDescendant(target, restart))
        {
            throw new ArgumentException("The restart executable must be inside the application directory.", nameof(request));
        }

        var startInfo = new ProcessStartInfo(updater)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(updater)!
        };
        startInfo.ArgumentList.Add("--parent-pid");
        startInfo.ArgumentList.Add(request.ParentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(request.Mode.ToString());
        startInfo.ArgumentList.Add("--asset");
        startInfo.ArgumentList.Add(asset);
        startInfo.ArgumentList.Add("--size");
        startInfo.ArgumentList.Add(request.ExpectedSize.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--sha256");
        startInfo.ArgumentList.Add(request.ExpectedSha256);
        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(target);
        if (restart is not null)
        {
            startInfo.ArgumentList.Add("--restart");
            startInfo.ArgumentList.Add(restart);
        }
        return startInfo;
    }

    public static Process LaunchFromTemporaryCopy(
        string bundledUpdaterPath,
        string temporaryRoot,
        ApplicationUpdateLaunchRequest request)
    {
        string source = Path.GetFullPath(bundledUpdaterPath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("The bundled update helper is missing.", source);
        }
        if (!File.Exists(request.AssetPath))
        {
            throw new FileNotFoundException("The verified update asset is missing.", request.AssetPath);
        }

        string updaterDirectory = Path.Combine(Path.GetFullPath(temporaryRoot), "updaters");
        Directory.CreateDirectory(updaterDirectory);
        DeleteOldHelpers(updaterDirectory);
        string stagedUpdater = Path.Combine(
            updaterDirectory,
            $"Crystalfly.Updater-{Guid.NewGuid():N}.exe");
        File.Copy(source, stagedUpdater, overwrite: false);
        try
        {
            return Process.Start(CreateStartInfo(stagedUpdater, request))
                ?? throw new InvalidOperationException("The update helper did not start.");
        }
        catch
        {
            File.Delete(stagedUpdater);
            throw;
        }
    }

    private static void DeleteOldHelpers(string directory)
    {
        DateTime threshold = DateTime.UtcNow.AddDays(-7);
        foreach (string path in Directory.EnumerateFiles(directory, "Crystalfly.Updater-*.exe"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < threshold)
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool IsStrictDescendant(string root, string candidate) =>
        candidate.StartsWith(
            root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
}
