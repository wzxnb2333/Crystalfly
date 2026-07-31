using System.Text.Json;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class RuntimePatchesConfigurationTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Write_uses_the_four_exact_RuntimePatches_field_names()
    {
        string path = Path.Combine(root, RuntimePatchesConfiguration.FileName);
        var configuration = new RuntimePatchesConfiguration
        {
            ScreenShakeModifier = true,
            MiniSaveStates = true,
            FasterIntroSkip = true,
            TextMasher = true
        };

        await RuntimePatchesConfiguration.WriteAsync(path, configuration);

        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(
            ["ScreenShakeModifier", "MiniSaveStates", "FasterIntroSkip", "TextMasher"],
            document.RootElement.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(configuration, await RuntimePatchesConfiguration.ReadAsync(path));
    }

    [Theory]
    [InlineData("{\"screenShakeModifier\":false,\"MiniSaveStates\":false,\"FasterIntroSkip\":false,\"TextMasher\":false}")]
    [InlineData("{\"ScreenShakeModifier\":false,\"MiniSaveStates\":false,\"TextMasher\":false}")]
    [InlineData("{\"ScreenShakeModifier\":false,\"MiniSaveStates\":false,\"FasterIntroSkip\":false,\"TextMasher\":false,\"Extra\":false}")]
    [InlineData("[]")]
    [InlineData("not json")]
    public async Task Read_rejects_damaged_or_noncanonical_configuration(string json)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, RuntimePatchesConfiguration.FileName);
        await File.WriteAllTextAsync(path, json);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimePatchesConfiguration.ReadAsync(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
