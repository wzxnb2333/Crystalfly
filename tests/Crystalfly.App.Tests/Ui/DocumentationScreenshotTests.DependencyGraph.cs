using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.App.Views.Controls;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaTheory]
    [InlineData(1100, 720, UiLanguage.SimplifiedChinese, true)]
    [InlineData(900, 600, UiLanguage.SimplifiedChinese, true)]
    [InlineData(900, 600, UiLanguage.English, true)]
    [InlineData(1100, 720, UiLanguage.SimplifiedChinese, false)]
    [InlineData(900, 600, UiLanguage.SimplifiedChinese, false)]
    [InlineData(900, 600, UiLanguage.English, false)]
    public async Task Hk_dependency_graph_uses_full_canvas_with_a_compact_inspector(int width, int height, UiLanguage language, bool dark)
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.DependencyGraph);
        var vm = fixture.ViewModel;
        vm.Loc.Apply(language);
        vm.ModManagement.InstalledMods.Clear();
        foreach (var (name, prerequisites) in new (string, string[])[]
        {
            ("FrogCore", []), ("Vasi", []), ("SFCore", []), ("Satchel", []),
            ("Pale Court", ["FrogCore", "Vasi", "SFCore"]),
            ("DebugMod", ["Satchel"]), ("Enemy HP Bar", ["Vasi", "SFCore"]),
            ("Randomizer 4", ["RandomizerCore"])
        })
        {
            var manifest = fixture.Mod with
            {
                Id = "hkmod:" + name, Name = name, DisplayName = name,
                Description = null, Dependencies = prerequisites.Select(id => "hkmod:" + id).ToArray()
            };
            var receipt = new InstalledModReceipt
            {
                Id = manifest.Id, Name = name, Version = manifest.Version, LoaderId = manifest.LoaderId,
                InstallRoot = "Mods/" + name, Enabled = true, Dependencies = manifest.Dependencies
            };
            vm.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(receipt, manifest,
                static () => { }, new MarketModItemViewModel(manifest, null, new Dictionary<string, string>(), false),
                ownershipDisplayName: vm.Loc["Installed"]));
        }
        var installed = vm.ModManagement.InstalledMods.ToArray();
        vm.ModManagement.LoadInstalledMods(installed.Select(mod => mod.Discovery).ToArray(),
            installed.Select(mod => mod.Receipt!).ToArray(), installed.Select(mod => mod.HealthReport).ToArray(), fixture.Instance.Record);
        vm.ModManagement.SelectedInstalledMod = vm.ModManagement.InstalledMods.Single(mod => mod.ReceiptName == "Pale Court");
        vm.ModManagement.ShowInstalledModGraphCommand.Execute(null);
        fixture.Window.Width = width;
        fixture.Window.Height = height;
        fixture.Window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        fixture.Window.Show();
        fixture.Window.DataContext = vm;
        Render();

        var view = fixture.Window.GetVisualDescendants().OfType<DependencyGraphView>()
            .Single(control => ReferenceEquals(control.DataContext, vm.DependencyGraph.Graph));
        var frame = view.FindAncestorOfType<Border>()!;
        var inspector = fixture.Window.FindControl<Expander>("InstalledModGraphDetails");
        Assert.NotNull(inspector);
        Assert.True(inspector.IsEffectivelyVisible);
        Assert.True(view.Bounds.Width > fixture.Window.ClientSize.Width - 240);
        Assert.InRange(inspector.Bounds.Height, 36, 280);
        Assert.True(view.Scale >= 0.7);
        Assert.Equal(7, vm.DependencyGraph.Graph.Edges.Count);
        var panelOrigin = Assert.IsType<Point>(inspector.TranslatePoint(default, frame));
        Assert.InRange(panelOrigin.X, 0, frame.Bounds.Width - inspector.Bounds.Width);
        Assert.InRange(panelOrigin.Y, 0, frame.Bounds.Height - inspector.Bounds.Height);
        Assert.False(inspector.IsExpanded);
        Assert.InRange(inspector.Bounds.Height, 1, 90);
        Capture("default");
        inspector.IsExpanded = true;
        Render();
        Assert.InRange(inspector.Bounds.Height, 36, 280);
        Capture("expanded");

        var missing = vm.DependencyGraph.Graph.Nodes.Single(node => node.IsMissing);
        vm.DependencyGraph.Graph.SetSelection(["hkmod:Pale Court", "hkmod:FrogCore", missing.Id], "hkmod:Pale Court", notify: true);
        Render();
        Assert.Equal(3, vm.DependencyGraph.Graph.SelectedNodes.Count);
        Assert.Equal(2, vm.ModManagement.SelectedModCount);
        Assert.True(vm.ModManagement.IsInstalledModBulkBarVisible);
        Assert.Contains(fixture.Window.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("cfp-mod-bulk-bar") && border.IsEffectivelyVisible);
        Capture("multiple");
        vm.DependencyGraph.Graph.SetSelection([], notify: true);
        Render();
        Assert.Equal(0, vm.ModManagement.SelectedModCount);
        Assert.False(inspector.IsEffectivelyVisible);
        vm.DependencyGraph.Graph.SelectNodeCommand.Execute(missing);
        Render();
        Assert.Same(missing, vm.DependencyGraph.Graph.SelectedNode);
        Assert.Null(vm.DependencyGraph.SelectedMod);
        Assert.Contains("Randomizer 4", vm.DependencyGraph.Graph.SelectedDependentsText);
        Capture("missing");

        inspector.IsExpanded = false;
        Render();
        Assert.InRange(inspector.Bounds.Height, 1, 90);
        Capture("collapsed");

        vm.DependencyGraph.Rebuild([], null, null);
        Render();
        Assert.False(inspector.FindAncestorOfType<Border>()!.IsEffectivelyVisible);
        vm.ModManagement.SelectedInstalledMod = vm.ModManagement.InstalledMods.First();
        vm.ModManagement.ShowInstalledModListCommand.Execute(null);
        Render();
        Assert.False(view.IsEffectivelyVisible);
        Assert.True(fixture.Window.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("cfp-mod-detail-panel")).IsEffectivelyVisible);

        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            fixture.Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Dispatcher.UIThread.RunJobs();
        }

        void Capture(string state)
        {
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_GRAPH_SCREENSHOTS") is not { Length: > 0 } output) return;
            Directory.CreateDirectory(output);
            using var bitmap = fixture.Window.GetLastRenderedFrame();
            Assert.NotNull(bitmap);
            bitmap.Save(Path.Combine(output, $"hk-graph-{width}-{language}-{(dark ? "dark" : "light")}-{state}.png"), new PngBitmapEncoderOptions());
        }
    }
}
