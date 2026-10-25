using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Services;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.App.Views.Controls;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;
using SkiaSharp;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class ThunderstoreUiTests
{
    [AvaloniaTheory]
    [InlineData(700, 600, UiLanguage.SimplifiedChinese, false)]
    [InlineData(1000, 720, UiLanguage.SimplifiedChinese, true)]
    [InlineData(700, 600, UiLanguage.English, true)]
    [InlineData(1000, 720, UiLanguage.English, false)]
    public async Task Silksong_installed_tools_graph_and_batch_fit_the_workspace(int width, int height, UiLanguage language, bool dark)
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Silksong.Management", Guid.NewGuid().ToString("N"));
        using var client = new HttpClient(new InstalledIconHandler());
        var iconLoader = new ThunderstorePackageIconLoader(client, Path.Combine(root, "icons"), () => false);
        var loc = new LocalizationViewModel();
        loc.Apply(language);
        var target = new InstanceRecord { Id = "silk", Name = "Silksong", GameId = GameId.Silksong, BuildId = "test", RootPath = Path.Combine(root, "game") };
        var names = new[] { "Silksong_Mod_Menu", "Flea_Counter", "Benchwarp", "Shared_Library", "Map_Enhancements", "Quick_Cast" };
        var packages = names.Select(name => new ThunderstorePackage { FullName = "Author-" + name, Name = name, Owner = "Author",
            Versions = [new() { FullName = "Author-" + name + "-1.1.0", Name = name, VersionNumber = "1.1.0", Description = "A short mod description.",
                DownloadUrl = "https://thunderstore.io/package/download/Author/" + name + "/1.1.0/", FileSize = 100,
                IconUrl = name == "Quick_Cast" ? "" : "https://ccdn.thunderstore.io/" + name + ".png" }] }).ToArray();
        foreach (var package in packages.Where(package => package.Latest!.IconUrl.Length > 0))
        {
            using var icon = await iconLoader.LoadAsync(package.Latest!.IconUrl);
            Assert.NotNull(icon);
        }
        await AtomicJsonStore.WriteAsync(Path.Combine(root, "catalog.json"), new ThunderstoreCatalog { Packages = packages });
        await AtomicJsonStore.WriteAsync(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState { Packages = packages.Select((item, index) => new ThunderstoreInstalledPackage {
                FullName = item.FullName, Version = "1.0.0", ArchiveSha256 = new string('A', 64), Enabled = index != 4, Pinned = index == 2,
                Dependencies = index == 3 ? ["Author-Missing_API-1.0.0"] : ["Author-Shared_Library-1.0.0"] }).ToArray() });
        var model = new ThunderstoreViewModel(loc, new(client, Path.Combine(root, "catalog.json")), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask, packageIconLoader: iconLoader);
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.Null(model.Error);
        model.SelectedInstalled = model.Installed[0];
        var previous = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var view = new ThunderstoreView { DataContext = model, Workspace = "Mods" };
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        try
        {
            Layout(window);
            Assert.True(view.FindControl<Border>("InstalledDetails")!.IsEffectivelyVisible);
            var list = view.FindControl<ListBox>("InstalledList")!;
            Assert.Empty(list.GetVisualDescendants().OfType<CheckBox>());
            var icons = list.GetVisualDescendants().OfType<ThunderstorePackageIconImage>().ToArray();
            Assert.Equal(names.Length, icons.Length);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                while (icons.Any(icon => !string.IsNullOrEmpty(icon.IconUrl) && icon.Source is null))
                {
                    await Task.Delay(10, timeout.Token);
                    Dispatcher.UIThread.RunJobs();
                }
            Assert.Equal(names.Length - 1, icons.Count(icon => icon.Source is not null));
            Assert.All(icons, icon => Assert.Equal(new Size(40, 40),
                icon.GetVisualAncestors().OfType<Border>().First(border => border.Width == 40 && border.Height == 40).Bounds.Size));
            var firstRow = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            var label = firstRow.GetVisualDescendants().OfType<TextBlock>().First(item => item.Text == packages[0].Name);
            var point = label.TranslatePoint(new Point(6, 6), window)!.Value;
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.Shift);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.Shift);
            Layout(window);
            Assert.Equal(1, model.SelectedInstalledCount);
            Assert.True(model.InstalledDisplayItems[0].IsSelected);
            Assert.Same(model.Installed[0], model.SelectedInstalled);
            AssertToolsFit();
            Capture("list");
            model.Loc.Apply(language == UiLanguage.English ? UiLanguage.SimplifiedChinese : UiLanguage.English);
            model.RefreshLocalization();
            Layout(window);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>().Where(Visible), text => text.Text == model.InstalledSelectionText);
            Assert.Equal(model.Loc["FilterAll"], model.InstalledStatus?.Name);
            model.Loc.Apply(language);
            model.RefreshLocalization();
            model.SelectAllInstalledCommand.Execute(null);
            Assert.Equal(names.Length, model.SelectedInstalledCount);
            model.ShowInstalledGraphCommand.Execute(null);
            Layout(window);
            var graph = view.FindControl<DependencyGraphView>("InstalledDependencyGraph")!;
            Assert.True(graph.IsEffectivelyVisible);
            Assert.False(view.FindControl<Border>("InstalledDetails")!.IsEffectivelyVisible);
            Assert.True(graph.Bounds.Width > width - 60);
            Capture("graph");
            Assert.True(graph.Bounds.Height >= view.FindControl<Grid>("InstalledWorkspace")!.Bounds.Height - 124, $"Graph: {graph.Bounds}; workspace: {view.FindControl<Grid>("InstalledWorkspace")!.Bounds}");
            Assert.Equal(6, model.InstalledGraph.Edges.Count);
            Assert.Single(model.InstalledGraph.Nodes, item => item.IsMissing);
            AssertToolsFit();
            Capture("graph");
            var inspector = view.FindControl<Expander>("SilksongGraphDetails")!;
            inspector.IsExpanded = true;
            Layout(window);
            Assert.InRange(inspector.Bounds.Height, 36, 300);
            AssertToolsFit();
            Capture("details");
            var missing = model.InstalledGraph.Nodes.Single(item => item.IsMissing);
            model.InstalledGraph.SetSelection([packages[0].FullName, packages[1].FullName, missing.Id], packages[0].FullName, notify: true);
            Layout(window);
            Assert.Equal(3, model.InstalledGraph.SelectedNodes.Count);
            Assert.Equal(2, model.SelectedInstalledCount);
            Assert.True(model.HasInstalledBatchSelection);
            Capture("multiple");
            model.InstalledGraph.SetSelection([], notify: true);
            Layout(window);
            Assert.Equal(0, model.SelectedInstalledCount);
            Assert.False(inspector.IsEffectivelyVisible);
            model.InstalledGraph.SelectNodeCommand.Execute(model.InstalledGraph.Nodes.Single(item => item.IsMissing));
            Layout(window);
            Assert.Null(model.SelectedInstalled);
            AssertToolsFit();
            Capture("missing");
            model.ShowInstalledListCommand.Execute(null);
            Layout(window);
            Assert.True(list.IsEffectivelyVisible);
            Assert.False(graph.IsEffectivelyVisible);
        }
        finally { window.Close(); Application.Current.RequestedThemeVariant = previous; Directory.Delete(root, true); }

        void AssertToolsFit()
        {
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>().Where(Visible), item => item.Text?.StartsWith("Ts", StringComparison.Ordinal) == true);
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(button => Visible(button) && !button.Classes.Contains("cfp-dependency-node")))
            {
                var position = button.TranslatePoint(default, view)!.Value;
                Assert.True(position.X >= -1 && position.X + button.Bounds.Width <= width + 1,
                    $"{AutomationProperties.GetName(button)} extends past {width}: {position}, {button.Bounds}");
            }
        }
        void Capture(string state)
        {
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_SILKSONG_SCREENSHOTS") is not { Length: > 0 } output) return;
            Directory.CreateDirectory(output);
            using var stream = File.Create(Path.Combine(output, $"silksong-{width}-{language}-{(dark ? "dark" : "light")}-{state}.png"));
            using var bitmap = window.CaptureRenderedFrame()!;
            bitmap.Save(stream, new PngBitmapEncoderOptions());
        }
    }

    private sealed class InstalledIconHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var bitmap = new SKBitmap(256, 256);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColor.Parse("#286F93"));
            using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawCircle(128, 128, 70, paint);
            paint.Color = SKColor.Parse("#286F93");
            canvas.DrawCircle(128, 128, 44, paint);
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(encoded.ToArray()) });
        }
    }
}
