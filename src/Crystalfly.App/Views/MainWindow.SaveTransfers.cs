using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Crystalfly.App.ViewModels;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
    private async void ImportSaveFile(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel model || model.SaveEditor is not { CanImport: true } editor) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            { Title = model.Loc["SaveImport"], AllowMultiple = false, FileTypeFilter = [new("DAT") { Patterns = ["*.dat"] }] });
            if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
            var slot = editor.ImportSlot;
            var warning = await editor.ImportNeedsWarningAsync(path);
            var message = model.Loc["SaveImportConfirm"] + " " + slot
                + (warning ? Environment.NewLine + model.Loc["SaveNewerVersionWarning"] : "");
            if (await ShowConfirmationAsync(model.Loc["SaveImport"], message, model.Loc["Confirm"], model)
                && ReferenceEquals(model.SaveEditor, editor))
                await editor.ImportFileAsync(path, slot);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException
            or ArgumentException or System.Text.Json.JsonException or System.Security.Cryptography.CryptographicException)
        { model.ErrorMessage = model.Loc.ErrorMessageFor(error); }
    }

    private async void ExportSaveFile(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not MainViewModel model || model.SaveEditor is not { IsLoaded: true, SelectedSlot: not null } editor) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        { Title = model.Loc["SaveExport"], SuggestedFileName = editor.SelectedSlot, FileTypeChoices = [new("DAT") { Patterns = ["*.dat"] }] });
        if (file?.TryGetLocalPath() is { } path && ReferenceEquals(model.SaveEditor, editor)) await editor.ExportFileAsync(path);
    }
}
