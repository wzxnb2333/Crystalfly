using System.IO.Pipes;
using Crystalfly.App.Runtime;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Tests.Runtime;

public sealed class SingleInstanceCommandChannelTests
{
    [Fact]
    public void Forwarder_and_parser_share_one_utf8_byte_limit()
    {
        Assert.Equal(ProtocolCommandParser.MaxUriBytes, SingleInstanceCommandChannel.MaxMessageBytes);
    }

    [Fact]
    public async Task ForwardAsync_delivers_one_protocol_command_to_the_primary_instance()
    {
        var pipeName = $"Crystalfly.Tests.{Guid.NewGuid():N}";
        await using var server = new SingleInstanceCommandChannel(pipeName);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += message => received.TrySetResult(message);
        server.Start();

        const string command = "crystalfly://modpack?code=AbCdEf123_-Z";
        await SingleInstanceCommandChannel.ForwardAsync(pipeName, command, TimeSpan.FromSeconds(5));

        Assert.Equal(command, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ForwardAsync_rejects_message_over_the_fixed_byte_limit()
    {
        var value = new string('x', SingleInstanceCommandChannel.MaxMessageBytes + 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SingleInstanceCommandChannel.ForwardAsync("unused", value, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Server_continues_accepting_after_each_completed_client()
    {
        var pipeName = $"Crystalfly.Tests.{Guid.NewGuid():N}";
        await using var server = new SingleInstanceCommandChannel(pipeName);
        var received = new List<string>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += message =>
        {
            lock (received)
            {
                received.Add(message);
                if (received.Count == 2)
                {
                    completed.TrySetResult();
                }
            }
        };
        server.Start();

        await SingleInstanceCommandChannel.ForwardAsync(pipeName, "first", TimeSpan.FromSeconds(5));
        await SingleInstanceCommandChannel.ForwardAsync(pipeName, "second", TimeSpan.FromSeconds(5));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["first", "second"], received);
    }

    [Fact]
    public async Task DisposeAsync_does_not_resume_on_the_calling_synchronization_context()
    {
        var pipeName = $"Crystalfly.Tests.{Guid.NewGuid():N}";
        var server = new SingleInstanceCommandChannel(pipeName);
        server.Start();
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var previousContext = SynchronizationContext.Current;
        var stoppedContext = new StoppedSynchronizationContext();
        ValueTask disposal;
        try
        {
            SynchronizationContext.SetSynchronizationContext(stoppedContext);
            disposal = server.DisposeAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        await disposal.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, stoppedContext.PostCount);
    }

    private sealed class StoppedSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            PostCount++;
        }
    }
}
