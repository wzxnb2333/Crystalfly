using System.Diagnostics;

namespace Crystalfly.Updater;

internal static class InstalledUpdateInstaller
{
    public static ProcessStartInfo CreateStartInfo(string installerPath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.GetFullPath(installerPath),
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(installerPath))!
        };
        startInfo.ArgumentList.Add("/VERYSILENT");
        startInfo.ArgumentList.Add("/SUPPRESSMSGBOXES");
        startInfo.ArgumentList.Add("/NORESTART");
        startInfo.ArgumentList.Add("/SP-");
        return startInfo;
    }

    public static async Task<int> RunAsync(string installerPath, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(installerPath);
        if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Installed update asset must be an existing installer executable.", nameof(installerPath));
        }

        using Process process = Process.Start(CreateStartInfo(fullPath))
            ?? throw new InvalidOperationException("Failed to start installer process.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}
