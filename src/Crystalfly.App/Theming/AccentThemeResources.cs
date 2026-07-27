using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Theming;

internal sealed record AccentThemePalette(
    Color Surface,
    Color Accent,
    Color Hover,
    Color Soft,
    Color AccentText,
    Color OnAccent,
    Color SurfaceSelected,
    Color SurfaceSelectedHover);

internal static class AccentThemeResources
{
    private static readonly Color LightSurface = Color.Parse("#F1F1F3");
    private static readonly Color DarkSurface = Color.Parse("#242628");
    private static readonly Color DarkBase = Color.Parse("#151617");

    public static AccentThemePalette Build(string accentColor, bool dark)
    {
        var accent = Color.Parse(AccentColorPalette.Normalize(accentColor));
        var surface = dark ? DarkSurface : LightSurface;
        var direction = dark ? Colors.White : Colors.Black;
        return new AccentThemePalette(
            surface,
            accent,
            Blend(accent, direction, 0.12),
            Blend(accent, dark ? DarkBase : Colors.White, dark ? 0.76 : 0.88),
            EnsureContrast(accent, surface, direction),
            ContrastRatio(Colors.Black, accent) >= ContrastRatio(Colors.White, accent)
                ? Colors.Black
                : Colors.White,
            Blend(accent, dark ? DarkBase : Colors.White, dark ? 0.76 : 0.88),
            Blend(accent, dark ? DarkBase : Colors.White, dark ? 0.66 : 0.82));
    }

    public static void Apply(string accentColor)
    {
        if (Application.Current is null)
        {
            return;
        }

        Apply(ThemeVariant.Light, Build(accentColor, dark: false));
        Apply(ThemeVariant.Dark, Build(accentColor, dark: true));
    }

    private static void Apply(ThemeVariant variant, AccentThemePalette palette)
    {
        SetColor("CfAccentBrush", variant, palette.Accent);
        SetColor("CfAccentHoverBrush", variant, palette.Hover);
        SetColor("CfAccentSoftBrush", variant, palette.Soft);
        SetColor("CfAccentTextBrush", variant, palette.AccentText);
        SetColor("CfOnAccentBrush", variant, palette.OnAccent);
        SetColor("CfSurfaceSelectedBrush", variant, palette.SurfaceSelected);
        SetColor("CfSurfaceSelectedHoverBrush", variant, palette.SurfaceSelectedHover);
    }

    private static void SetColor(string key, ThemeVariant variant, Color color)
    {
        if (Application.Current!.TryGetResource(key, variant, out var value)
            && value is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private static Color EnsureContrast(Color color, Color background, Color direction)
    {
        if (ContrastRatio(color, background) >= 4.5)
        {
            return color;
        }

        for (var amount = 0.05; amount <= 1; amount += 0.05)
        {
            var candidate = Blend(color, direction, amount);
            if (ContrastRatio(candidate, background) >= 4.5)
            {
                return candidate;
            }
        }

        return direction;
    }

    private static Color Blend(Color source, Color target, double amount) => Color.FromRgb(
        (byte)Math.Round(source.R + ((target.R - source.R) * amount)),
        (byte)Math.Round(source.G + ((target.G - source.G) * amount)),
        (byte)Math.Round(source.B + ((target.B - source.B) * amount)));

    private static double ContrastRatio(Color first, Color second)
    {
        static double Luminance(Color color)
        {
            static double Linear(byte channel)
            {
                var value = channel / 255d;
                return value <= 0.04045
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Linear(color.R))
                + (0.7152 * Linear(color.G))
                + (0.0722 * Linear(color.B));
        }

        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05)
            / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }
}
