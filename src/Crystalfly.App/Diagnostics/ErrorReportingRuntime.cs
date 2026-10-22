using System.Reflection;
using Avalonia.Threading;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Diagnostics;

namespace Crystalfly.App.Diagnostics;

internal static class ErrorReportingRuntime
{
    // Only Program initializes the real service. View models and tests never start a cloud client implicitly.
    public static ErrorReportingService? Service { get; private set; }
    public static IErrorReporter Reporter => (IErrorReporter?)Service ?? NullErrorReporter.Instance;
    public static string Region => GetMetadata("ErrorReportingRegion");

    internal static string GetMetadata(string name) => typeof(ErrorReportingRuntime).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(item => item.Key == name)?.Value ?? string.Empty;

    public static void Start()
    {
        try
        {
            var paths = CrystalflyPaths.Resolve(AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            Start(paths.ApplicationDataRoot, GetMetadata("ErrorReportingDsn"));
        }
        catch { Stop(); }
    }

    internal static void Start(string dataRoot, string? dsn)
    {
        if (Service is not null) return;
        Service = new ErrorReportingService(dataRoot, dsn, ErrorReportingService.BuildRelease);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobserved;
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandled;
        try
        {
            var settings = CrystalflySettingsStore.LoadAsync(Path.Combine(dataRoot, "settings.json"))
                .GetAwaiter().GetResult();
            Service.Configure(settings.ErrorReportingEnabled, settings.OfflineMode);
        }
        catch (Exception exception)
        {
            Service.Configure(false, true, settingsReady: false);
            Service.Capture(exception, ErrorOperation.Settings);
        }
    }

    private static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception) Reporter.Capture(exception, ErrorOperation.BackgroundTask, fatal: true);
    }

    private static void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args) =>
        Reporter.Capture(args.Exception, ErrorOperation.BackgroundTask);

    private static void OnDispatcherUnhandled(object? sender, DispatcherUnhandledExceptionEventArgs args) =>
        Reporter.Capture(args.Exception, ErrorOperation.UserInterface, fatal: true);

    public static void Stop()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
        TaskScheduler.UnobservedTaskException -= OnUnobserved;
        Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandled;
        try { Service?.Dispose(); } catch { }
        Service = null;
    }
}
