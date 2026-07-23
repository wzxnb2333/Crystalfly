using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Runtime;

public sealed class SingleInstanceCommandChannel : IAsyncDisposable
{
    public const int MaxMessageBytes = ProtocolCommandParser.MaxUriBytes;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly string pipeName;
    private readonly CancellationTokenSource lifetime = new();
    private Task? serverTask;
    private int started;

    public SingleInstanceCommandChannel(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if (pipeName.Length > 256 || pipeName.IndexOfAny(['\\', '/']) >= 0)
        {
            throw new ArgumentException("The pipe name is invalid.", nameof(pipeName));
        }
        this.pipeName = pipeName;
    }

    public event Action<string>? MessageReceived;

    public void Start()
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            throw new InvalidOperationException("The command channel has already started.");
        }
        serverTask = RunServerAsync(lifetime.Token);
    }

    public static async Task ForwardAsync(
        string pipeName,
        string message,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        var payload = Utf8.GetBytes(message);
        if (payload.Length > MaxMessageBytes)
        {
            throw new ArgumentException(
                $"A forwarded command cannot exceed {MaxMessageBytes} UTF-8 bytes.",
                nameof(message));
        }

        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(linked.Token);
        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
        await client.WriteAsync(length, linked.Token);
        await client.WriteAsync(payload, linked.Token);
        await client.FlushAsync(linked.Token);
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);
                var lengthBuffer = new byte[sizeof(int)];
                await server.ReadExactlyAsync(lengthBuffer, cancellationToken);
                var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
                if (length is <= 0 or > MaxMessageBytes)
                {
                    continue;
                }
                var payload = new byte[length];
                await server.ReadExactlyAsync(payload, cancellationToken);
                string message;
                try
                {
                    message = Utf8.GetString(payload);
                }
                catch (DecoderFallbackException)
                {
                    continue;
                }
                foreach (var handler in MessageReceived?.GetInvocationList()
                             .Cast<Action<string>>() ?? [])
                {
                    try
                    {
                        handler(message);
                    }
                    catch
                    {
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (serverTask is not null)
        {
            await serverTask;
        }
        lifetime.Dispose();
    }
}
