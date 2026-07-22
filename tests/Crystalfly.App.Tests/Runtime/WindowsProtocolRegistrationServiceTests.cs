using Crystalfly.App.Runtime;

namespace Crystalfly.App.Tests.Runtime;

public sealed class WindowsProtocolRegistrationServiceTests
{
    [Fact]
    public void ExpectedCommand_quotes_executable_and_protocol_argument()
    {
        string executable = Path.GetFullPath(@"C:\Program Files\Crystalfly\Crystalfly.App.exe");

        string command = WindowsProtocolRegistrationService.BuildExpectedCommand(executable);

        Assert.Equal($"\"{executable}\" \"%1\"", command);
        Assert.True(WindowsProtocolRegistrationService.CommandMatches(command, executable));
        Assert.False(WindowsProtocolRegistrationService.CommandMatches(
            "\"C:\\Other\\Crystalfly.App.exe\" \"%1\"",
            executable));
    }
}
