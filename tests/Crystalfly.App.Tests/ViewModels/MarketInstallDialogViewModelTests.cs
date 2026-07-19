using System.Reflection;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class MarketInstallDialogViewModelTests
{
    [Fact]
    public async Task Late_plan_from_previous_target_does_not_replace_current_target_plan()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-dialog", Guid.NewGuid().ToString("N"));
        await using var main = new MainViewModel(Path.Combine(root, "app-data"));
        var firstTarget = Target("first", "First");
        var secondTarget = Target("second", "Second");
        main.MarketInstallTargets.Add(firstTarget);
        main.MarketInstallTargets.Add(secondTarget);
        main.SelectedMarketInstallTarget = firstTarget;
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstPlan = new TaskCompletionSource<ModInstallPlan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPlan = new TaskCompletionSource<ModInstallPlan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var dialog = new MarketInstallDialogViewModel(
            main,
            "Feature",
            async (target, cancellationToken) =>
            {
                if (ReferenceEquals(target, firstTarget))
                {
                    firstStarted.SetResult();
                    return await firstPlan.Task.WaitAsync(cancellationToken);
                }
                secondStarted.SetResult();
                return await secondPlan.Task.WaitAsync(cancellationToken);
            });

        var firstLoad = dialog.LoadPlanAsync();
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dialog.Targets[1].IsSelected = true;
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        secondPlan.SetResult(Plan("second", "Second loader"));
        await WaitUntilAsync(() => dialog.PlanItems.SingleOrDefault()?.PrimaryName == "Second loader");

        firstPlan.SetResult(Plan("first", "First loader"));
        await firstLoad.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Second loader", Assert.Single(dialog.PlanItems).PrimaryName);
        Assert.Same(secondTarget, main.SelectedMarketInstallTarget);

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Selected_target_loads_loader_dependencies_and_main_mod_plan()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-dialog", Guid.NewGuid().ToString("N"));
        var instanceRoot = Path.Combine(root, "versions", "practice");
        var managedRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed");
        var stateRoot = Path.Combine(root, "versions", ".crystalfly", "instances", "practice");
        Directory.CreateDirectory(managedRoot);
        Directory.CreateDirectory(stateRoot);
        var loaderFile = Path.Combine(managedRoot, "MMHOOK_Assembly-CSharp.dll");
        await File.WriteAllTextAsync(loaderFile, "loader");
        await AtomicJsonStore.WriteAsync(
            Path.Combine(stateRoot, "loader.json"),
            new InstalledPackageReceipt
            {
                PackageId = "modding-api-77",
                LoaderState = LoaderState.ModdingApi,
                IsVerified = true,
                Files =
                [
                    new InstalledFileReceipt
                    {
                        RelativePath = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll",
                        Sha256 = FileSha256(loaderFile)
                    }
                ]
            });

        var dependency = Mod("library", "Library", []);
        var mod = Mod("feature", "Feature", [dependency.Id]);
        await using var main = new MainViewModel(Path.Combine(root, "app-data"))
        {
            VersionRoot = Path.Combine(root, "versions"),
            SelectedMarketMod = mod
        };
        SetCatalog(main, new GameCatalog { Mods = [dependency, mod] });
        main.Instances.Add(new InstanceItemViewModel(
            new InstanceRecord
            {
                Id = "practice",
                Name = "Practice",
                RootPath = instanceRoot,
                BuildId = "1.5.78.11833",
                CreatedAt = DateTimeOffset.UtcNow
            },
            "1.5.78.11833",
            "Modding API v77",
            0));
        await main.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);

        using var dialog = new MarketInstallDialogViewModel(main, "Feature");
        await dialog.LoadPlanAsync();

        Assert.Equal(3, dialog.PlanItems.Count);
        Assert.Equal("Loader", dialog.PlanItems[0].KindText);
        Assert.Equal("Library", dialog.PlanItems[1].PrimaryName);
        Assert.Equal("Feature", dialog.PlanItems[2].PrimaryName);
        Assert.True(dialog.PlanItems[0].IsSatisfied);
        Assert.True(dialog.CanInstall);

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ModManifest Mod(string id, string name, IReadOnlyList<string> dependencies) => new()
    {
        Id = id,
        Name = name,
        DisplayName = name,
        Version = "1.0.0",
        LoaderId = "modding-api-77",
        DownloadUrl = $"https://example.invalid/{id}.zip",
        Sha256 = new string('A', 64),
        SupportedBuildIds = ["1.5.78.11833"],
        Dependencies = dependencies
    };

    private static MarketInstallTargetViewModel Target(string id, string name) => new(
        new InstanceItemViewModel(
            new InstanceRecord
            {
                Id = id,
                Name = name,
                RootPath = $"C:\\versions\\{id}",
                BuildId = "1.5.78.11833",
                CreatedAt = DateTimeOffset.UtcNow
            },
            "1.5.78.11833",
            "Vanilla",
            0),
        "1.5.78.11833",
        "Vanilla",
        "Ready",
        IsAvailable: true,
        RequiresLoader: true);

    private static ModInstallPlan Plan(string instanceId, string loaderName) => new()
    {
        ModId = "feature",
        InstanceId = instanceId,
        InstanceName = instanceId,
        Items =
        [
            new ModInstallPlanItem
            {
                Kind = ModInstallPlanItemKind.Loader,
                State = ModInstallPlanItemState.NeedsInstall,
                Id = "modding-api-77",
                Name = loaderName,
                Version = "77",
                LoaderId = "modding-api-77",
                Reason = "Loader will be installed."
            }
        ]
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static void SetCatalog(MainViewModel viewModel, GameCatalog catalog) =>
        typeof(MainViewModel)
            .GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, catalog);

    private static string FileSha256(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
}
