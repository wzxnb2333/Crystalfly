using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Ursa.Controls;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Views;

public partial class MainWindow
{
    private void OnInstalledModPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed
            || eventArgs.Source is not Avalonia.Visual visual
            || visual.FindAncestorOfType<Button>() is not null
            || visual.FindAncestorOfType<ListBoxItem>() is not { DataContext: InstalledModItemViewModel item }
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }
        viewModel.ModManagement.SelectInstalledMod(
            item,
            eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control),
            eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift));
        InstalledModsList.Focus(NavigationMethod.Pointer, eventArgs.KeyModifiers);
        eventArgs.Handled = true;
    }

    private void OnInstalledModsKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.A
            && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            && DataContext is MainViewModel viewModel)
        {
            viewModel.ModManagement.SelectAllInstalledModsCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    private void OpenInstalledModInfo(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.OpenInstalledModInfoCommand.Execute(item);
        }
    }

    private void OpenInstalledModFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        viewModel.ModManagement.SelectedInstalledMod = item;
        OpenSafeInstanceFolder(instance.RootPath, item.InstallRoot, viewModel);
    }

    private void OpenSelectedMarketModFolder(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel
            {
                SelectedInstance: { } instance,
                SelectedMarketInstalledMod: { } item
            } viewModel)
        {
            return;
        }
        OpenSafeInstanceFolder(instance.RootPath, item.InstallRoot, viewModel);
    }

    private void OpenSelectedModGlobalSettings(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            var filePath = viewModel.ResolveSelectedModGlobalSettingsPath();
            var directory = filePath is null ? null : Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or Win32Exception)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private async void ConfirmDeleteSelectedModGlobalSettings(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel
            {
                SelectedMarketModDisplay: { } mod,
                HasSelectedModGlobalSettings: true
            } viewModel)
        {
            return;
        }
        if (await ShowConfirmationAsync(
                viewModel.Loc["DeleteGlobalSettings"],
                viewModel.Loc["DeleteGlobalSettings"],
                mod.PrimaryName,
                viewModel,
                isDangerous: true))
        {
            await viewModel.DeleteSelectedModGlobalSettingsCommand.ExecuteAsync(null);
        }
    }

    private void OpenInstalledModRoot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel { SelectedInstance: { } instance } viewModel)
        {
            return;
        }
        var relative = viewModel.CurrentLoaderState == Crystalfly.Core.Models.LoaderState.BepInEx
            ? Path.Combine("BepInEx", "plugins")
            : Path.Combine("hollow_knight_Data", "Managed", "Mods");
        OpenSafeInstanceFolder(instance.RootPath, relative, viewModel);
    }

    private async void ImportLocalModPackage(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["SelectModPackageTitle"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["Mods"])
                {
                    Patterns = ["*.zip", "*.dll"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        viewModel.ModManagement.LocalModPath = path;
        await viewModel.ModManagement.ImportLocalModCommand.ExecuteAsync(null);
    }

    private async void ToggleHoveredInstalledMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.ModManagement.SelectedInstalledMod = item;
            await viewModel.ModManagement.ToggleSelectedModCommand.ExecuteAsync(null);
        }
    }

    private async void ToggleHoveredModPinned(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.ModManagement.SelectedInstalledMod = item;
            await viewModel.ModManagement.ToggleSelectedModPinnedCommand.ExecuteAsync(null);
        }
    }

    private async void TakeOverHoveredInstalledMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        viewModel.ModManagement.SelectedInstalledMod = item;
        if (await ShowConfirmationAsync(
                viewModel.Loc["TakeOverMod"],
                viewModel.Loc["ExternalModReadOnly"],
                item.Name,
                viewModel))
        {
            await viewModel.ModManagement.TakeOverSelectedModCommand.ExecuteAsync(null);
        }
    }

    private async void RepairHoveredInstalledMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.ModManagement.SelectedInstalledMod = item;
            await viewModel.ModManagement.RepairSelectedModCommand.ExecuteAsync(null);
        }
    }

    private async void AcceptHoveredLocalModFiles(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        viewModel.ModManagement.SelectedInstalledMod = item;
        if (await ShowConfirmationAsync(
                viewModel.Loc["AcceptCurrentFiles"],
                item.HealthDisplayName,
                item.Name,
                viewModel))
        {
            await viewModel.ModManagement.AcceptSelectedLocalModFilesCommand.ExecuteAsync(null);
        }
    }

    private async void ReimportHoveredLocalMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = viewModel.Loc["ReimportLocalMod"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(viewModel.Loc["Mods"])
                {
                    Patterns = ["*.zip", "*.dll"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        viewModel.ModManagement.SelectedInstalledMod = item;
        viewModel.ModManagement.LocalModPath = path;
        await viewModel.ModManagement.ReimportSelectedLocalModCommand.ExecuteAsync(null);
    }

    private async void ShowInstalledModHealth(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel
            || sender is not Control { DataContext: InstalledModItemViewModel item })
        {
            return;
        }
        var report = item.HealthReport;
        var paths = report.MissingFiles
            .Concat(report.ModifiedFiles)
            .Concat(report.ExtraFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await ShowConfirmationAsync(
            item.HealthDisplayName,
            string.IsNullOrWhiteSpace(report.Detail) ? item.HealthDisplayName : report.Detail,
            paths.Length == 0 ? item.Name : string.Join(Environment.NewLine, paths),
            viewModel,
            canConfirm: false);
    }

    private async void ConfirmHoveredModUninstall(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Control { DataContext: InstalledModItemViewModel item })
        {
            viewModel.ModManagement.SelectedInstalledMod = item;
            await ConfirmModRemovalAsync(viewModel, bulk: false);
        }
    }

    private async void ConfirmRepairDependencies(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }
        try
        {
            var plan = viewModel.ModManagement.CreateModDependencyRepairPlan();
            var nodes = BuildDependencyRepairNodes(viewModel, plan);
            var dialog = new DependencyPlanDialogViewModel(
                viewModel.Loc["RepairDependencies"],
                viewModel.Loc["DependencyImpact"],
                nodes,
                viewModel.Loc["Confirm"],
                viewModel.Loc["Cancel"],
                plan.Items.Any(item => item.Action != ModDependencyRepairAction.Unresolved),
                isDangerous: false);
            var confirmed = await OverlayDialog.ShowCustomAsync<
                DependencyPlanDialogView,
                DependencyPlanDialogViewModel,
                bool>(dialog, OverlayHostId, CreateOverlayOptions());
            if (confirmed)
            {
                await viewModel.ModManagement.RepairModDependenciesCommand.ExecuteAsync(null);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private async Task ConfirmModRemovalAsync(MainViewModel viewModel, bool bulk)
    {
        var plan = viewModel.ModManagement.CreateModRemovalPlan(bulk);
        var installed = viewModel.ModManagement.InstalledMods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var nodes = plan.Nodes.Select(node =>
        {
            installed.TryGetValue(node.ModId, out var item);
            return new DependencyPlanNodeViewModel(
                node.ModId,
                item?.PrimaryName ?? node.ReceiptName,
                item?.SecondaryName ?? node.ModId,
                node.Enabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"],
                DependencyGraphNodeState.Attention,
                node.Kind == ModRemovalImpactKind.WillRemove
                    ? viewModel.Loc["WillDelete"]
                    : viewModel.Loc["DependenciesWillBeMissing"],
                node.RelatedToModId is null ? null : [node.RelatedToModId]);
        }).ToArray();
        var dialog = new DependencyPlanDialogViewModel(
            bulk ? viewModel.Loc["ConfirmBulkUninstallTitle"] : viewModel.Loc["ConfirmModUninstallTitle"],
            viewModel.Loc["DependencyImpact"],
            nodes,
            viewModel.Loc["Uninstall"],
            viewModel.Loc["Cancel"],
            canConfirm: true,
            isDangerous: true);
        var confirmed = await OverlayDialog.ShowCustomAsync<
            DependencyPlanDialogView,
            DependencyPlanDialogViewModel,
            bool>(dialog, OverlayHostId, CreateOverlayOptions());
        if (confirmed)
        {
            if (bulk)
            {
                await viewModel.ModManagement.UninstallSelectedModsCommand.ExecuteAsync(null);
            }
            else
            {
                await viewModel.ModManagement.UninstallSelectedModCommand.ExecuteAsync(null);
            }
        }
    }

    private static void OpenSafeInstanceFolder(
        string instanceRoot,
        string relativePath,
        MainViewModel viewModel)
    {
        try
        {
            var target = ResolveSafeInstanceFolder(instanceRoot, relativePath);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or Win32Exception)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private static void OpenSafeAbsoluteFolder(
        string allowedRoot,
        string targetPath,
        bool create,
        MainViewModel viewModel)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot));
            var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
            if (!string.Equals(root, target, StringComparison.OrdinalIgnoreCase)
                && !target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The requested folder is outside the selected instance data root.");
            }
            if (create)
            {
                Directory.CreateDirectory(target);
            }
            if (!Directory.Exists(target))
            {
                throw new DirectoryNotFoundException($"Directory '{target}' was not found.");
            }
            for (var current = new DirectoryInfo(target); current is not null; current = current.Parent)
            {
                RejectReparsePoint(current.FullName);
                if (string.Equals(current.FullName, root, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or Win32Exception)
        {
            viewModel.ErrorMessage = viewModel.Loc.ErrorMessageFor(exception);
        }
    }

    private static string ResolveSafeInstanceFolder(string instanceRoot, string relativePath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(instanceRoot));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Instance root '{root}' was not found.");
        }
        RejectReparsePoint(root);

        var target = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested Mod path is outside the selected instance.");
        }

        var current = root;
        foreach (var segment in Path.GetRelativePath(root, target).Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                throw new DirectoryNotFoundException($"Mod directory '{current}' was not found.");
            }
            RejectReparsePoint(current);
        }
        return target;
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Cannot open reparse point '{path}'.");
        }
    }

    private static IReadOnlyList<DependencyPlanNodeViewModel> BuildDependencyRepairNodes(
        MainViewModel viewModel,
        ModDependencyRepairPlan plan)
    {
        var installed = viewModel.ModManagement.InstalledMods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var repairItems = plan.Items.ToDictionary(item => item.ModId, StringComparer.OrdinalIgnoreCase);
        var prerequisitesByRequiredMod = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in plan.Items)
        {
            foreach (var requiredById in item.RequiredByModIds)
            {
                if (!prerequisitesByRequiredMod.TryGetValue(requiredById, out var prerequisites))
                {
                    prerequisites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    prerequisitesByRequiredMod[requiredById] = prerequisites;
                }
                prerequisites.Add(item.ModId);
            }
        }

        var allIds = repairItems.Keys
            .Concat(prerequisitesByRequiredMod.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        return allIds.Select(id =>
        {
            installed.TryGetValue(id, out var current);
            prerequisitesByRequiredMod.TryGetValue(id, out var prerequisites);
            if (!repairItems.TryGetValue(id, out var item))
            {
                return new DependencyPlanNodeViewModel(
                    id,
                    current?.PrimaryName ?? id,
                    current?.SecondaryName ?? string.Empty,
                    current is null ? viewModel.Loc["Missing"] : current.IsEnabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"],
                    current is null
                        ? DependencyGraphNodeState.Missing
                        : current.IsExternal
                            ? DependencyGraphNodeState.External
                            : current.IsEnabled
                                ? DependencyGraphNodeState.Normal
                                : DependencyGraphNodeState.Disabled,
                    PrerequisiteIds: prerequisites?.ToArray());
            }

            var action = item.Action switch
            {
                ModDependencyRepairAction.ReEnable => viewModel.Loc["WillReEnable"],
                ModDependencyRepairAction.DownloadAndInstall => viewModel.Loc["WillDownloadAndInstall"],
                _ => viewModel.Loc["CannotRepair"]
            };
            var state = item.Action == ModDependencyRepairAction.Unresolved
                ? DependencyGraphNodeState.Attention
                : current is null
                    ? DependencyGraphNodeState.Missing
                    : current.IsExternal
                        ? DependencyGraphNodeState.External
                        : current.IsEnabled
                            ? DependencyGraphNodeState.Normal
                            : DependencyGraphNodeState.Disabled;
            return new DependencyPlanNodeViewModel(
                item.ModId,
                current?.PrimaryName ?? item.Name,
                current?.SecondaryName ?? string.Empty,
                current is null ? viewModel.Loc["Missing"] : current.IsEnabled ? viewModel.Loc["Enabled"] : viewModel.Loc["Disabled"],
                state,
                action,
                prerequisites?.ToArray());
        }).ToArray();
    }

    private async void ConfirmUninstallLoader(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedInstance is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmLoaderUninstallTitle"],
            viewModel.Loc["ConfirmLoaderUninstallMessage"],
            viewModel.SelectedInstance.Name,
            viewModel,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.UninstallLoaderCommand.ExecuteAsync(null);
        }
    }

    private async void ConfirmUninstallMod(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await ConfirmModRemovalAsync(viewModel, bulk: false);
        }
    }

    private async void ConfirmBulkUninstallMods(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel && viewModel.ModManagement.HasSelectedMods)
        {
            await ConfirmModRemovalAsync(viewModel, bulk: true);
        }
    }

    private async void ConfirmRestoreSnapshot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedSnapshot is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmSnapshotRestoreTitle"],
            viewModel.Loc["ConfirmSnapshotRestoreMessage"],
            viewModel.SelectedSnapshot.Name,
            viewModel);
        if (confirmed)
        {
            await viewModel.RestoreSnapshotCommand.ExecuteAsync(null);
        }
    }

    private async void ConfirmDeleteSnapshot(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedSnapshot is null)
        {
            return;
        }
        var confirmed = await ShowConfirmationAsync(
            viewModel.Loc["ConfirmSnapshotDeleteTitle"],
            viewModel.Loc["ConfirmSnapshotDeleteMessage"],
            viewModel.SelectedSnapshot.Name,
            viewModel,
            isDangerous: true);
        if (confirmed)
        {
            await viewModel.DeleteSnapshotCommand.ExecuteAsync(null);
        }
    }
}
