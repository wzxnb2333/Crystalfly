namespace Crystalfly.App.Runtime;

internal static class ApplicationUpdateHealthHandshake
{
    private const string RecoveryDirectoryName = ".crystalfly-update-recovery";
    internal const string HealthFileEnvironmentVariable = "CRYSTALFLY_UPDATE_HEALTH_FILE";

    public static void SignalFromEnvironment() => Signal(
        Environment.GetEnvironmentVariable(HealthFileEnvironmentVariable),
        AppContext.BaseDirectory);

    internal static void Signal(string? healthFilePath, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(healthFilePath) || string.IsNullOrWhiteSpace(targetDirectory))
        {
            return;
        }

        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        string parent = Directory.GetParent(target)?.FullName ?? string.Empty;
        string recoveryRoot = Path.Combine(parent, RecoveryDirectoryName);
        string health = Path.GetFullPath(healthFilePath);
        string recoveryPrefix = Path.TrimEndingDirectorySeparator(recoveryRoot) + Path.DirectorySeparatorChar;
        if (!health.StartsWith(recoveryPrefix, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(health), "healthy", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(health)!);
        File.WriteAllText(health, "healthy");
    }
}
