using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Packages;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class DownloadQueueRenderingTests
{
    [AvaloniaFact]
    public async Task Closing_with_active_download_requires_confirmation()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-close", Guid.NewGuid().ToString("N"));
        var storePath = Path.Combine(root, "download-queue.json");
        var executor = new BlockingExecutor();
        var queue = new DownloadQueueService(
            storePath,
            executor,
            static () => false,
            TimeSpan.FromMilliseconds(10));
        var viewModel = new MainViewModel(
            root,
            null,
            null,
            null,
            null,
            queue);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        await queue.InitializeAsync();
        await queue.EnqueueAsync(new DownloadQueueGroup
        {
            Id = "active-group",
            DeduplicationKey = "practice:mod:feature",
            Name = "Feature",
            TargetInstanceId = "practice",
            TargetInstanceName = "Practice",
            TargetInstanceRoot = "C:\\Games\\Practice",
            CreatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new DownloadQueueItem
                {
                    Id = "active-item",
                    Name = "Feature",
                    PackageId = "feature",
                    State = DownloadQueueItemState.Pending,
                    TotalBytes = 100
                }
            ]
        });
        await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var attempt = 0; attempt < 50 && !viewModel.HasUnfinishedDownloads; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        try
        {
            window.Close();
            CustomDialogControl[] dialogs = [];
            for (var attempt = 0; attempt < 50 && dialogs.Length == 0; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialogs = window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray();
            }

            Assert.True(window.IsVisible);
            var dialog = Assert.Single(dialogs);
            var confirm = dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["Confirm"]);
            Assert.NotNull(confirm.Command);
            confirm.Command.Execute(confirm.CommandParameter);
            for (var attempt = 0; attempt < 100 && window.IsVisible; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Assert.False(window.IsVisible);

            var resumedExecutor = new CompletingExecutor();
            await using var resumedQueue = new DownloadQueueService(
                storePath,
                resumedExecutor,
                static () => false,
                TimeSpan.FromMilliseconds(10));
            await resumedQueue.InitializeAsync();
            await resumedQueue.WaitForIdleAsync();
            Assert.Equal(1, resumedExecutor.TransferCalls);
            Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(resumedQueue.Groups).State);
        }
        finally
        {
            if (window.IsVisible)
            {
                typeof(MainWindow)
                    .GetField("closeAfterDispose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(window, true);
                window.Close();
                await viewModel.DisposeAsync();
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Canceling_close_confirmation_keeps_window_open()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-close-cancel", Guid.NewGuid().ToString("N"));
        var executor = new BlockingExecutor();
        var queue = new DownloadQueueService(
            Path.Combine(root, "download-queue.json"),
            executor,
            static () => false,
            TimeSpan.FromMilliseconds(10));
        var viewModel = new MainViewModel(root, null, null, null, null, queue);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("cancel-close"));
        await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return viewModel.HasUnfinishedDownloads;
        });

        try
        {
            window.Close();
            var dialog = await WaitForDialogAsync(window);
            var cancel = dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == viewModel.Loc["Cancel"]);
            cancel.Command!.Execute(cancel.CommandParameter);
            await WaitUntilAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                return !window.GetVisualDescendants().OfType<CustomDialogControl>().Any();
            });

            Assert.True(window.IsVisible);
        }
        finally
        {
            typeof(MainWindow)
                .GetField("closeAfterDispose", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(window, true);
            window.Close();
            await viewModel.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task Failed_download_requires_close_confirmation()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-close-failed", Guid.NewGuid().ToString("N"));
        var queue = new DownloadQueueService(
            Path.Combine(root, "download-queue.json"),
            new FailingExecutor(),
            static () => false,
            TimeSpan.FromMilliseconds(10));
        var viewModel = new MainViewModel(root, null, null, null, null, queue);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("failed-close"));
        await queue.WaitForIdleAsync();
        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return viewModel.DownloadQueueGroups.SingleOrDefault()?.State
                == DownloadQueueGroupState.Failed;
        });

        try
        {
            window.Close();
            var dialog = await WaitForDialogAsync(window);

            Assert.True(window.IsVisible);
            Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.Text == viewModel.Loc["ConfirmCloseDownloadsTitle"]);
        }
        finally
        {
            typeof(MainWindow)
                .GetField("closeAfterDispose", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(window, true);
            window.Close();
            await viewModel.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public void Download_queue_is_a_third_download_section_with_compact_actions()
    {
        var viewModel = new MainViewModel(Path.Combine(
            Path.GetTempPath(),
            "crystalfly-ui",
            Guid.NewGuid().ToString("N")))
        {
            CurrentPage = "Downloads",
            CurrentDownloadSection = "DownloadQueue"
        };
        var queueGroup = new DownloadQueueGroupItemViewModel(
            new DownloadQueueGroup
            {
                Id = "group-1",
                Name = "Feature",
                TargetInstanceId = "practice",
                TargetInstanceName = "Practice",
                TargetInstanceRoot = "C:\\Games\\Practice",
                CreatedAt = DateTimeOffset.UtcNow,
                State = DownloadQueueGroupState.Running,
                Stage = "Downloading",
                CompletedBytes = 25,
                TotalBytes = 100,
                Items =
                [
                    new DownloadQueueItem
                    {
                        Id = "item-1",
                        Name = "Library",
                        Version = "1.0.0",
                        State = DownloadQueueItemState.Transferring,
                        Stage = "Downloading",
                        CompletedBytes = 25,
                        TotalBytes = 100
                    }
                ]
            },
            viewModel.Loc);

        var window = new MainWindow { Width = 900, Height = 600, DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        viewModel.DownloadQueueGroups.Add(queueGroup);
        Dispatcher.UIThread.RunJobs();

        try
        {
            var railButtons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible
                    && button.Classes.Contains("cfp-local-nav"))
                .ToArray();
            Assert.Equal(3, railButtons.Length);
            Assert.Contains(railButtons, button =>
                AutomationProperties.GetName(button) == viewModel.Loc["DownloadQueue"]);
            var texts = window.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Assert.Contains(texts, text => text.IsEffectivelyVisible && text.Text == "Feature");
            Assert.Contains(texts, text => text.Text == viewModel.Loc["QueueDetails"]);

            var cancel = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.IsEffectivelyVisible
                    && AutomationProperties.GetName(button) == viewModel.Loc["Cancel"]);
            Assert.InRange(cancel.Bounds.Width, 43.5, 44.5);
            Assert.InRange(cancel.Bounds.Height, 43.5, 44.5);
        }
        finally
        {
            typeof(MainWindow)
                .GetField("closeAfterDispose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(window, true);
            window.Close();
        }
    }

    private sealed class BlockingExecutor : IDownloadQueueExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool RequiresGameExit(DownloadQueueItem item) => false;

        public bool IsTransient(Exception exception) => false;

        public async Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken)
        {
            await networkGate.WaitAsync(cancellationToken);
            try
            {
                Started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                networkGate.Release();
            }
        }

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CompletingExecutor : IDownloadQueueExecutor
    {
        private int transferCalls;

        public int TransferCalls => Volatile.Read(ref transferCalls);

        public bool RequiresGameExit(DownloadQueueItem item) => false;

        public bool IsTransient(Exception exception) => false;

        public async Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken)
        {
            await networkGate.WaitAsync(cancellationToken);
            try
            {
                Interlocked.Increment(ref transferCalls);
            }
            finally
            {
                networkGate.Release();
            }
        }

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingExecutor : IDownloadQueueExecutor
    {
        public bool RequiresGameExit(DownloadQueueItem item) => false;

        public bool IsTransient(Exception exception) => false;

        public async Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken)
        {
            await networkGate.WaitAsync(cancellationToken);
            try
            {
                throw new InvalidDataException("Package is invalid.");
            }
            finally
            {
                networkGate.Release();
            }
        }

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static DownloadQueueGroup Group(string id) => new()
    {
        Id = id,
        DeduplicationKey = $"practice:mod:{id}",
        Name = "Feature",
        TargetInstanceId = "practice",
        TargetInstanceName = "Practice",
        TargetInstanceRoot = "C:\\Games\\Practice",
        CreatedAt = DateTimeOffset.UtcNow,
        Items =
        [
            new DownloadQueueItem
            {
                Id = $"{id}-item",
                Name = "Feature",
                PackageId = "feature",
                State = DownloadQueueItemState.Pending,
                TotalBytes = 100
            }
        ]
    };

    private static async Task<CustomDialogControl> WaitForDialogAsync(MainWindow window)
    {
        CustomDialogControl[] dialogs = [];
        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            dialogs = window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray();
            return dialogs.Length > 0;
        });
        return Assert.Single(dialogs);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
