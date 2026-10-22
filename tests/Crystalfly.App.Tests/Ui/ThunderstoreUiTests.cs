using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Net;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.Ui;

public sealed class ThunderstoreUiTests
{
    [AvaloniaFact]
    public void Market_removes_target_card_and_separate_locator_and_keeps_accessible_instance_picker()
    {
        using var client = new HttpClient();
        var model = CreateModel(client, UiLanguage.SimplifiedChinese);
        var view = new ThunderstoreView { DataContext = model };
        var window = new Window { Content = view, Width = 700, Height = 600 };
        window.Show();
        try
        {
            Layout(window);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("cfp-market-source"));
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBox>(), box => AutomationProperties.GetName(box) == model.Loc["TsPackageLink"]);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Expander>(), expander => Equals(expander.Header, model.Loc["TsOpenLink"]));

            var search = view.FindControl<TextBox>("MarketSearch")!;
            Assert.Equal(model.Loc["TsSearchOrLocate"], AutomationProperties.GetName(search));
            Assert.True(search.Bounds.Width > 200);
            var selector = view.FindControl<Button>("SelectTargetInstance")!;
            Assert.True(Visible(selector));
            Assert.Equal(model.Loc["SelectInstance"], AutomationProperties.GetName(selector));
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(Visible))
            {
                var position = button.TranslatePoint(default, view)!.Value;
                Assert.True(position.X >= -1 && position.X + button.Bounds.Width <= view.Bounds.Width + 1);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(700, 600, UiLanguage.SimplifiedChinese, false)]
    [InlineData(1000, 720, UiLanguage.SimplifiedChinese, true)]
    [InlineData(700, 600, UiLanguage.English, true)]
    [InlineData(1300, 800, UiLanguage.English, false)]
    public void Workspaces_are_distinct_and_actions_fit_available_width(int width, int height, UiLanguage language, bool dark)
    {
        using var client = new HttpClient();
        var model = CreateModel(client, language);
        model.RestoreFilters(null, 0, false);
        var installed = new ThunderstoreInstalledPackage { FullName = "Author-Long_Package_Name", Version = "1.2.3", Enabled = true, Pinned = true };
        model.Installed.Add(installed);
        model.InstalledMods.Add(installed);
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var view = new ThunderstoreView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = height };
        window.Show();
        try
        {
            foreach (var workspace in new[] { "Market", "Mods", "Loader", "Presets" })
            {
                view.Workspace = workspace;
                model.SelectedInstalled = workspace == "Mods" ? installed : null;
                Layout(window);
                var expected = workspace == "Mods" ? "InstalledWorkspace" : workspace + "Workspace";
                foreach (var name in new[] { "MarketWorkspace", "InstalledWorkspace", "LoaderWorkspace", "PresetsWorkspace" })
                    Assert.Equal(name == expected, view.FindControl<Control>(name)!.IsVisible);
                var visibleText = view.GetVisualDescendants().OfType<TextBlock>().Where(Visible).Select(item => item.Text ?? "").ToArray();
                Assert.DoesNotContain(visibleText, text => text.StartsWith("Ts", StringComparison.Ordinal) || text.Contains("SettingOption {", StringComparison.Ordinal));
                if (workspace == "Mods")
                {
                    Assert.Contains(model.Loc["TsEnabled"], visibleText);
                    Assert.Contains(model.Loc["TsPinned"], visibleText);
                    Assert.True(view.FindControl<ListBox>("InstalledList")!.Bounds.Height > 120);
                }
                foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(Visible))
                {
                    var position = button.TranslatePoint(default, view);
                    Assert.NotNull(position);
                    Assert.InRange(position.Value.X, -1, width);
                    Assert.True(position.Value.X + button.Bounds.Width <= width + 1,
                        $"{workspace}: {button.Content} extends past {width}");
                }
            }
        }
        finally { window.Close(); Application.Current.RequestedThemeVariant = previousTheme; }
    }

    [AvaloniaTheory]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.English)]
    public void Long_install_confirmation_keeps_actions_visible_at_minimum_window_size(UiLanguage language)
    {
        using var client = new HttpClient();
        var model = CreateModel(client, language);
        model.PreviewText = string.Join(Environment.NewLine, Enumerable.Range(1, 40).Select(i => $"Author-Dependency{i} 1.0.0 → 2.0.0"));
        model.HasPlan = true;
        model.NeedsCompatibilityConfirmation = true;
        var view = new ThunderstoreView { DataContext = model };
        var window = new Window { Content = view, Width = 700, Height = 510 };
        window.Show();
        try
        {
            Layout(window);
            var confirmation = view.FindControl<Border>("InstallConfirmation")!;
            Assert.True(Visible(confirmation));
            var confirm = confirmation.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, model.Loc["TsConfirmInstall"]));
            Assert.False(model.ConfirmInstallCommand.CanExecute(null));
            Assert.False(confirm.IsEffectivelyEnabled);
            model.AcceptUnverified = true;
            Layout(window);
            Assert.True(confirm.IsEffectivelyEnabled);
            foreach (var button in confirmation.GetVisualDescendants().OfType<Button>())
            {
                var origin = button.TranslatePoint(default, view)!.Value;
                Assert.True(origin.Y >= 0 && origin.Y + button.Bounds.Height <= view.Bounds.Height);
            }
            model.CancelPreviewCommand.Execute(null);
            Layout(window);
            Assert.False(Visible(confirmation));
            Assert.True(Visible(view.FindControl<Grid>("MarketWorkspace")!));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Catalog_opens_full_width_detail_and_returns_to_results()
    {
        using var client = new HttpClient();
        var model = CreateModel(client, UiLanguage.SimplifiedChinese);
        var package = new ThunderstorePackage { Name = "Example", Owner = "Author", FullName = "Author-Example",
            Versions = [new() { VersionNumber = "1.0.0", Name = "Example", Description = "Author description" }] };
        model.Packages.Add(package);
        var view = new ThunderstoreView { DataContext = model };
        var window = new Window { Content = view, Width = 700, Height = 640 };
        window.Show();
        try
        {
            Layout(window);
            Assert.True(view.FindControl<Border>("CatalogPane")!.IsVisible);
            Assert.False(view.FindControl<Border>("PackageDetailPane")!.IsVisible);
            view.FindControl<ListBox>("CatalogList")!.SelectedItem = Assert.Single(model.PackageDisplayItems);
            Layout(window);
            Assert.Same(package, model.SelectedPackage);
            Assert.False(view.FindControl<Border>("CatalogPane")!.IsVisible);
            Assert.True(view.FindControl<Border>("PackageDetailPane")!.Bounds.Width > 600);
            view.FindControl<Button>("BackToCatalog")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(window);
            Assert.True(view.FindControl<Border>("CatalogPane")!.IsVisible);
            Assert.False(view.FindControl<Border>("PackageDetailPane")!.IsVisible);
            window.Width = 1100;
            Layout(window);
            Assert.True(view.FindControl<Border>("CatalogPane")!.IsVisible);
            Assert.False(view.FindControl<Border>("PackageDetailPane")!.IsVisible);
            Assert.False(view.FindControl<Button>("BackToCatalog")!.IsVisible);
            view.FindControl<ListBox>("CatalogList")!.SelectedItem = Assert.Single(model.PackageDisplayItems);
            Layout(window);
            Assert.False(view.FindControl<Border>("CatalogPane")!.IsVisible);
            Assert.True(view.FindControl<Border>("PackageDetailPane")!.IsVisible);
            Assert.True(view.FindControl<Button>("BackToCatalog")!.IsVisible);
            view.FindControl<Button>("BackToCatalog")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(window);
            Assert.True(view.FindControl<Border>("CatalogPane")!.IsVisible);
            Assert.False(view.FindControl<Border>("PackageDetailPane")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Market_rows_use_two_text_lines_and_a_40px_fallback_icon_at_700px()
    {
        using var client = new HttpClient();
        var model = CreateModel(client, UiLanguage.SimplifiedChinese);
        var package = new ThunderstorePackage { Name = "LongOriginalModName", Owner = "Author", FullName = "Author-LongOriginalModName",
            Versions = [new() { VersionNumber = "1.2.3", Name = "LongOriginalModName", Description = "A long description that must stay on one line in the compact market list." }] };
        model.Packages.Add(package);
        var view = new ThunderstoreView { DataContext = model };
        var window = new Window { Content = view, Width = 700, Height = 600 };
        window.Show();
        try
        {
            Layout(window);
            var icon = Assert.Single(view.GetVisualDescendants().OfType<ThunderstorePackageIconImage>());
            var fallback = Assert.Single(icon.GetVisualAncestors().OfType<Border>(), item => item.Width == 40 && item.Height == 40);
            Assert.Equal(40, fallback.Bounds.Width);
            Assert.Equal(40, fallback.Bounds.Height);
            Assert.Null(icon.Source);

            var row = Assert.Single(icon.GetVisualAncestors().OfType<ListBoxItem>());
            Assert.InRange(row.Bounds.Height, 56, 64);
            Assert.True(row.Bounds.Width <= 700);
            var texts = row.GetVisualDescendants().OfType<TextBlock>().Where(item => item.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(item.Text)).ToArray();
            Assert.Equal(3, texts.Length);
            var name = Assert.Single(texts, item => item.Text == "LongOriginalModName");
            var version = Assert.Single(texts, item => item.Text == "1.2.3");
            var description = Assert.Single(texts, item => item.Text == package.Latest!.Description);
            var nameY = name.TranslatePoint(default, row)!.Value.Y;
            var versionY = version.TranslatePoint(default, row)!.Value.Y;
            var descriptionY = description.TranslatePoint(default, row)!.Value.Y;
            Assert.InRange(Math.Abs(nameY - versionY), 0, 1);
            Assert.True(descriptionY > nameY);
            Assert.DoesNotContain(texts, item => item.Text is "Author" or "Author-LongOriginalModName");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Parent_visibility_cancels_and_restarts_visible_icon_requests()
    {
        var handler = new CancellationHandler();
        using var client = new HttpClient(handler);
        var cacheRoot = Path.Combine(Path.GetTempPath(), "Crystalfly.Ts.Ui", Guid.NewGuid().ToString("N"));
        var translationLoc = new LocalizationViewModel();
        translationLoc.Apply(UiLanguage.SimplifiedChinese);
        var model = new ThunderstoreViewModel(translationLoc,
            new(client, Path.Combine(cacheRoot, "catalog.json")), new(client, Path.Combine(cacheRoot, "packages")),
            () => null, () => false, _ => Task.CompletedTask, () => Task.CompletedTask,
            packageIconLoader: new(client, Path.Combine(cacheRoot, "icons"), () => false));
        model.Packages.Add(new ThunderstorePackage { Name = "Mod", Owner = "Author", FullName = "Author-Mod",
            Versions = [new() { Name = "Mod", VersionNumber = "1.0.0", IconUrl = "https://ccdn.thunderstore.io/visible.png" }] });
        var view = new ThunderstoreView { DataContext = model };
        var parent = new Grid { IsVisible = false, Children = { view } };
        var window = new Window { Content = parent, Width = 700, Height = 600 };
        window.Show();
        try
        {
            Layout(window);
            Assert.Equal(0, handler.RequestCount);
            parent.IsVisible = true;
            Layout(window);
            await handler.FirstRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
            parent.IsVisible = false;
            await handler.FirstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            parent.IsVisible = true;
            Layout(window);
            await handler.SecondRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            window.Close();
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, recursive: true);
        }

    }

    [AvaloniaFact]
    public void Empty_market_explains_next_step_and_language_change_updates_page_title()
    {
        using var client = new HttpClient();
        var model = CreateModel(client, UiLanguage.SimplifiedChinese);
        var view = new ThunderstoreView { DataContext = model };
        var window = new Window { Content = view, Width = 1000, Height = 700 };
        window.Show();
        try
        {
            Layout(window);
            Assert.True(view.FindControl<StackPanel>("CatalogEmptyState")!.IsVisible);
            Assert.Equal("发现丝之歌模组", view.FindControl<TextBlock>("WorkspaceTitle")!.Text);
            model.Loc.Apply(UiLanguage.English);
            model.RefreshLocalization();
            Layout(window);
            Assert.Equal("Discover Silksong mods", view.FindControl<TextBlock>("WorkspaceTitle")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Long_Chinese_names_and_bilingual_Markdown_fit_a_narrow_detail_and_preserve_selection()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Ts.TranslatedUi", Guid.NewGuid().ToString("N"));
        using var client = new HttpClient();
        const string original = "# Original\n\n`Config.Key`\n\n| Value | Meaning |\n| --- | --- |\n| 1 | Test |\n";
        const string chinese = "# 中文说明\n\n`Config.Key`\n\n| 数值 | 含义 |\n| --- | --- |\n| 1 | 测试 |\n";
        var metadata = new SilksongModTranslationCatalog { Mods = [new() { FullName = "Author-Example",
            DisplayName = string.Concat(Enumerable.Repeat("很长的中文纹章工具名称", 10)),
            SourceUrl = "https://thunderstore.io/c/hollow-knight-silksong/p/Author/Example/",
            Versions = [new() { Version = "1.0.0", Description = "中文功能说明", DescriptionSha256 = SilksongModTranslationSource.Hash("Author description"),
                Documents = new Dictionary<string, SilksongModDocumentTranslation> { ["README.md"] = new()
                { SourceSha256 = SilksongModTranslationSource.Hash(original), TranslationSha256 = SilksongModTranslationSource.Hash(chinese) } } }] }] };
        var directory = Path.Combine(root, "documents", "Author-Example", "1.0.0");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "README.en.md"), original);
        await File.WriteAllTextAsync(Path.Combine(directory, "README.zh-CN.md"), chinese);
        var model = CreateModel(client, UiLanguage.SimplifiedChinese, new(cacheRoot: root, baseline: metadata));
        var package = new ThunderstorePackage { Name = "Example", Owner = "Author", FullName = "Author-Example",
            Versions = [new() { VersionNumber = "1.0.0", Name = "Example", Description = "Author description" }] };
        model.Packages.Add(package);
        var view = new ThunderstoreView { DataContext = model };
        var window = new Window { Content = view, Width = 700, Height = 640 };
        window.Show();
        try
        {
            view.FindControl<ListBox>("CatalogList")!.SelectedItem = Assert.Single(model.PackageDisplayItems);
            await model.ReadDocumentCommand.ExecuteAsync("README.md");
            Layout(window);
            Assert.Equal(chinese, model.Document);
            Assert.True(view.FindControl<Button>("ChineseDocumentButton")!.IsEffectivelyEnabled);
            Assert.Contains("cfp-accent", view.FindControl<Button>("ChineseDocumentButton")!.Classes);
            Assert.Equal(view.FindControl<Button>("ChineseDocumentButton")!.Bounds.Width, view.FindControl<Button>("OriginalDocumentButton")!.Bounds.Width);
            Assert.True(view.FindControl<Expander>("OriginalDescription")!.IsVisible);
            Assert.False(view.FindControl<Expander>("OriginalDescription")!.IsExpanded);
            var version = model.SelectedVersion;
            model.Loc.Apply(UiLanguage.English);
            model.RefreshLocalization();
            Layout(window);
            Assert.Same(package, model.SelectedPackage);
            Assert.Same(version, model.SelectedVersion);
            Assert.False(view.FindControl<Expander>("OriginalDescription")!.IsVisible);
            Assert.Equal(original, model.Document);
            Assert.Contains("cfp-accent", view.FindControl<Button>("OriginalDocumentButton")!.Classes);
            view.FindControl<Button>("ChineseDocumentButton")!.Command!.Execute("chinese");
            Layout(window);
            Assert.Equal(chinese, model.Document);
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(Visible))
            {
                var position = button.TranslatePoint(default, view)!.Value;
                Assert.True(position.X >= -1 && position.X + button.Bounds.Width <= view.Bounds.Width + 1);
            }
            view.FindControl<Button>("BackToCatalog")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(window);
            Assert.True(view.FindControl<Border>("CatalogPane")!.IsVisible);
        }
        finally { window.Close(); Directory.Delete(root, recursive: true); }
    }

    private static ThunderstoreViewModel CreateModel(HttpClient client, UiLanguage language, SilksongModTranslationSource? translationSource = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Ts.Ui", Guid.NewGuid().ToString("N"));
        var loc = new LocalizationViewModel();
        loc.Apply(language);
        return new(loc, new(client, Path.Combine(root, "catalog.json")), new(client, Path.Combine(root, "cache")),
            () => new InstanceRecord { GameId = GameId.Silksong, Id = "test", BuildId = "test", Name = "Silksong test instance", RootPath = root },
            () => true, _ => Task.CompletedTask, () => Task.CompletedTask, translationSource: translationSource);
    }
    private sealed class CancellationHandler : HttpMessageHandler
    {
        private int requests;
        public int RequestCount => Volatile.Read(ref requests);
        public TaskCompletionSource FirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (Interlocked.Increment(ref requests) == 1) FirstRequest.TrySetResult();
            else SecondRequest.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { FirstCancelled.TrySetResult(); throw; }
            return new(HttpStatusCode.OK);
        }
    }
    private static bool Visible(Visual visual) => visual.IsVisible && visual.GetVisualAncestors().All(parent => parent.IsVisible);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
}
