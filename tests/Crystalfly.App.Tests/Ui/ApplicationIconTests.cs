using System.Buffers.Binary;

namespace Crystalfly.App.Tests.Ui;

public sealed class ApplicationIconTests
{
    [Fact]
    public void Application_uses_new_window_and_executable_icons()
    {
        var root = FindRepositoryRoot();
        var png = Path.Combine(root, "src", "Crystalfly.App", "Assets", "crystalfly-icon.png");
        var ico = Path.Combine(root, "src", "Crystalfly.App", "Assets", "crystalfly-icon.ico");
        Assert.True(File.Exists(png));
        Assert.True(File.Exists(ico));

        var project = File.ReadAllText(Path.Combine(root, "src", "Crystalfly.App", "Crystalfly.App.csproj"));
        var window = File.ReadAllText(Path.Combine(root, "src", "Crystalfly.App", "Views", "MainWindow.axaml"));
        Assert.Contains("<ApplicationIcon>Assets\\crystalfly-icon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("Icon=\"/Assets/crystalfly-icon.png\"", window, StringComparison.Ordinal);
    }

    [Fact]
    public void Executable_icon_contains_all_required_sizes()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Crystalfly.App",
            "Assets",
            "crystalfly-icon.ico");
        var bytes = File.ReadAllBytes(path);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        Assert.Equal(7, count);

        var sizes = Enumerable.Range(0, count)
            .Select(index => bytes[6 + (index * 16)] is 0 ? 256 : bytes[6 + (index * 16)])
            .Order()
            .ToArray();
        Assert.Equal([16, 24, 32, 48, 64, 128, 256], sizes);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Crystalfly.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
