using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Tests.Runtime;

public sealed class ProtocolCommandParserTests
{
    [Theory]
    [InlineData("crystalfly://mod/download?instance=practice&id=hkmod%3ADebugMod", ProtocolCommandKind.DownloadMod)]
    [InlineData("crystalfly://mod/reinstall-all?instance=practice", ProtocolCommandKind.ReinstallAllMods)]
    [InlineData("crystalfly://app/reset-settings", ProtocolCommandKind.ResetApplicationSettings)]
    [InlineData("crystalfly://modlinks/official", ProtocolCommandKind.UseOfficialModLinks)]
    [InlineData("crystalfly://mod/settings/delete?instance=practice&id=hkmod%3ADebugMod", ProtocolCommandKind.DeleteModSettings)]
    [InlineData("crystalfly://mod/settings/delete-all?instance=practice", ProtocolCommandKind.DeleteAllModSettings)]
    [InlineData("crystalfly://instance/launch?id=practice", ProtocolCommandKind.LaunchInstance)]
    [InlineData("crystalfly://mod/open?instance=practice&id=hkmod%3ADebugMod", ProtocolCommandKind.OpenModLocation)]
    [InlineData("crystalfly://modpack?code=AbCdEf123_-Z", ProtocolCommandKind.ImportPresetShare)]
    public void Parse_accepts_supported_commands(string value, ProtocolCommandKind expectedKind)
    {
        ProtocolCommand command = ProtocolCommandParser.Parse(value);

        Assert.Equal(expectedKind, command.Kind);
    }

    [Fact]
    public void Parse_accepts_https_custom_modlinks_with_exact_binding()
    {
        ProtocolCommand command = ProtocolCommandParser.Parse(
            "crystalfly://modlinks/custom?url=https%3A%2F%2Fexample.com%2FModLinks.xml&build=1.5.78.11833&loader=modding-api-77");

        Assert.Equal(ProtocolCommandKind.UseCustomModLinks, command.Kind);
        Assert.Equal("https://example.com/ModLinks.xml", command.SourceUrl);
        Assert.Equal("1.5.78.11833", command.BuildId);
        Assert.Equal("modding-api-77", command.LoaderId);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("crystalfly://mod/download?instance=practice&id=one&id=two")]
    [InlineData("crystalfly://mod/download?instance=practice&id=mod&url=https%3A%2F%2Fevil.test%2Fpayload.zip")]
    [InlineData("crystalfly://instance/launch?id=practice&force=true")]
    [InlineData("crystalfly://mod/open?instance=..%2Foutside&id=mod")]
    [InlineData("crystalfly://modlinks/custom?url=http%3A%2F%2Fexample.com%2Flinks.xml&build=test&loader=loader")]
    [InlineData("crystalfly://modpack?code=short")]
    [InlineData("crystalfly://unknown/action")]
    public void Parse_rejects_unsupported_or_ambiguous_input(string value)
    {
        Assert.Throws<ProtocolCommandException>(() => ProtocolCommandParser.Parse(value));
    }

    [Fact]
    public void Parse_rejects_uri_over_the_fixed_length_limit()
    {
        string value = "crystalfly://instance/launch?id=" + new string('a', ProtocolCommandParser.MaxUriLength);

        Assert.Throws<ProtocolCommandException>(() => ProtocolCommandParser.Parse(value));
    }

    [Fact]
    public void Parse_rejects_uri_over_the_shared_utf8_byte_limit()
    {
        string source = "https://example.com/" + new string('中', 1400);
        string value = $"crystalfly://modlinks/custom?url={source}&build=test&loader=loader";

        Assert.True(value.Length < ProtocolCommandParser.MaxUriLength);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(value) > 4096);
        Assert.Throws<ProtocolCommandException>(() => ProtocolCommandParser.Parse(value));
    }
}
