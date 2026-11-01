using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{
    [AvaloniaFact]
    [Trait("Category", "VisualScan")]
    public async Task Deep_instance_hierarchy_reference_keeps_connectors_joined()
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceSelection);
        var model = fixture.ViewModel;
        model.Instances.Instances.Clear();
        var parent = fixture.Instance;
        for (var depth = 0; depth < 9; depth++)
        {
            var item = fixture.Instance with { Record = fixture.Instance.Record with
            {
                Id = $"depth-{depth}", Name = $"共享实例 · {depth + 1:D2}", UsesSharedContent = true,
                ParentInstanceId = depth == 0 ? null : parent.Id,
                ParentInstanceName = depth == 0 ? null : parent.Name
            } };
            model.Instances.Instances.Add(item);
            parent = item;
        }
        foreach (var depth in new[] { 5, 0 })
            model.Instances.Instances.Add(fixture.Instance with { Record = fixture.Instance.Record with
            {
                Id = $"sibling-{depth}", Name = $"分支实例 · {depth}", UsesSharedContent = true,
                ParentInstanceId = $"depth-{depth}"
            } });
        model.Instances.ApplyInstanceFilter();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        fixture.Window.Width = 1100;
        fixture.Window.Height = 1050;
        fixture.Window.Show();
        fixture.Window.DataContext = model;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            using var frame = fixture.Window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(output, "deep-hierarchy.png"), new PngBitmapEncoderOptions());
        }
    }

    [AvaloniaFact]
    public async Task Instance_hierarchy_exposes_both_clone_modes_and_storage_actions_at_minimum_width()
    {
        await using var fixture = CreateFixture();
        await fixture.PrepareAsync(ScreenshotState.InstanceSelection);
        var model = fixture.ViewModel;
        var parent = fixture.Instance with { Record = fixture.Instance.Record with { Name = "日常游玩", GameId = GameId.Silksong, UsesSharedContent = true }, DisplayVersion = "1.0.30000", LoaderDisplay = "bepinex-silksong-5.4.23.4", ModCount = 12 };
        var child = parent with { Record = parent.Record with { Id = "shared-demo", Name = "模组练习", ParentInstanceId = parent.Id, ParentInstanceName = parent.Name } };
        var independent = parent with { Record = parent.Record with { Id = "independent-demo", Name = "独立整合包", ParentInstanceId = parent.Id, ParentInstanceName = parent.Name, UsesSharedContent = false } };
        model.Instances.Instances.Clear();
        foreach (var item in new[] { parent, child, independent }) model.Instances.Instances.Add(item);
        var directory = model.Instances.SelectedGameDirectory!;
        directory.UpdateRegistration(directory.Registration with { DisplayName = "sk_ver" });
        directory.InstanceCount = 3;
        foreach (var (name, source) in new[] { ("HK 练习", GameDirectorySourceKind.Managed), ("Steam", GameDirectorySourceKind.Steam) })
            model.Instances.GameDirectories.Add(new GameDirectoryItemViewModel(new GameDirectoryRegistration
            {
                DisplayName = name, Path = Path.Combine(Path.GetTempPath(), "crystalfly-layout", name), Source = source
            }) { InstanceCount = 1, ScanStatus = model.Loc["ScanReady"] });
        model.Instances.ApplyInstanceFilter();
        model.SelectedInstance = child;
        await model.Instances.RefreshStorageUsageAsync();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        fixture.Window.Width = 1100;
        fixture.Window.Height = 720;
        fixture.Window.Show();
        fixture.Window.DataContext = model;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        Capture("instances-light");
        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
        Capture("instances-dark");
        fixture.Window.Width = 900;
        fixture.Window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        var list = Assert.Single(fixture.Window.GetVisualDescendants().OfType<ListBox>(), box => box.Classes.Contains("cfp-instance-list"));
        var branchPaths = list.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
            .Where(path => path.DataContext is InstanceItemViewModel && path.Data is not null).ToArray();
        Assert.Equal(3, branchPaths.Length);
        Assert.Equal(32, Assert.Single(branchPaths, path => ReferenceEquals(path.DataContext, child)).Data!.Bounds.Bottom);
        var childRow = list.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Classes.Contains("cfp-instance-row") && ReferenceEquals(grid.DataContext, child));
        fixture.Window.MouseMove(childRow.TranslatePoint(new Point(100, 32), fixture.Window)!.Value);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(250);
        Dispatcher.UIThread.RunJobs();
        var frameBorder = childRow.Children.OfType<Border>().Single(border => border.Classes.Contains("cfp-instance-action-frame"));
        Assert.Equal(1d, frameBorder.Opacity);
        Assert.True(frameBorder.IsHitTestVisible);
        Assert.Null(ToolTip.GetTip(childRow));
        var actions = list.GetVisualDescendants().OfType<Button>().Where(button => ReferenceEquals(button.DataContext, child) && button.Classes.Contains("cfp-instance-action")).ToArray();
        Assert.Equal(4, actions.Length);
        Assert.Contains(actions, button => AutomationProperties.GetName(button) == model.Loc["CloneSharedInstance"]);
        Assert.Contains(actions, button => AutomationProperties.GetName(button) == model.Loc["CloneIndependentInstance"]);
        foreach (var button in actions)
        {
            var position = button.TranslatePoint(default, fixture.Window)!.Value;
            Assert.InRange(position.X, 0, fixture.Window.ClientSize.Width - button.Bounds.Width);
            Assert.True(button.IsEffectivelyVisible);
            Assert.True(button.IsHitTestVisible);
        }
        var more = Assert.Single(actions, button => button.Flyout is not null);
        var flyout = Assert.IsType<Flyout>(more.Flyout);
        flyout.ShowAt(more);
        Dispatcher.UIThread.RunJobs();
        var menu = Assert.IsType<StackPanel>(flyout.Content);
        Assert.Same(child, menu.DataContext);
        Assert.Equal(new[] { model.Loc["FavoriteInstance"], model.Loc["OptimizeInstanceStorage"], model.Loc["MaterializeInstanceStorage"], model.Loc["DeleteInstance"] },
            menu.Children.OfType<Button>().Select(button => button.Content));
        Capture("instances-menu");
        flyout.Hide();
        await Task.Delay(200);
        Capture("instances-minimum");
        var rows = list.GetVisualDescendants().OfType<Grid>().Where(grid => grid.Classes.Contains("cfp-instance-row")).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.DoesNotContain(list.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("cfp-instance-toggle"));
        Assert.DoesNotContain(list.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "12");
        Assert.All(rows, row =>
        {
            var surface = row.Children.OfType<Border>().Single(border => border.Classes.Contains("cfp-instance-surface"));
            var summary = row.Children.OfType<Grid>().Single(grid => grid.Classes.Contains("cfp-instance-summary"));
            Assert.Equal(0, surface.Bounds.Left);
            Assert.Equal(40, summary.Bounds.Left);
            var instance = Assert.IsType<InstanceItemViewModel>(row.DataContext);
            var metadata = summary.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Classes.Contains("cfp-instance-metadata"));
            var version = Assert.Single(metadata.Children.OfType<TextBlock>(), text => text.Text == instance.DisplayVersion);
            Assert.DoesNotContain(metadata.Children.OfType<TextBlock>(), text => text.Text == instance.LoaderDisplay);
            Assert.True(version.Bounds.Width >= version.DesiredSize.Width);
        });

        if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is { Length: > 0 })
        {
            model.Instances.Instances.Clear();
            model.Instances.Instances.Add(parent);
            model.Instances.ApplyInstanceFilter();
            model.SelectedInstance = parent;
            Capture("instances-single");
        }

        void Capture(string name)
        {
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is not { Length: > 0 } output) return;
            Dispatcher.UIThread.RunJobs();
            fixture.Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Directory.CreateDirectory(output);
            using var frame = fixture.Window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
        }
    }
}
