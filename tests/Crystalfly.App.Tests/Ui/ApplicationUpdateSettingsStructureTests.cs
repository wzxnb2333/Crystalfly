using System.Xml.Linq;

namespace Crystalfly.App.Tests.Ui;

public sealed class ApplicationUpdateSettingsStructureTests
{
    [Fact]
    public void Settings_page_exposes_update_check_and_protocol_status()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml"));
        string markup = document.ToString();

        Assert.Contains("IsAutomaticUpdateCheckEnabled", markup, StringComparison.Ordinal);
        Assert.Contains("OnCheckForApplicationUpdatesClick", markup, StringComparison.Ordinal);
        Assert.Contains("ApplicationUpdateStatus", markup, StringComparison.Ordinal);
        Assert.Contains("ProtocolRegistrationStatus", markup, StringComparison.Ordinal);
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
