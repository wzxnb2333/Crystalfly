using Microsoft.Win32;

namespace Crystalfly.App.Runtime;

public interface IProtocolRegistrationService
{
    bool IsRegistered(string executablePath);
}

public sealed class WindowsProtocolRegistrationService : IProtocolRegistrationService
{
    private const string CommandKeyPath = @"crystalfly\shell\open\command";

    public bool IsRegistered(string executablePath)
    {
        try
        {
            using RegistryKey? key = Registry.ClassesRoot.OpenSubKey(CommandKeyPath, writable: false);
            return key?.GetValue(null) is string command && CommandMatches(command, executablePath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return false;
        }
    }

    internal static string BuildExpectedCommand(string executablePath) =>
        $"\"{Path.GetFullPath(executablePath)}\" \"%1\"";

    internal static bool CommandMatches(string command, string executablePath) =>
        string.Equals(
            command.Trim(),
            BuildExpectedCommand(executablePath),
            StringComparison.OrdinalIgnoreCase);
}
