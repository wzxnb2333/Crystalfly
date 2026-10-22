using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed class ErrorReportingUiTests
{
    [AvaloniaFact]
    public async Task Settings_toggle_localizes_and_notice_opt_out_persists()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly-ReportingUi-" + Guid.NewGuid());
        try
        {
            await using var viewModel = new MainViewModel(root) { CurrentPage = "Settings", HasErrorReportingNotice = true };
            var window = new MainWindow { DataContext = viewModel };
            var toggle = window.FindControl<CheckBox>("ErrorReportingToggle")!;
            viewModel.Loc.Apply(UiLanguage.SimplifiedChinese);
            Assert.Equal("自动发送错误报告", toggle.Content);
            Assert.True(toggle.IsChecked);
            viewModel.Loc.Apply(UiLanguage.English);
            Assert.Equal("Automatically send error reports", toggle.Content);
            Assert.Contains("No cloud destination", viewModel.ErrorReportingDestination);
            viewModel.DisableErrorReportingCommand.Execute(null);
            Assert.False(toggle.IsChecked);
            Assert.False(viewModel.HasErrorReportingNotice);
            await viewModel.DisposeAsync();
            var settings = await CrystalflySettingsStore.LoadAsync(Path.Combine(root, "settings.json"));
            Assert.False(settings.ErrorReportingEnabled);
            Assert.Equal(1, settings.ErrorReportingNoticeVersion);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
