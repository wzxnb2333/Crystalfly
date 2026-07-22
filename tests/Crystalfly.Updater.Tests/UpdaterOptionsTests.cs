namespace Crystalfly.Updater.Tests;

public sealed class UpdaterOptionsTests
{
    [Fact]
    public void Parse_accepts_required_arguments_and_optional_restart()
    {
        string[] args =
        [
            "--parent-pid", "42",
            "--mode", "Portable",
            "--asset", @"C:\updates\release.zip",
            "--size", "1234",
            "--sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            "--target", @"C:\Apps\Crystalfly",
            "--restart", @"C:\Apps\Crystalfly\Crystalfly.exe"
        ];

        UpdaterOptions options = UpdaterOptions.Parse(args);

        Assert.Equal(42, options.ParentProcessId);
        Assert.Equal(UpdateMode.Portable, options.Mode);
        Assert.Equal(Path.GetFullPath(@"C:\updates\release.zip"), options.AssetPath);
        Assert.Equal(1234, options.ExpectedSize);
        Assert.Equal(new string('A', 64), options.ExpectedSha256);
        Assert.Equal(Path.GetFullPath(@"C:\Apps\Crystalfly"), options.TargetDirectory);
        Assert.Equal(Path.GetFullPath(@"C:\Apps\Crystalfly\Crystalfly.exe"), options.RestartExecutablePath);
        Assert.Equal(TimeSpan.FromMinutes(2), options.ParentExitTimeout);
    }

    [Theory]
    [InlineData("--parent-pid", "0")]
    [InlineData("--mode", "Unknown")]
    [InlineData("--asset", "")]
    [InlineData("--size", "0")]
    [InlineData("--sha256", "invalid")]
    [InlineData("--target", "")]
    public void Parse_rejects_invalid_required_argument(string name, string value)
    {
        string[] args =
        [
            "--parent-pid", "42",
            "--mode", "Portable",
            "--asset", @"C:\updates\release.zip",
            "--size", "1234",
            "--sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            "--target", @"C:\Apps\Crystalfly"
        ];
        int index = Array.IndexOf(args, name);
        args[index + 1] = value;

        ArgumentException exception = Assert.Throws<ArgumentException>(() => UpdaterOptions.Parse(args));

        Assert.Contains(name, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_restart_outside_target_directory()
    {
        string[] args =
        [
            "--parent-pid", "42",
            "--mode", "Portable",
            "--asset", @"C:\updates\release.zip",
            "--size", "1234",
            "--sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            "--target", @"C:\Apps\Crystalfly",
            "--restart", @"C:\Other\tool.exe"
        ];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => UpdaterOptions.Parse(args));

        Assert.Contains("--restart", exception.Message, StringComparison.Ordinal);
    }
}
