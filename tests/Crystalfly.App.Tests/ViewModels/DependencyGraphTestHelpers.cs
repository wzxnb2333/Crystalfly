using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

internal sealed class TempDirectory : IDisposable
{
    private TempDirectory(string directoryPath) => DirectoryPath = directoryPath;

    public string DirectoryPath { get; }

    public static TempDirectory Create()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"crystalfly-graph-vm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return new TempDirectory(directoryPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}

internal static class DependencyGraphTestHelpers
{
    public static DependencyGraphDependencies Dependencies(
        Func<string, string>? getLayoutPath = null,
        Func<string?>? getSelectedInstanceId = null) => new(
        () => new LocalizationViewModel(),
        _ => null,
        getLayoutPath ?? (_ => Path.Combine(Path.GetTempPath(), "crystalfly-test", "dependency-graph.layout.json")),
        getSelectedInstanceId ?? (() => null),
        _ => { });

    public static InstalledModItemViewModel Mod(string id, string[]? dependencies = null, bool enabled = true)
    {
        var receipt = new InstalledModReceipt
        {
            Id = id,
            Name = $"Mod {id}",
            Version = "1.0.0",
            LoaderId = "modding-api",
            InstallRoot = $"Mods/{id}",
            Enabled = enabled,
            Dependencies = dependencies ?? []
        };
        var discovery = new ModDiscoveryEntry
        {
            Id = receipt.Id,
            Name = receipt.Name,
            LoaderId = receipt.LoaderId,
            InstallRoot = receipt.InstallRoot,
            Enabled = receipt.Enabled,
            Ownership = ModOwnership.Managed,
            Files = receipt.Files.Select(file => file.RelativePath).ToArray(),
            EntryFiles = receipt.EntryFiles
        };
        return new InstalledModItemViewModel(
            discovery,
            receipt,
            new ModHealthReport { ModId = receipt.Id, Status = ModHealthStatus.Healthy },
            null,
            static () => { });
    }

    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Condition was not met within {timeoutMilliseconds} ms.");
            }

            await Task.Delay(10);
        }
    }
}
