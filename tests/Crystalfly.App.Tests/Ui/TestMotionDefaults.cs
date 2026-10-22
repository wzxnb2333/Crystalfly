using System.Runtime.CompilerServices;
using Crystalfly.App.Views;

namespace Crystalfly.App.Tests.Ui;

internal static class TestMotionDefaults
{
    // Headless CI runners report client-area animations as disabled, which would
    // suppress every entrance animation the UI tests assert on.
    [ModuleInitializer]
    internal static void Initialize() => MainWindow.ClientAreaAnimationsOverride = static () => true;
}
