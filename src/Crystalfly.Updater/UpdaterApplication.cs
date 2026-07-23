using System.Diagnostics;

namespace Crystalfly.Updater;

internal static class UpdaterApplication
{
    public static async Task<int> RunAsync(
        string[] args,
        Func<int, TimeSpan, CancellationToken, Task> waitForParent,
        Func<string, long, string, CancellationToken, Task<IDisposable>> verifyAsset,
        Func<string, CancellationToken, Task<int>> install,
        Func<string, string, CancellationToken, Task<PortableUpdateOperation?>> applyPortable,
        Action<string, string?> restart,
        Func<PortableUpdateOperation, CancellationToken, Task> completePortable,
        CancellationToken cancellationToken)
    {
        UpdaterOptions options = UpdaterOptions.Parse(args);
        try
        {
            await waitForParent(
                options.ParentProcessId,
                options.ParentExitTimeout,
                cancellationToken).ConfigureAwait(false);
            using IDisposable assetLease = await verifyAsset(
                options.AssetPath,
                options.ExpectedSize,
                options.ExpectedSha256,
                cancellationToken).ConfigureAwait(false);

            int exitCode;
            PortableUpdateOperation? portableOperation = null;
            if (options.Mode == UpdateMode.Installed)
            {
                exitCode = await install(options.AssetPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                portableOperation = await applyPortable(
                    options.AssetPath,
                    options.TargetDirectory,
                    cancellationToken).ConfigureAwait(false);
                exitCode = 0;
            }

            if (exitCode == 0 && options.RestartExecutablePath is { } restartPath)
            {
                restart(restartPath, portableOperation?.HealthFilePath);
                if (portableOperation is not null)
                {
                    await completePortable(portableOperation, cancellationToken).ConfigureAwait(false);
                }
            }
            return exitCode;
        }
        finally
        {
            TryDeleteAsset(options.AssetPath);
        }
    }

    private static void TryDeleteAsset(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void Restart(string executablePath, string? healthFilePath)
    {
        string fullPath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Updated application executable was not found.", fullPath);
        }
        var startInfo = new ProcessStartInfo(fullPath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(fullPath)!
        };
        if (!string.IsNullOrWhiteSpace(healthFilePath))
        {
            startInfo.Environment["CRYSTALFLY_UPDATE_HEALTH_FILE"] = healthFilePath;
        }
        Process.Start(startInfo);
    }
}
