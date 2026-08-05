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
        string updateHandlers = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.SettingsHandlers.cs"));
        Assert.Contains("CheckForApplicationUpdateAsync(viewModel, force: true", updateHandlers, StringComparison.Ordinal);
        Assert.Contains("ApplicationUpdateDialogResult.Update", updateHandlers, StringComparison.Ordinal);
        Assert.Contains("ApplicationUpdateDialogResult.SkipVersion", updateHandlers, StringComparison.Ordinal);
        Assert.Contains("SkipApplicationUpdateAsync", updateHandlers, StringComparison.Ordinal);
        Assert.Contains("StartAvailableApplicationUpdateAsync", updateHandlers, StringComparison.Ordinal);
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
