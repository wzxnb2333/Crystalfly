using Crystalfly.App.Services;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class ProtocolServiceTests
{
    [Fact]
    public void Parse_accepts_a_legal_download_command()
    {
        var service = CreateService();

        var command = service.Parse(
            "crystalfly://mod/download?instance=practice&id=hkmod%3ADebugMod");

        Assert.Equal(ProtocolCommandKind.DownloadMod, command.Kind);
        Assert.Equal("practice", command.InstanceId);
        Assert.Equal("hkmod:DebugMod", command.ModId);
    }

    [Fact]
    public void Parse_rejects_an_unsupported_route()
    {
        var service = CreateService();

        Assert.Throws<ProtocolCommandException>(() =>
            service.Parse("crystalfly://app/delete-everything"));
    }

    [Fact]
    public void Parse_rejects_a_non_crystalfly_scheme()
    {
        var service = CreateService();

        Assert.Throws<ProtocolCommandException>(() =>
            service.Parse("https://example.invalid/mod/download?instance=practice&id=hkmod%3ADebugMod"));
    }

    [Fact]
    public void Parse_rejects_missing_parameters()
    {
        var service = CreateService();

        Assert.Throws<ProtocolCommandException>(() =>
            service.Parse("crystalfly://mod/download?instance=practice"));
    }

    [Fact]
    public void Prepare_fills_instance_id_from_the_selected_instance()
    {
        var service = CreateService(selectedInstance: Instance("practice", "Practice"));

        var prepared = service.Prepare(service.Parse("crystalfly://modpack?code=AbCdEf123_-Z"));

        Assert.Equal("practice", prepared.InstanceId);
    }

    [Fact]
    public void Prepare_keeps_an_explicit_instance_id()
    {
        var service = CreateService();

        var prepared = service.Prepare(service.Parse(
            "crystalfly://mod/download?instance=race&id=hkmod%3ADebugMod"));

        Assert.Equal("race", prepared.InstanceId);
    }

    [Fact]
    public void Prepare_throws_when_no_instance_is_selected()
    {
        var service = CreateService();

        Assert.Throws<InvalidOperationException>(() =>
            service.Prepare(service.Parse("crystalfly://modpack?code=AbCdEf123_-Z")));
    }

    [Fact]
    public void CanExecute_rejects_while_busy()
    {
        var service = CreateService(isBusy: true);

        Assert.False(service.CanExecute(
            service.Parse("crystalfly://app/reset-settings"),
            out string reason));
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void CanExecute_rejects_while_the_game_is_running()
    {
        var service = CreateService(isGameRunning: true);

        Assert.False(service.CanExecute(
            service.Parse("crystalfly://app/reset-settings"),
            out string reason));
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void CanExecute_rejects_source_reset_with_unfinished_downloads()
    {
        var localization = CreateLocalization();
        var service = CreateService(loc: localization, hasUnfinishedDownloads: true);

        Assert.False(service.CanExecute(
            service.Parse("crystalfly://app/reset-settings"),
            out string reason));
        Assert.Equal(localization["ExternalCommandDownloadsActive"], reason);
    }

    [Fact]
    public void Describe_lists_the_action_instance_and_mod()
    {
        var localization = CreateLocalization();
        var service = CreateService(
            loc: localization,
            instances: [Instance("practice", "Practice 1.5.78")]);

        var summary = service.Describe(service.Parse(
            "crystalfly://mod/download?instance=practice&id=hkmod%3ADebugMod"));

        Assert.Contains(localization["ProtocolDownloadMod"], summary, StringComparison.Ordinal);
        Assert.Contains("Practice 1.5.78", summary, StringComparison.Ordinal);
        Assert.Contains("hkmod:DebugMod", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_serializes_commands_while_one_is_running()
    {
        var service = CreateService();
        var executed = new List<ProtocolCommandKind>();

        var first = service.ExecuteAsync(
            service.Parse("crystalfly://app/reset-settings"),
            (command, cancellationToken) =>
            {
                executed.Add(command.Kind);
                return Task.Delay(100, cancellationToken);
            });

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = service.ExecuteAsync(
                service.Parse("crystalfly://instance/launch?id=practice"),
                (command, cancellationToken) => Task.CompletedTask);
        });

        await first;
        Assert.Equal([ProtocolCommandKind.ResetApplicationSettings], executed);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_after_shutdown()
    {
        using var lifetime = new CancellationTokenSource();
        var service = CreateService(lifetimeCancellation: lifetime.Token);
        lifetime.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(
                service.Parse("crystalfly://app/reset-settings"),
                (command, cancellationToken) => Task.CompletedTask));
    }

    [Fact]
    public async Task ExecuteAsync_dispatches_the_command_kind_to_the_matching_target()
    {
        var service = CreateService();
        var executed = new List<ProtocolCommandKind>();
        var executor = new ProtocolCommandExecutor(
            DownloadMod: (command, cancellationToken) => Record(executed, ProtocolCommandKind.DownloadMod),
            ReinstallAllMods: (command, cancellationToken) => Record(executed, ProtocolCommandKind.ReinstallAllMods),
            ResetApplicationSettings: (command, cancellationToken) => Record(executed, ProtocolCommandKind.ResetApplicationSettings),
            ApplyModLinks: (command, cancellationToken) => Record(executed, command.Kind),
            DeleteModSettings: (command, cancellationToken) => Record(executed, command.Kind),
            LaunchInstance: (command, cancellationToken) => Record(executed, ProtocolCommandKind.LaunchInstance),
            OpenModLocation: (command, cancellationToken) => Record(executed, ProtocolCommandKind.OpenModLocation),
            ImportPresetShare: (command, cancellationToken) => Record(executed, ProtocolCommandKind.ImportPresetShare));

        await service.ExecuteAsync(
            service.Parse("crystalfly://app/reset-settings"),
            executor);
        await service.ExecuteAsync(
            service.Parse("crystalfly://instance/launch?id=practice"),
            executor);
        await service.ExecuteAsync(
            service.Parse("crystalfly://mod/settings/delete?instance=practice&id=hkmod%3ADebugMod"),
            executor);
        await service.ExecuteAsync(
            service.Parse("crystalfly://mod/settings/delete-all?instance=practice"),
            executor);
        await service.ExecuteAsync(
            service.Parse("crystalfly://mod/open?instance=practice&id=hkmod%3ADebugMod"),
            executor);

        Assert.Equal(
            [
                ProtocolCommandKind.ResetApplicationSettings,
                ProtocolCommandKind.LaunchInstance,
                ProtocolCommandKind.DeleteModSettings,
                ProtocolCommandKind.DeleteAllModSettings,
                ProtocolCommandKind.OpenModLocation
            ],
            executed);
    }

    private static Task Record(List<ProtocolCommandKind> executed, ProtocolCommandKind kind)
    {
        executed.Add(kind);
        return Task.CompletedTask;
    }

    private static ProtocolService CreateService(
        LocalizationViewModel? loc = null,
        InstanceItemViewModel? selectedInstance = null,
        IReadOnlyList<InstanceItemViewModel>? instances = null,
        GameCatalog? catalog = null,
        bool isBusy = false,
        bool isGameRunning = false,
        bool hasUnfinishedDownloads = false,
        CancellationToken lifetimeCancellation = default)
    {
        var localization = loc ?? CreateLocalization();
        return new ProtocolService(
            () => localization,
            () => selectedInstance,
            () => instances ?? [],
            () => catalog ?? new GameCatalog(),
            () => isBusy,
            () => isGameRunning,
            () => hasUnfinishedDownloads,
            lifetimeCancellation);
    }

    private static LocalizationViewModel CreateLocalization()
    {
        var localization = new LocalizationViewModel();
        localization.Apply(UiLanguage.English);
        return localization;
    }

    private static InstanceItemViewModel Instance(string id, string name) => new(
        new InstanceRecord
        {
            Id = id,
            Name = name,
            RootPath = Path.Combine(Path.GetTempPath(), id),
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        },
        "1.5.78.11833",
        "Modding API v77",
        0);
}
