using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class OnboardingTests : IDisposable
{
    private readonly TestDirectory testDirectory = new();

    [Fact]
    public async Task First_run_requests_onboarding_once_and_completing_it_persists_the_flag()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var requested = 0;
        await using var viewModel = new MainViewModel(applicationDataRoot);
        viewModel.OnboardingRequested += () => requested++;

        await viewModel.InitializeAsync();

        Assert.Equal(1, requested);

        viewModel.CompleteOnboarding();

        // QueueSettingsSave is asynchronous; wait for the settings file to update.
        var settingsPath = Path.Combine(applicationDataRoot, "settings.json");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            var settings = await Crystalfly.Core.Configuration.CrystalflySettingsStore.LoadAsync(settingsPath);
            if (settings.OnboardingCompleted)
            {
                return;
            }
            await Task.Delay(10, timeout.Token);
        }

        Assert.Fail("OnboardingCompleted was not persisted.");
    }

    [Fact]
    public async Task Completed_onboarding_is_not_requested_again_on_next_startup()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var firstViewModel = new MainViewModel(applicationDataRoot);
        await firstViewModel.InitializeAsync();
        firstViewModel.CompleteOnboarding();
        await firstViewModel.DisposeAsync();

        var requested = 0;
        await using var secondViewModel = new MainViewModel(applicationDataRoot);
        secondViewModel.OnboardingRequested += () => requested++;

        await secondViewModel.InitializeAsync();

        Assert.Equal(0, requested);
    }

    [Fact]
    public async Task Onboarding_actions_navigate_to_the_target_workspace()
    {
        await using var viewModel = new MainViewModel(testDirectory.CreateDirectory("app-data"));

        viewModel.RunOnboardingAction("ManageLoader");
        Assert.Equal("Manage", viewModel.CurrentPage);
        Assert.Equal("Loader", viewModel.CurrentManageTab);

        viewModel.RunOnboardingAction("ModMarket");
        Assert.Equal("Downloads", viewModel.CurrentPage);
        Assert.Equal("ModMarket", viewModel.CurrentDownloadSection);

        viewModel.RunOnboardingAction("Speedrun");
        Assert.Equal("Speedrun", viewModel.CurrentPage);
    }

    public void Dispose()
    {
        testDirectory.Dispose();
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "Crystalfly.Tests",
            Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
