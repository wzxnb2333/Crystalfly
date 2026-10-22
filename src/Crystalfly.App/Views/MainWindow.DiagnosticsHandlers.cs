using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Crystalfly.App.ViewModels;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
    private async void ExportErrorReports(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = viewModel.Loc["ExportErrorReports"],
                SuggestedFileName = "crystalfly-error-reports.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (file is null) return;
            var reports = viewModel.ExportErrorReports();
            await using var output = await file.OpenWriteAsync();
            output.SetLength(0);
            await output.WriteAsync(reports);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }
}
