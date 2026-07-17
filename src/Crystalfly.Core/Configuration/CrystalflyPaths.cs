namespace Crystalfly.Core.Configuration;

public sealed record CrystalflyPaths(string ApplicationDataRoot, bool IsPortable)
{
    public static CrystalflyPaths Resolve(string executableDirectory, string localAppData)
    {
        executableDirectory = Path.GetFullPath(executableDirectory);
        localAppData = Path.GetFullPath(localAppData);
        var portable = File.Exists(Path.Combine(executableDirectory, "portable.flag"));
        return new CrystalflyPaths(
            portable
                ? Path.Combine(executableDirectory, "Data")
                : Path.Combine(localAppData, "Crystalfly"),
            portable);
    }

    public string GetVersionDataRoot(string versionRoot) =>
        Path.Combine(Path.GetFullPath(versionRoot), ".crystalfly");
}
