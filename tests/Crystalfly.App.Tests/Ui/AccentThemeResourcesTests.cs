using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Crystalfly.App.Theming;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed class AccentThemeResourcesTests
{
    public static TheoryData<string, bool> AccentCases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var color in AccentColorPalette.Presets.Append("#FDE68A"))
            {
                data.Add(color, false);
                data.Add(color, true);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AccentCases))]
    public void Build_preserves_accent_and_keeps_text_readable(string accent, bool dark)
    {
        var palette = AccentThemeResources.Build(accent, dark);

        Assert.Equal(Color.Parse(accent), palette.Accent);
        Assert.True(ContrastRatio(palette.OnAccent, palette.Accent) >= 4.5);
        Assert.True(ContrastRatio(palette.AccentText, palette.Surface) >= 4.5);
    }

    [Fact]
    public void Build_uses_fixed_light_and_dark_blends()
    {
        var light = AccentThemeResources.Build("#0F6CBD", dark: false);
        var dark = AccentThemeResources.Build("#0F6CBD", dark: true);

        Assert.Equal(Blend(Color.Parse("#0F6CBD"), Colors.Black, 0.12), light.Hover);
        Assert.Equal(Blend(Color.Parse("#0F6CBD"), Colors.White, 0.12), dark.Hover);
        Assert.Equal(Blend(Color.Parse("#0F6CBD"), Colors.White, 0.88), light.Soft);
        Assert.Equal(Blend(Color.Parse("#0F6CBD"), Color.Parse("#151617"), 0.76), dark.Soft);
    }

    [AvaloniaFact]
    public void Apply_updates_both_theme_resource_sets()
    {
        try
        {
            AccentThemeResources.Apply("#BE185D");

            Assert.Equal(Color.Parse("#BE185D"), ResourceColor("CfAccentBrush", ThemeVariant.Light));
            Assert.Equal(Color.Parse("#BE185D"), ResourceColor("CfAccentBrush", ThemeVariant.Dark));
            Assert.Equal(
                AccentThemeResources.Build("#BE185D", dark: false).Soft,
                ResourceColor("CfAccentSoftBrush", ThemeVariant.Light));
            Assert.Equal(
                AccentThemeResources.Build("#BE185D", dark: true).Soft,
                ResourceColor("CfAccentSoftBrush", ThemeVariant.Dark));
        }
        finally
        {
            AccentThemeResources.Apply(AccentColorPalette.DefaultColor);
        }
    }

    private static Color ResourceColor(string key, ThemeVariant variant)
    {
        Assert.True(Application.Current!.TryGetResource(key, variant, out var value));
        return Assert.IsType<SolidColorBrush>(value).Color;
    }

    private static Color Blend(Color source, Color target, double amount) => Color.FromRgb(
        (byte)Math.Round(source.R + ((target.R - source.R) * amount)),
        (byte)Math.Round(source.G + ((target.G - source.G) * amount)),
        (byte)Math.Round(source.B + ((target.B - source.B) * amount)));

    private static double ContrastRatio(Color foreground, Color background)
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

        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }
}
