using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;

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
    public void Wizard_starts_on_the_first_step_and_walks_through_to_completion()
    {
        var viewModel = new OnboardingDialogViewModel(key => key);
        bool? closed = null;
        viewModel.RequestClose += (_, result) => closed = (bool?)result;

        string[] titles =
        [
            "OnboardingStepWelcomeTitle", "OnboardingStepImportTitle",
            "OnboardingStepSelectInstanceTitle", "OnboardingStepLoaderTitle",
            "OnboardingStepModsTitle", "OnboardingStepLaunchTitle",
            "OnboardingStepExtraTitle", "OnboardingStepFinishTitle"
        ];

        Assert.Equal(titles[0], viewModel.CurrentTitle);
        Assert.False(viewModel.CanGoBack);
        Assert.True(viewModel.CanGoNext);
        Assert.False(viewModel.IsLastStep);

        for (var step = 1; step < titles.Length; step++)
        {
            viewModel.NextCommand.Execute(null);
            Assert.Equal(titles[step], viewModel.CurrentTitle);
        }

        Assert.True(viewModel.IsLastStep);
        Assert.False(viewModel.CanGoNext);

        viewModel.NextCommand.Execute(null);
        Assert.Equal(true, closed);
    }

    [Fact]
    public void Wizard_back_and_skip_work_as_expected()
    {
        var viewModel = new OnboardingDialogViewModel(key => key);
        bool? closed = null;
        viewModel.RequestClose += (_, result) => closed = (bool?)result;

        viewModel.NextCommand.Execute(null);
        Assert.Equal("OnboardingStepImportTitle", viewModel.CurrentTitle);
        Assert.True(viewModel.CanGoBack);

        viewModel.BackCommand.Execute(null);
        Assert.Equal("OnboardingStepWelcomeTitle", viewModel.CurrentTitle);
        Assert.False(viewModel.CanGoBack);

        viewModel.SkipCommand.Execute(null);
        Assert.Equal(false, closed);
    }

    [Fact]
    public void Wizard_last_step_button_shows_finish_text()
    {
        var viewModel = new OnboardingDialogViewModel(key => key);
        for (var step = 1; step < 8; step++)
        {
            viewModel.NextCommand.Execute(null);
        }

        Assert.Equal("OnboardingFinish", viewModel.NextText);
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
