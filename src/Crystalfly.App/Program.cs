using Avalonia;
using Crystalfly.App.Runtime;

namespace Crystalfly.App;

internal sealed class Program
{
    private const string ApplicationMutexName = @"Local\Crystalfly.Application";
    private const string CommandPipeName = "Crystalfly.Application.Commands";
    internal const string ActivateMessage = "@activate";

    [STAThread]
    public static void Main(string[] args)
    {
        using var applicationMutex = new Mutex(
            initiallyOwned: false,
            ApplicationMutexName,
            out bool createdNew);
        if (!createdNew)
        {
            try
            {
                SingleInstanceCommandChannel.ForwardAsync(
                        CommandPipeName,
                        FindProtocolCommand(args) ?? ActivateMessage,
                        TimeSpan.FromSeconds(5))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception) when (exception is IOException
                or TimeoutException
                or OperationCanceledException
                or UnauthorizedAccessException
                or ArgumentException)
            {
            }
            return;
        }

        var commandChannel = new SingleInstanceCommandChannel(CommandPipeName);
        commandChannel.MessageReceived += App.EnqueueExternalMessage;
        commandChannel.Start();
        if (FindProtocolCommand(args) is { } startupCommand)
        {
            App.EnqueueExternalMessage(startupCommand);
        }
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            commandChannel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static string? FindProtocolCommand(IEnumerable<string> args) =>
        args.FirstOrDefault(argument =>
            argument.StartsWith("crystalfly:", StringComparison.OrdinalIgnoreCase));

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
