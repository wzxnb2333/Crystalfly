using System.Globalization;
using System.Runtime.CompilerServices;

namespace Crystalfly.App.Tests;

internal static class TestCultureDefaults
{
    // UI tests assert Simplified Chinese labels. Pin the process culture so a
    // runner with an English system locale behaves like a developer machine
    // instead of resolving FollowSystem to English.
    [ModuleInitializer]
    internal static void Initialize()
    {
        var chinese = new CultureInfo("zh-CN");
        CultureInfo.DefaultThreadCurrentCulture = chinese;
        CultureInfo.DefaultThreadCurrentUICulture = chinese;
        CultureInfo.CurrentCulture = chinese;
        CultureInfo.CurrentUICulture = chinese;
    }
}
