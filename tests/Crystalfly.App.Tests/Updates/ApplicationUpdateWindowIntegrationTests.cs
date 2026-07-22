namespace Crystalfly.App.Tests.Updates;

public sealed class ApplicationUpdateWindowIntegrationTests
{
    [Fact]
    public void MainWindow_checks_then_handles_update_later_and_skip_choices()
    {
        string code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));

        Assert.Contains("CheckForApplicationUpdateAsync(viewModel, force: false", code, StringComparison.Ordinal);
        Assert.Contains("CheckForApplicationUpdateAsync(viewModel, force: true", code, StringComparison.Ordinal);
        Assert.Contains("ApplicationUpdateDialogResult.Update", code, StringComparison.Ordinal);
        Assert.Contains("ApplicationUpdateDialogResult.SkipVersion", code, StringComparison.Ordinal);
        Assert.Contains("SkipApplicationUpdateAsync", code, StringComparison.Ordinal);
        Assert.Contains("StartAvailableApplicationUpdateAsync", code, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx")))
        {
            directory = directory.Parent;
        }
        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }
}
