using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Crystalfly.App.ViewModels;
using Avalonia.Controls.Notifications;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Ursa.Controls;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
    private async void ConfirmApplyPreset(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel
            {
                SelectedInstance: { } instance,
                SelectedPreset: { } preset
            } viewModel)
        {
            return;
        }

        try
        {
            var plan = await viewModel.CreateSelectedPresetPlanAsync();
            if (plan is null)
            {
                return;
            }

            var dialog = new DependencyPlanDialogViewModel(
                viewModel.Loc["ConfirmApplyPresetTitle"],
                $"{viewModel.Loc["ConfirmApplyPresetMessage"]}{Environment.NewLine}"
                + $"{viewModel.Loc["PresetMode"]}: {GetPresetModeName(viewModel, preset.ApplyMode)}{Environment.NewLine}"
                + $"{viewModel.Loc["PresetTargetInstance"]}: {instance.Name}",
                BuildPresetApplyNodes(viewModel),
                viewModel.Loc["ApplyPreset"],
                viewModel.Loc["Cancel"],
                canConfirm: !plan.IsBlocked,
                isDangerous: false);
            var confirmed = await OverlayDialog.ShowCustomAsync<
                DependencyPlanDialogView,
                DependencyPlanDialogViewModel,
                bool>(dialog, OverlayHostId, CreateOverlayOptions());
            if (confirmed)
            {
                await viewModel.EnqueueSelectedPresetAsync();
            }
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private async void ShowCreateModPackDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new ModPackEditorDialogViewModel(
            viewModel.Loc["CreateModPack"],
            viewModel.Loc["CreateModPackHint"],
            string.Empty,
            ModPresetApplyMode.Append,
            viewModel.Loc["PresetName"],
            viewModel.Loc["PresetMode"],
            viewModel.Loc["PresetModeAppend"],
            viewModel.Loc["PresetModeExact"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var result = await OverlayDialog.ShowCustomAsync<
            ModPackEditorDialogView,
            ModPackEditorDialogViewModel,
            ModPackEditorDialogResult?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (result is null)
        {
            return;
        }

        viewModel.PresetName = result.Name;
        viewModel.SelectedPresetModeOption = viewModel.PresetModeOptions.First(option => option.Value == result.ApplyMode);
        await viewModel.CreatePresetCommand.ExecuteAsync(null);
    }

    private async void ShowCopyModPackDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedPreset: { } preset } viewModel)
        {
            return;
        }

        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["CopyPreset"],
            viewModel.Loc["CopyModPackHint"],
            $"{preset.Name} - {viewModel.Loc["CopySuffix"]}",
            viewModel.Loc["PresetName"],
            viewModel.Loc["Confirm"],
            viewModel.Loc["Cancel"]);
        var name = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        viewModel.PresetCopyName = name;
        await viewModel.CopySelectedPresetCommand.ExecuteAsync(null);
    }

    private async void ShowImportSharedModPackDialog(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new TextInputDialogViewModel(
            viewModel.Loc["ImportSharedPreset"],
            viewModel.Loc["ImportSharedPresetHint"],
            string.Empty,
            viewModel.Loc["PresetShareCode"],
            viewModel.Loc["ImportSharedPreset"],
            viewModel.Loc["Cancel"]);
        var code = await OverlayDialog.ShowCustomAsync<
            TextInputDialogView,
            TextInputDialogViewModel,
            string?>(dialog, OverlayHostId, CreateOverlayOptions());
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        viewModel.PresetShareCode = code;
        await viewModel.ImportSharedPresetCommand.ExecuteAsync(null);
    }

    private async void ShareAndCopyPresetLink(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        await viewModel.ShareSelectedPresetCommand.ExecuteAsync(null);
        if (viewModel.ErrorMessage is null && await TryCopyPresetShareLinkAsync(viewModel))
        {
            ShowToast(viewModel, viewModel.Loc["PresetShareLinkCopied"], NotificationType.Success);
        }
        else if (viewModel.ErrorMessage is null)
        {
            ShowToast(viewModel, viewModel.Loc["PresetShared"], NotificationType.Success);
        }
    }

    private async void ConfirmDeletePreset(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedPreset: { } preset } viewModel)
        {
            return;
        }

        if (await ShowConfirmationAsync(
                viewModel.Loc["ConfirmDeletePresetTitle"],
                viewModel.Loc["ConfirmDeletePresetMessage"],
                preset.Name,
                viewModel,
                isDangerous: true))
        {
            await viewModel.DeleteSelectedPresetAsync();
        }
    }

    private async void ImportPresetFile(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["ImportPreset"],
            AllowMultiple = false,
            FileTypeFilter = [CreatePresetJsonFileType(viewModel)]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        if (!IsPresetJsonPath(path))
        {
            viewModel.ErrorMessage = viewModel.Loc["PresetJsonFileRequired"];
            return;
        }

        await viewModel.ImportPresetFromFileAsync(path);
    }

    private async void ExportSelectedPreset(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedPreset: { } preset } viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = viewModel.Loc["ExportPreset"],
            SuggestedFileName = $"{preset.Name}.json",
            DefaultExtension = "json",
            FileTypeChoices = [CreatePresetJsonFileType(viewModel)],
            ShowOverwritePrompt = true
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.ExportSelectedPresetToFileAsync(path);
    }

    private async Task<bool> TryCopyPresetShareLinkAsync(MainViewModel viewModel)
    {
        if (Clipboard is null
            || !Uri.TryCreate(viewModel.LastPresetShareUrl, UriKind.Absolute, out var link)
            || link.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        try
        {
            await Clipboard.SetTextAsync(link.AbsoluteUri);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            viewModel.ErrorMessage = $"{viewModel.Loc["OperationFailed"]}: {exception.Message}";
            return false;
        }
    }

    private static FilePickerFileType CreatePresetJsonFileType(MainViewModel viewModel) => new(
        viewModel.Loc["PresetJsonFiles"])
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    private static bool IsPresetJsonPath(string path) =>
        string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);

    private static string GetPresetModeName(MainViewModel viewModel, Crystalfly.Core.Models.ModPresetApplyMode mode) =>
        viewModel.Loc[mode == Crystalfly.Core.Models.ModPresetApplyMode.Exact
            ? "PresetModeExact"
            : "PresetModeAppend"];

    private static IReadOnlyList<DependencyPlanNodeViewModel> BuildPresetApplyNodes(MainViewModel viewModel) =>
        viewModel.PresetApplySteps.Select(step => new DependencyPlanNodeViewModel(
            step.ModId,
            step.ModId,
            string.Join(" · ", new[] { step.Version, step.LoaderId }
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            step.State,
            step.IsUnresolved || step.IsBlocked
                ? DependencyGraphNodeState.Attention
                : DependencyGraphNodeState.Normal,
            step.Action)).ToArray();
}
