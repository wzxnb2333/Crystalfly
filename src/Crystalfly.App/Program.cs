using Avalonia;
using System;

namespace Crystalfly.App;

sealed class Program
{
    private const string ApplicationMutexName = @"Local\Crystalfly.Application";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var applicationMutex = new Mutex(
            initiallyOwned: false,
            ApplicationMutexName,
            out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
