using System.Text;

namespace Crystalfly.Core.Logs;

public static class ApplicationLog
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();

    public static void Write(string path, string category, string message)
    {
        try
        {
            lock (Gate)
            {
                var fullPath = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(fullPath)
                    ?? throw new ArgumentException("Log path must include a directory.", nameof(path));
                Directory.CreateDirectory(directory);
                RotateIfNeeded(fullPath);
                File.AppendAllText(
                    fullPath,
                    $"{DateTimeOffset.UtcNow:O} [{category}] {message}{Environment.NewLine}",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void RotateIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length <= MaxFileBytes)
        {
            return;
        }

        var backup = path + ".1";
        File.Delete(backup);
        File.Move(path, backup);
    }
}
