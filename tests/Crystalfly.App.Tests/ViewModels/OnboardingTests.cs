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
    public void Task_center_selects_the_first_incomplete_task_and_can_complete()
    {
        var tasks = new[]
        {
            new OnboardingTaskItemViewModel("game", "Game", "Ready", "Done", "Open", "Versions", OnboardingTaskState.Done),
            new OnboardingTaskItemViewModel("loader", "Loader", "Install it", "Next", "Open", "ManageLoader", OnboardingTaskState.Current)
        };
        var viewModel = new OnboardingDialogViewModel(key => key, tasks);
        bool? closed = null;
        viewModel.RequestClose += (_, result) => closed = (bool?)result;

        Assert.Equal(tasks[1], viewModel.SelectedTask);
        Assert.Equal("Loader", viewModel.CurrentTitle);
        Assert.Equal("Install it", viewModel.CurrentDescription);
        Assert.True(viewModel.HasAction);

        viewModel.CompleteCommand.Execute(null);
        Assert.Equal(true, closed);
    }

    [Fact]
    public async Task Task_center_runs_the_selected_task_action()
    {
        var invoked = string.Empty;
        var tasks = new[]
        {
            new OnboardingTaskItemViewModel("market", "Market", "Open it", "Next", "Open market", "ModMarket", OnboardingTaskState.Current)
        };
        var viewModel = new OnboardingDialogViewModel(key => key, tasks, action =>
        {
            invoked = action;
            return Task.CompletedTask;
        });

        await viewModel.RunSelectedActionCommand.ExecuteAsync(null);

        Assert.Equal("ModMarket", invoked);
    }

    [Fact]
    public async Task Launch_onboarding_card_uses_the_next_incomplete_task()
    {
        var versionRoot = testDirectory.CreateDirectory("versions");
        var instanceRoot = testDirectory.CreateDirectory("versions", "1578");
        var record = new InstanceRecord
        {
            Id = "1578",
            Name = "1.5.78",
            BuildId = "1.5.78.11833",
            RootPath = instanceRoot
        };
        await using var viewModel = new MainViewModel(testDirectory.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0)
        };

        Assert.True(viewModel.ShouldShowLaunchOnboarding);
        Assert.Equal(viewModel.Loc["OnboardingTaskLoaderTitle"], viewModel.OnboardingNextTaskTitle);
        Assert.Contains("3", viewModel.OnboardingProgressText, StringComparison.Ordinal);
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

    [Fact]
    public async Task Completing_onboarding_hides_the_launch_card_but_keeps_manual_reopen()
    {
        await using var viewModel = new MainViewModel(testDirectory.CreateDirectory("app-data"));

        Assert.True(viewModel.ShouldShowLaunchOnboarding);

        viewModel.CompleteOnboarding();

        Assert.True(viewModel.IsOnboardingCompleted);
        Assert.False(viewModel.ShouldShowLaunchOnboarding);
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
