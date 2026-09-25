using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Crystalfly.App.ViewModels;

namespace Crystalfly.App.Tests.Ui;

public sealed class FontRenderingTests
{
    private const string MixedText =
        "启动游戏 设置 下载队列 版本管理 模组 依赖 存档 更新 就绪，。！？《空洞骑士》 Crystalfly Steam 12.3 MiB/s";

    [AvaloniaFact]
    public void Default_typeface_resolves_to_the_bundled_Inter_font()
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(Typeface.Default, out var typeface));
        Assert.Equal("Inter", typeface.FamilyName);
    }

    [AvaloniaTheory]
    [InlineData(FontWeight.Normal)]
    [InlineData(FontWeight.SemiBold)]
    [InlineData(FontWeight.Bold)]
    public void Window_text_shapes_Chinese_and_Latin_without_missing_glyphs(FontWeight weight)
    {
        var text = new TextBlock
        {
            Text = MixedText,
            FontWeight = weight
        };
        var window = new Window { Content = text, Width = 1200, Height = 100 };
        window.Show();
        try
        {
            text.Measure(Size.Infinity);
            AssertNoMissingGlyphs(text.TextLayout);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("fonts:Inter#Inter")]
    [InlineData("Segoe UI")]
    [InlineData("Consolas")]
    [InlineData("Crystalfly Missing Font")]
    public void Explicit_fonts_fall_back_for_Chinese_text(string family)
    {
        using var layout = new TextLayout(MixedText, new Typeface(family), 14, Brushes.Black);
        AssertNoMissingGlyphs(layout);
    }

    [AvaloniaTheory]
    [InlineData("Chinese", FontWeight.Normal)]
    [InlineData("Chinese", FontWeight.SemiBold)]
    [InlineData("Chinese", FontWeight.Bold)]
    [InlineData("English", FontWeight.Normal)]
    [InlineData("English", FontWeight.SemiBold)]
    [InlineData("English", FontWeight.Bold)]
    public void All_localized_characters_have_glyphs(string catalogName, FontWeight weight)
    {
        var catalog = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
            typeof(LocalizationViewModel)
                .GetField(catalogName, BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null));
        var characters = new HashSet<Rune>();
        foreach (var value in catalog.Values)
        {
            foreach (var rune in value.EnumerateRunes())
            {
                if (!Rune.IsControl(rune) && !Rune.IsWhiteSpace(rune))
                {
                    characters.Add(rune);
                }
            }
        }
        Assert.NotEmpty(characters);
        var text = string.Concat(characters.OrderBy(rune => rune.Value).Select(rune => rune.ToString()));
        using var layout = new TextLayout(text, new Typeface(FontFamily.Default, weight: weight), 14, Brushes.Black);
        AssertNoMissingGlyphs(layout);
    }

    private static void AssertNoMissingGlyphs(TextLayout layout)
    {
        var runs = layout.TextLines
            .SelectMany(line => line.TextRuns)
            .OfType<ShapedTextRun>()
            .ToArray();
        Assert.NotEmpty(runs);
        Assert.All(runs, run => Assert.NotEmpty(run.GlyphRun.GlyphInfos));
        var missing = runs
            .Where(run => run.GlyphRun.GlyphInfos.Any(glyph => glyph.GlyphIndex == 0))
            .Select(run => $"{run.Text}: {run.GlyphRun.GlyphTypeface.FamilyName}");
        Assert.Empty(missing);
    }
}
