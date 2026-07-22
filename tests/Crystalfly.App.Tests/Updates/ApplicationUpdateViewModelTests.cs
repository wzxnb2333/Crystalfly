using Crystalfly.App.Updates;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Updates;

namespace Crystalfly.App.Tests.Updates;

public sealed class ApplicationUpdateViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly-update-view-model-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CheckApplicationUpdateAsync_exposes_update_and_persists_check_time()
    {
        DateTimeOffset checkedAt = new(2026, 7, 23, 8, 0, 0, TimeSpan.Zero);
        UpdateManifest manifest = Manifest("0.6.1");
        await using var viewModel = CreateViewModel((settings, force, _) =>
        {
            Assert.True(settings.CheckForUpdates);
            Assert.False(force);
            return Task.FromResult(new ApplicationUpdateCheckResult(
                ApplicationUpdateCheckStatus.UpdateAvailable,
                manifest,
                checkedAt));
        });

        ApplicationUpdateCheckResult result = await viewModel.CheckApplicationUpdateAsync(force: false);
        await viewModel.FlushSettingsAsync();
        CrystalflySettings saved = await CrystalflySettingsStore.LoadAsync(
            Path.Combine(root, "settings.json"));

        Assert.Equal(ApplicationUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Same(manifest, viewModel.AvailableApplicationUpdate);
        Assert.Equal(checkedAt, saved.LastUpdateCheckAt);
    }

    [Fact]
    public async Task SkipApplicationUpdateAsync_persists_version_and_clears_prompt()
    {
        UpdateManifest manifest = Manifest("0.6.1");
        await using var viewModel = CreateViewModel((_, _, _) => Task.FromResult(
            new ApplicationUpdateCheckResult(
                ApplicationUpdateCheckStatus.UpdateAvailable,
                manifest,
                DateTimeOffset.UtcNow)));
        await viewModel.CheckApplicationUpdateAsync(force: true);

        await viewModel.SkipApplicationUpdateAsync();
        await viewModel.FlushSettingsAsync();
        CrystalflySettings saved = await CrystalflySettingsStore.LoadAsync(
            Path.Combine(root, "settings.json"));

        Assert.Equal("0.6.1", saved.SkippedUpdateVersion);
        Assert.Null(viewModel.AvailableApplicationUpdate);
    }

    private MainViewModel CreateViewModel(
        Func<CrystalflySettings, bool, CancellationToken, Task<ApplicationUpdateCheckResult>> check) =>
        new(
            root,
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            applicationUpdateCheckOverride: check);

    private static UpdateManifest Manifest(string version) => new()
    {
        Channel = "stable",
        Version = version,
        PublishedAt = DateTimeOffset.UtcNow,
        NotesMarkdown = "notes",
        Assets = []
    };

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
