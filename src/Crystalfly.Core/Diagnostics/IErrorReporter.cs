namespace Crystalfly.Core.Diagnostics;

/// <summary>Accepts exceptions and fixed operation identifiers, never user input.</summary>
public interface IErrorReporter
{
    void Capture(Exception exception, ErrorOperation operation, bool fatal = false);
}

public enum ErrorOperation
{
    Startup,
    UserInterface,
    BackgroundTask,
    CatalogRefresh,
    Download,
    Install,
    SaveRead,
    SaveWrite,
    Settings,
    InstanceOperation,
    Shutdown
}

public sealed class NullErrorReporter : IErrorReporter
{
    public static NullErrorReporter Instance { get; } = new();

    private NullErrorReporter() { }

    public void Capture(Exception exception, ErrorOperation operation, bool fatal = false) { }
}
