using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class DialogLayoutRegressionTests
{
    [AvaloniaTheory]
    [InlineData("discovery", UiLanguage.SimplifiedChinese)]
    [InlineData("discovery", UiLanguage.English)]
    [InlineData("mod-pack", UiLanguage.SimplifiedChinese)]
    [InlineData("mod-pack", UiLanguage.English)]
    [InlineData("update-error", UiLanguage.SimplifiedChinese)]
    [InlineData("update-error", UiLanguage.English)]
    [InlineData("update-notes-error", UiLanguage.SimplifiedChinese)]
    [InlineData("update-notes-error", UiLanguage.English)]
    public async Task Dialog_content_and_actions_fit_the_minimum_window(string kind, UiLanguage language)
    {
        var loc = new LocalizationViewModel();
        loc.Apply(language);
        var window = new MainWindow
        {
            Width = 900, Height = 600,
            RequestedThemeVariant = language == UiLanguage.English ? ThemeVariant.Light : ThemeVariant.Dark
        };
        window.Show();
        try
        {
            var options = new OverlayDialogOptions
            {
                TopLevelHashCode = window.GetHashCode(), CanLightDismiss = false,
                CanDragMove = false, IsCloseButtonVisible = true, CanResize = false
            };
            switch (kind)
            {
                case "discovery":
                    var candidates = new[]
                    {
                        new GameDirectoryCandidateItemViewModel(new GameDirectoryCandidate
                        {
                            Path = @"D:\Games\Hollow Knight\" + new string('x', 160),
                            DisplayName = "Hollow Knight custom installation with separate practice saves and community speedrunning tools",
                            Source = GameDirectorySourceKind.Custom
                        })
                    };
                    _ = OverlayDialog.ShowCustomAsync<GameDirectoryDiscoveryDialogView,
                        GameDirectoryDiscoveryDialogViewModel, GameDirectoryDiscoveryDialogResult>(
                        new(loc["GameDirectoryDiscoveryTitle"], loc["GameDirectoryDiscoveryHint"], candidates,
                            loc["ScanGameDirectories"], loc["AddGameDirectory"], loc["ConfirmAddDirectories"], loc["SkipForNow"]),
                        MainWindow.OverlayHostId, options);
                    break;
                case "mod-pack":
                    _ = OverlayDialog.ShowCustomAsync<ModPackEditorDialogView, ModPackEditorDialogViewModel, ModPackEditorDialogResult?>(
                        new(loc["CreateModPack"], loc["CreateModPackHint"], "Practice", ModPresetApplyMode.Append,
                            loc["PresetName"], loc["PresetMode"], loc["PresetModeAppend"], loc["PresetModeExact"],
                            loc["Confirm"], loc["Cancel"]), MainWindow.OverlayHostId, options);
                    break;
                case "update-error":
                case "update-notes-error":
                    var notes = kind == "update-notes-error"
                        ? string.Join("\n\n", Enumerable.Repeat("## Release notes\nImproves application reliability and compatibility.", 30))
                        : "Release notes";
                    var update = new ApplicationUpdateDialogViewModel(loc, "1.1.6", notes,
                        (_, _) => throw new IOException(string.Join(Environment.NewLine,
                            Enumerable.Repeat("The update download failed. Retry after checking the network connection.", 24))));
                    await update.UpdateCommand.ExecuteAsync(null);
                    _ = OverlayDialog.ShowCustomAsync<ApplicationUpdateDialogView, ApplicationUpdateDialogViewModel, ApplicationUpdateDialogResult>(
                        update, MainWindow.OverlayHostId, options);
                    break;
            }

            CustomDialogControl? dialog = null;
            for (var i = 0; i < 50 && dialog is null; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
                dialog = window.GetVisualDescendants().OfType<CustomDialogControl>().SingleOrDefault();
            }
            Assert.NotNull(dialog);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            if (kind == "discovery")
            {
                var candidate = Assert.Single(dialog.GetVisualDescendants().OfType<CheckBox>());
                var item = Assert.IsType<GameDirectoryCandidateItemViewModel>(candidate.DataContext);
                foreach (var value in new[] { item.DisplayName, item.Path })
                {
                    var label = Assert.Single(dialog.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == value);
                    Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
                    Assert.Equal(value, ToolTip.GetTip(label));
                    Assert.True(label.Bounds.Width > 0);
                }
            }
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_CAPTURE_DIALOG_AUDIT") == "1")
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx")))
                    directory = directory.Parent;
                Assert.NotNull(directory);
                var output = Path.Combine(directory.FullName, "artifacts", "goal-review", "round-01");
                Directory.CreateDirectory(output);
                using var frame = Assert.IsType<WriteableBitmap>(window.GetLastRenderedFrame());
                frame.Save(Path.Combine(output, $"{kind}-{language}.jpg"), new JpegBitmapEncoderOptions { Quality = 95 });
            }

            var heading = Assert.Single(dialog.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Classes.Contains("cfp-section-title"));
            var headingOrigin = Assert.IsType<Point>(heading.TranslatePoint(default, window));
            Assert.True(headingOrigin.Y >= 0 && headingOrigin.Y + heading.Bounds.Height <= window.ClientSize.Height + 1,
                $"{kind}: the dialog title is clipped vertically at {headingOrigin} ({heading.Bounds}).");
            if (kind.StartsWith("update-", StringComparison.Ordinal))
            {
                var error = Assert.Single(dialog.GetVisualDescendants().OfType<Border>(),
                    border => border.Classes.Contains("cfp-error"));
                var scroll = Assert.Single(error.GetVisualDescendants().OfType<ScrollViewer>());
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
                Assert.True(scroll.Bounds.Height <= 120);
            }

            var actions = dialog.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && button.Command is not null).ToArray();
            Assert.NotEmpty(actions);
            foreach (var button in actions)
            {
                var origin = Assert.IsType<Point>(button.TranslatePoint(default, window));
                Assert.True(origin.Y >= 0 && origin.Y + button.Bounds.Height <= window.ClientSize.Height + 1,
                    $"{kind}: action '{button.Content}' is outside the window at {origin} ({button.Bounds}).");
                Assert.True(origin.X >= 0 && origin.X + button.Bounds.Width <= window.ClientSize.Width + 1,
                    $"{kind}: action '{button.Content}' is clipped horizontally at {origin} ({button.Bounds}).");
            }
            foreach (var text in dialog.GetVisualDescendants().OfType<TextBlock>().Where(text =>
                         text.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(text.Text) &&
                         text.FindAncestorOfType<TextBox>() is null && text.TextTrimming == TextTrimming.None))
            {
                Assert.True(text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 1,
                    $"{kind}: '{text.Text}' needs {text.TextLayout.WidthIncludingTrailingWhitespace:F1}px, has {text.Bounds.Width:F1}px.");
                var origin = Assert.IsType<Point>(text.TranslatePoint(default, window));
                Assert.True(origin.X >= -1 && origin.X + text.Bounds.Width <= window.ClientSize.Width + 1,
                    $"{kind}: '{text.Text}' extends past the window horizontally.");
            }
        }
        finally
        {
            foreach (var dialog in window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray())
                dialog.Close();
            typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
            window.Close();
        }
    }
}
