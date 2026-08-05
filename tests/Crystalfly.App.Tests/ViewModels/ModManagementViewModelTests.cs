using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class ModManagementViewModelTests
{
    [Fact]
    public void Has_updates_available_aggregates_installed_mods()
    {
        var viewModel = CreateViewModel();
        viewModel.InstalledMods.Add(Installed("a", version: "1.0.0", manifestVersion: "1.1.0"));
        viewModel.InstalledMods.Add(Installed("b", version: "1.0.0", manifestVersion: "1.0.0"));

        Assert.True(viewModel.HasUpdatesAvailable);

        viewModel.CheckForUpdatesCommand.Execute(null);

        Assert.True(viewModel.HasUpdatesAvailable);
    }

    [Fact]
    public void Has_updates_available_is_false_when_nothing_can_update()
    {
        var viewModel = CreateViewModel();
        viewModel.InstalledMods.Add(Installed("a", version: "1.0.0", manifestVersion: "1.0.0"));
        viewModel.InstalledMods.Add(Installed("b", version: "1.0.0", manifestVersion: null));

        Assert.False(viewModel.HasUpdatesAvailable);
    }

    [Fact]
    public void Conflicts_are_surfaced_on_the_installed_mod_items()
    {
        var viewModel = CreateViewModel();
        viewModel.InstalledMods.Add(Installed("a", version: "1.0.0", modifiedFiles: ["hollow_knight_Data/Managed/Shared.dll"]));
        viewModel.InstalledMods.Add(Installed("b", version: "1.0.0", modifiedFiles: ["hollow_knight_Data/Managed/Shared.dll"]));
        viewModel.InstalledMods.Add(Installed("c", version: "1.0.0", modifiedFiles: ["hollow_knight_Data/Managed/Other.dll"]));

        viewModel.RefreshConflicts();

        var pair = Assert.Single(viewModel.Conflicts);
        Assert.Equal(("a", "b"), (pair.ModA, pair.ModB));
        Assert.Equal(["hollow_knight_Data/Managed/Shared.dll"], pair.OverlappingFiles);
        Assert.True(viewModel.InstalledMods[0].HasConflicts);
        Assert.True(viewModel.InstalledMods[1].HasConflicts);
        Assert.False(viewModel.InstalledMods[2].HasConflicts);
        Assert.Contains("Mod b", viewModel.InstalledMods[0].ConflictWithText, StringComparison.Ordinal);
        Assert.Contains("Mod a", viewModel.InstalledMods[1].ConflictWithText, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_properties_format_catalog_manifest_data()
    {
        var manifest = Manifest("a", "1.0.0") with
        {
            Authors = ["Alice", "Bob"],
            RepositoryUrl = "https://github.com/example/mod",
            Dependencies = ["lib-a", "lib-b"]
        };
        var item = new InstalledModItemViewModel(Receipt("a", "1.0.0"), manifest, static () => { });

        Assert.Equal("lib-a, lib-b", item.DependenciesText);
        Assert.Equal("Alice, Bob", item.AuthorsText);
        Assert.Equal("https://github.com/example/mod", item.RepositoryUrl);
        Assert.True(item.HasRepositoryUrl);
        Assert.False(item.HasLatestVersion);
        Assert.Equal(string.Empty, item.LatestVersionText);
    }

    [Fact]
    public void Detail_properties_are_empty_without_catalog_manifest()
    {
        var item = new InstalledModItemViewModel(Receipt("a", "1.0.0"), null, static () => { });

        Assert.Equal(string.Empty, item.DependenciesText);
        Assert.Equal(string.Empty, item.AuthorsText);
        Assert.Equal(string.Empty, item.RepositoryUrl);
        Assert.False(item.HasRepositoryUrl);
        Assert.Equal(string.Empty, item.InstallDateText);
        Assert.False(item.HasInstallDate);
    }

    [Fact]
    public void Latest_version_text_is_shown_when_update_is_available()
    {
        var item = new InstalledModItemViewModel(Receipt("a", "1.0.0"), Manifest("a", "1.1.0"), static () => { });

        Assert.True(item.HasUpdate);
        Assert.Equal("1.1.0", item.LatestVersionText);
        Assert.True(item.HasLatestVersion);
    }

    [Fact]
    public void Install_date_text_comes_from_newest_receipt_file_write_time()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-test", Guid.NewGuid().ToString("N"));
        try
        {
            var relativePath = "hollow_knight_Data/Managed/Mods/DebugMod/DebugMod.dll";
            var filePath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "installed");
            var installedAt = new DateTime(2026, 7, 20, 14, 30, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(filePath, installedAt);
            var receipt = Receipt("a", "1.0.0") with
            {
                Files =
                [
                    new InstalledFileReceipt { RelativePath = relativePath, Sha256 = new string('A', 64) }
                ]
            };

            var item = new InstalledModItemViewModel(
                receipt,
                null,
                static () => { },
                instanceRoot: root);

            Assert.Equal("2026-07-20 14:30", item.InstallDateText);
            Assert.True(item.HasInstallDate);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Modified_files_text_lists_health_modified_files()
    {
        var item = Installed("a", "1.0.0", modifiedFiles:
        [
            "hollow_knight_Data/Managed/Shared/First.dll",
            "hollow_knight_Data/Managed/Shared/Second.dll"
        ]);

        Assert.Equal(
            "hollow_knight_Data/Managed/Shared/First.dll"
            + Environment.NewLine
            + "hollow_knight_Data/Managed/Shared/Second.dll",
            item.ModifiedFilesText);
        Assert.True(item.HasModifiedFilesText);
    }

    [Fact]
    public void Conflicts_are_cleared_when_no_mods_overlap()
    {
        var viewModel = CreateViewModel();
        viewModel.InstalledMods.Add(Installed("a", version: "1.0.0", modifiedFiles: ["A.dll"]));
        viewModel.InstalledMods.Add(Installed("b", version: "1.0.0", modifiedFiles: ["B.dll"]));

        viewModel.RefreshConflicts();

        Assert.Empty(viewModel.Conflicts);
        Assert.All(viewModel.InstalledMods, item => Assert.False(item.HasConflicts));
    }

    private static ModManagementViewModel CreateViewModel()
    {
        var dependencies = new ModManagementDependencies(
            () => new GameCatalog(),
            () => new LocalizationViewModel(),
            _ => { },
            CancellationToken.None,
            operation => operation(NewInstance()),
            _ => throw new InvalidOperationException("Not used in this test"),
            _ => throw new InvalidOperationException("Not used in this test"),
            _ => throw new InvalidOperationException("Not used in this test"),
            (manifest, _) => new MarketModItemViewModel(
                manifest,
                null,
                new Dictionary<string, string>(),
                chinese: false),
            _ => null,
            _ => "installed",
            _ => "healthy",
            _ => { },
            () => NewInstance(),
            _ => Task.CompletedTask);
        return new ModManagementViewModel(dependencies);
    }

    private static InstanceRecord NewInstance() => new()
    {
        Id = "practice",
        Name = "Practice",
        BuildId = "1.5.78.11833",
        RootPath = Path.Combine(Path.GetTempPath(), "crystalfly-test", "practice"),
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static InstalledModItemViewModel Installed(
        string id,
        string version,
        string? manifestVersion = null,
        IReadOnlyList<string>? modifiedFiles = null)
    {
        var receipt = Receipt(id, version);
        var discovery = new ModDiscoveryEntry
        {
            Id = receipt.Id,
            Name = receipt.Name,
            LoaderId = receipt.LoaderId,
            InstallRoot = receipt.InstallRoot,
            Enabled = receipt.Enabled,
            Ownership = receipt.Ownership,
            Files = receipt.Files.Select(file => file.RelativePath).ToArray(),
            EntryFiles = receipt.EntryFiles
        };
        var manifest = manifestVersion is null
            ? null
            : new ModManifest
            {
                Id = id,
                Name = $"Mod {id}",
                Version = manifestVersion,
                DownloadUrl = $"https://example.invalid/{id}.zip",
                Sha256 = new string('A', 64),
                LoaderId = "modding-api"
            };
        return new InstalledModItemViewModel(
            discovery,
            receipt,
            new ModHealthReport
            {
                ModId = receipt.Id,
                Status = modifiedFiles is null ? ModHealthStatus.Healthy : ModHealthStatus.ModifiedFile,
                ModifiedFiles = modifiedFiles ?? []
            },
            manifest,
            static () => { });
    }

    private static InstalledModReceipt Receipt(string id, string version) => new()
    {
        Id = id,
        Name = $"Mod {id}",
        Version = version,
        LoaderId = "modding-api",
        InstallRoot = $"Mods/{id}",
        Enabled = true
    };

    private static ModManifest Manifest(string id, string version) => new()
    {
        Id = id,
        Name = $"Mod {id}",
        Version = version,
        DownloadUrl = $"https://example.invalid/{id}.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api"
    };
}
