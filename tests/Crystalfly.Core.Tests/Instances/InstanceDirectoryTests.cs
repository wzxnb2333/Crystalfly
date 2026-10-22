using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceDirectoryTests
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "crystalfly-instance-directory",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("name.")]
    [InlineData("name ")]
    [InlineData("CON")]
    [InlineData("NUL.txt")]
    [InlineData("COM1")]
    [InlineData("LPT9.log")]
    [InlineData("COM\u00B9")]
    [InlineData("COM\u00B9.txt")]
    [InlineData("COM\u00B2")]
    [InlineData("COM\u00B2.txt")]
    [InlineData("COM\u00B3")]
    [InlineData("COM\u00B3.txt")]
    [InlineData("LPT\u00B9")]
    [InlineData("LPT\u00B9.txt")]
    [InlineData("LPT\u00B2")]
    [InlineData("LPT\u00B2.txt")]
    [InlineData("LPT\u00B3")]
    [InlineData("LPT\u00B3.txt")]
    [InlineData(@"nested\child")]
    [InlineData("nested/child")]
    public void ResolveUnderRoot_rejects_invalid_windows_directory_names(string name) =>
        Assert.Throws<ArgumentException>(() => InstanceDirectory.ResolveUnderRoot(root, name));

    [Fact]
    public void ResolveUnderRoot_rejects_names_longer_than_255_utf16_code_units()
    {
        var name = new string('a', 256);

        Assert.Throws<ArgumentException>(() => InstanceDirectory.ResolveUnderRoot(root, name));
    }
}
