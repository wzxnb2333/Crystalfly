using Crystalfly.App.ViewModels;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Tests.Runtime;

public sealed class ProtocolCommandIntegrationTests
{
    [Fact]
    public async Task Summary_names_the_action_instance_and_mod_before_confirmation()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(root);
        viewModel.Instances.Add(new InstanceItemViewModel(
            new InstanceRecord
            {
                Id = "practice",
                Name = "Practice 1.5.78",
                RootPath = Path.Combine(root, "practice"),
                BuildId = "1.5.78.11833",
                CreatedAt = DateTimeOffset.UtcNow
            },
            "1.5.78.11833",
            "Modding API v77",
            0));
        var command = ProtocolCommandParser.Parse(
            "crystalfly://mod/download?instance=practice&id=hkmod%3ADebugMod");

        string summary = viewModel.DescribeProtocolCommand(command);

        Assert.Contains(viewModel.Loc["ProtocolDownloadMod"], summary, StringComparison.Ordinal);
        Assert.Contains("Practice 1.5.78", summary, StringComparison.Ordinal);
        Assert.Contains("hkmod:DebugMod", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_location_is_the_only_supported_command_without_confirmation()
    {
        var open = ProtocolCommandParser.Parse(
            "crystalfly://mod/open?instance=practice&id=hkmod%3ADebugMod");
        var launch = ProtocolCommandParser.Parse(
            "crystalfly://instance/launch?id=practice");

        Assert.False(open.RequiresConfirmation);
        Assert.True(launch.RequiresConfirmation);
    }

    [Fact]
    public async Task Modpack_confirmation_freezes_and_displays_the_selected_target_instance()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(root);
        var first = Instance(root, "practice", "Practice");
        var second = Instance(root, "race", "Race");
        viewModel.Instances.Add(first);
        viewModel.Instances.Add(second);
        viewModel.SelectedInstance = first;

        ProtocolCommand prepared = viewModel.PrepareProtocolCommand(ProtocolCommandParser.Parse(
            "crystalfly://modpack?code=AbCdEf123_-Z"));
        viewModel.SelectedInstance = second;
        string summary = viewModel.DescribeProtocolCommand(prepared);

        Assert.Equal("practice", prepared.InstanceId);
        Assert.Contains("Practice", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Race", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Protocol_lifecycle_gate_rejects_commands_while_busy_or_game_is_running()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(root);
        var command = ProtocolCommandParser.Parse("crystalfly://app/reset-settings");

        viewModel.IsBusy = true;
        Assert.False(viewModel.CanExecuteProtocolCommand(command, out string busyReason));
        Assert.NotEmpty(busyReason);

        viewModel.IsBusy = false;
        viewModel.IsGameRunning = true;
        Assert.False(viewModel.CanExecuteProtocolCommand(command, out string runningReason));
        Assert.NotEmpty(runningReason);
    }

    [Fact]
    public async Task Protocol_lifecycle_gate_blocks_source_or_settings_reset_with_unfinished_downloads()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(root);
        viewModel.DownloadQueueGroups.Add(new DownloadQueueGroupItemViewModel(
            new DownloadQueueGroup
            {
                Id = "pending",
                Name = "Pending",
                State = DownloadQueueGroupState.Pending
            },
            viewModel.Loc));

        Assert.False(viewModel.CanExecuteProtocolCommand(
            ProtocolCommandParser.Parse("crystalfly://app/reset-settings"),
            out string reason));
        Assert.Contains(viewModel.Loc["ExternalCommandDownloadsActive"], reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Protocol_execution_exposes_an_exclusive_ui_mutation_state()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(root);
        var states = new List<bool>();
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MainViewModel.IsExternalCommandRunning))
            {
                states.Add(viewModel.IsExternalCommandRunning);
            }
        };

        await viewModel.ExecuteProtocolCommandAsync(ProtocolCommandParser.Parse(
            "crystalfly://app/reset-settings"));

        Assert.Equal([true, false], states);
        Assert.True(viewModel.CanNavigate);
    }

    [Fact]
    public async Task Protocol_execution_is_rejected_after_view_model_shutdown()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        var viewModel = new MainViewModel(root);
        await viewModel.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            viewModel.ExecuteProtocolCommandAsync(ProtocolCommandParser.Parse(
                "crystalfly://app/reset-settings")));
    }

    [Fact]
    public void Program_and_window_keep_forwarding_parsing_and_confirmation_in_one_flow()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Crystalfly.App", "Program.cs"));
        var window = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Crystalfly.App",
            "Views",
            "MainWindow.axaml.cs"));

        Assert.Contains("SingleInstanceCommandChannel.ForwardAsync", program, StringComparison.Ordinal);
        Assert.Contains("ProtocolCommandParser.Parse", window, StringComparison.Ordinal);
        Assert.Contains("command.RequiresConfirmation", window, StringComparison.Ordinal);
        Assert.Contains("ExecuteProtocolCommandAsync", window, StringComparison.Ordinal);
        var handlerStart = window.IndexOf("HandleExternalMessageAsync", StringComparison.Ordinal);
        var handlerEnd = window.IndexOf("private void OpenExternalUrl", handlerStart, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ForceLaunchGameCommand",
            window[handlerStart..handlerEnd],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reset_settings_clears_live_instance_selection_without_restart()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(root);
        var item = new InstanceItemViewModel(
            new InstanceRecord
            {
                Id = "practice",
                Name = "Practice",
                RootPath = Path.Combine(root, "practice"),
                BuildId = "1.5.78.11833",
                CreatedAt = DateTimeOffset.UtcNow
            },
            "1.5.78.11833",
            "Vanilla",
            0);
        viewModel.Instances.Add(item);
        viewModel.VisibleInstances.Add(item);
        viewModel.SelectedInstance = item;
        viewModel.VersionRoot = Path.Combine(root, "versions");
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            new InstalledModReceipt
            {
                Id = "local",
                Name = "Local",
                Version = "1.0.0",
                LoaderId = "modding-api-77",
                InstallRoot = "Mods/Local",
                Enabled = true
            },
            null,
            static () => { }));
        viewModel.Snapshots.Add(new NamedSnapshot
        {
            Id = "snapshot",
            Name = "Before reset",
            SourcePath = Path.Combine(root, "source"),
            SnapshotPath = Path.Combine(root, "snapshot"),
            Sha256 = new string('A', 64)
        });

        await viewModel.ExecuteProtocolCommandAsync(ProtocolCommandParser.Parse(
            "crystalfly://app/reset-settings"));

        Assert.Null(viewModel.SelectedInstance);
        Assert.Empty(viewModel.Instances);
        Assert.Empty(viewModel.VisibleInstances);
        Assert.Empty(viewModel.InstalledMods);
        Assert.Empty(viewModel.Snapshots);
        Assert.Equal(string.Empty, viewModel.VersionRoot);
        Assert.Equal("Launch", viewModel.CurrentPage);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx")))
        {
            directory = directory.Parent;
        }
        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }

    private static InstanceItemViewModel Instance(string root, string id, string name) => new(
        new InstanceRecord
        {
            Id = id,
            Name = name,
            RootPath = Path.Combine(root, id),
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        },
        "1.5.78.11833",
        "Modding API v77",
        0);
}
