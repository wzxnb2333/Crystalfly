using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views.Dialogs;
using Ursa.Controls;

namespace Crystalfly.App.Views;

public partial class ThunderstoreView : UserControl
{
    public static readonly StyledProperty<string> WorkspaceProperty =
        AvaloniaProperty.Register<ThunderstoreView, string>(nameof(Workspace), "Market");
    public string Workspace { get => GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }
    private bool detailOpen;
    private bool initialized;
    private Task? confirmationTask;
    private readonly List<Visual> visibilityAncestors = [];
    public ThunderstoreView()
    {
        InitializeComponent();
        CatalogList.AddHandler(InputElement.PointerPressedEvent, OnCatalogPointerPressed, RoutingStrategies.Tunnel);
        initialized = true;
        UpdatePresentation();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty)
        {
            if (change.OldValue is ThunderstoreViewModel previous) previous.PropertyChanged -= ModelChanged;
            if (change.NewValue is ThunderstoreViewModel next) next.PropertyChanged += ModelChanged;
        }
        if (change.Property == WorkspaceProperty) detailOpen = false;
        if (initialized && (change.Property == DataContextProperty || change.Property == WorkspaceProperty || change.Property == BoundsProperty))
            UpdatePresentation();
        if (initialized && change.Property == IsVisibleProperty) RefreshPackageIcons();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        foreach (var ancestor in this.GetVisualAncestors())
        {
            ancestor.PropertyChanged += VisibilityAncestorChanged;
            visibilityAncestors.Add(ancestor);
        }
        RefreshPackageIcons();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var ancestor in visibilityAncestors) ancestor.PropertyChanged -= VisibilityAncestorChanged;
        visibilityAncestors.Clear();
        base.OnDetachedFromVisualTree(e);
    }
    private void VisibilityAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty) RefreshPackageIcons();
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ThunderstoreViewModel.SelectedPackage))
            detailOpen = (sender as ThunderstoreViewModel)?.SelectedPackage is not null;
        if (args.PropertyName is nameof(ThunderstoreViewModel.SelectedPackage) or nameof(ThunderstoreViewModel.TargetName))
            UpdatePresentation();
        if (args.PropertyName == nameof(ThunderstoreViewModel.HasConfirmation))
            Dispatcher.UIThread.Post(ShowConfirmation);
    }

    private async void ShowConfirmation()
    {
        if (confirmationTask is not null || !IsEffectivelyVisible
            || DataContext is not ThunderstoreViewModel { HasConfirmation: true } model
            || TopLevel.GetTopLevel(this) is not MainWindow window) return;
        using var dialog = new ThunderstoreConfirmationDialogViewModel(model);
        try
        {
            confirmationTask = OverlayDialog.ShowCustomAsync<ThunderstoreConfirmationDialogView,
                ThunderstoreConfirmationDialogViewModel, object?>(dialog, MainWindow.OverlayHostId,
                new OverlayDialogOptions { TopLevelHashCode = window.GetHashCode(), CanLightDismiss = false,
                    CanDragMove = false, CanResize = false, IsCloseButtonVisible = false });
            await confirmationTask;
        }
        finally { confirmationTask = null; }
    }
    private void OnCatalogPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (args.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && args.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed
            && args.Source is Control { DataContext: ThunderstorePackageDisplayItem item })
        {
            item.IsSelected = !item.IsSelected;
            args.Handled = true;
        }
    }
    private void SelectCatalogPackage(object? sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count == 0) return;
        detailOpen = true;
        UpdatePresentation();
    }
    private void ShowCatalog(object? sender, RoutedEventArgs args)
    {
        detailOpen = false;
        if (DataContext is ThunderstoreViewModel model)
            model.SelectedPackage = null;
        CatalogList.SelectedItem = null;
        UpdatePresentation();
        CatalogList.Focus();
    }
    private void UpdatePresentation()
    {
        if (!initialized) return;
        var isMarket = Workspace == "Market";
        SelectTargetInstance.IsVisible = !isMarket;
        RefreshWorkspace.IsVisible = !isMarket;
        MarketWorkspace.IsVisible = isMarket && !detailOpen;
        PackageDetailWorkspace.IsVisible = isMarket && detailOpen;
        InstalledWorkspace.IsVisible = Workspace == "Mods";
        LoaderWorkspace.IsVisible = Workspace == "Loader";
        PresetsWorkspace.IsVisible = Workspace == "Presets";
        if (DataContext is ThunderstoreViewModel model)
        {
            WorkspaceTitle.Text = model.Loc[Workspace switch { "Mods" => "InstalledMods", "Loader" => "Loader", "Presets" => "ModPacks", _ => "TsMarketTitle" }];
            WorkspaceHint.Text = model.Loc[Workspace switch { "Mods" => "TsInstalledHint", "Loader" => "TsLoaderHint", "Presets" => "TsPresetsHint", _ => "TsMarketHint" }];
        }
        CatalogPane.IsVisible = isMarket && !detailOpen;
        PackageDetailPane.IsVisible = isMarket && detailOpen;
        BackToCatalog.IsVisible = isMarket && detailOpen;
        WorkspaceHeader.IsVisible = !isMarket || !detailOpen;
        RefreshPackageIcons();
    }
    private void RefreshPackageIcons()
    {
        foreach (var icon in CatalogList.GetVisualDescendants().OfType<ThunderstorePackageIconImage>()) icon.RefreshVisibility();
    }
    private async void ImportZip(object? sender, RoutedEventArgs args) => await ImportAsync(false);
    private async void ImportPreset(object? sender, RoutedEventArgs args) => await ImportAsync(true);
    private async Task ImportAsync(bool preset)
    {
        if (DataContext is not ThunderstoreViewModel model || TopLevel.GetTopLevel(this) is not { } top) return;
        if (!model.CanManage) return;
        var context = model.ContextVersion;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = model.Loc[preset ? "TsImportPreset" : "TsImportZip"], AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(preset ? "JSON" : "ZIP") { Patterns = [preset ? "*.json" : "*.zip"] }]
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
        if (context != model.ContextVersion) { model.Error = model.Loc["TsTargetChanged"]; return; }
        if (preset) await model.ImportPresetAsync(path); else await model.ImportZipAsync(path);
    }
    private async void ExportPreset(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not ThunderstoreViewModel model || TopLevel.GetTopLevel(this) is not { } top) return;
        if (!model.CanManage) return;
        var context = model.ContextVersion;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = model.Loc["TsExportPreset"], SuggestedFileName = "silksong-mods.json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });
        if (context != model.ContextVersion) { model.Error = model.Loc["TsTargetChanged"]; return; }
        if (file?.TryGetLocalPath() is { } path) await model.ExportPresetAsync(path);
    }
}

public sealed class ThunderstorePackageIconImage : Image
{
    public static readonly StyledProperty<string?> IconUrlProperty =
        AvaloniaProperty.Register<ThunderstorePackageIconImage, string?>(nameof(IconUrl));
    private CancellationTokenSource? request;
    private Bitmap? bitmap;
    private int generation;
    private bool attached;

    public string? IconUrl { get => GetValue(IconUrlProperty); set => SetValue(IconUrlProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attached = false;
        Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconUrlProperty)
        {
            Clear();
            if (attached) Load();
        }
    }

    internal void RefreshVisibility() { if (IsEffectivelyVisible) Load(); else Clear(); }

    private void Clear()
    {
        generation++;
        var previous = request;
        request = null;
        previous?.Cancel();
        Source = null;
        bitmap?.Dispose();
        bitmap = null;
    }

    private async void Load()
    {
        if (!IsEffectivelyVisible || request is not null || bitmap is not null || string.IsNullOrWhiteSpace(IconUrl)
            || this.FindAncestorOfType<ThunderstoreView>()?.DataContext is not ThunderstoreViewModel { PackageIconLoader: { } loader }) return;
        var url = IconUrl;
        var current = ++generation;
        var cancellation = new CancellationTokenSource();
        request = cancellation;
        try
        {
            var loaded = await loader.LoadAsync(url, cancellation.Token);
            if (loaded is null) return;
            if (current != generation || !attached || IconUrl != url)
            {
                loaded.Dispose();
                return;
            }
            bitmap = loaded;
            Source = loaded;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(request, cancellation)) request = null;
            cancellation.Dispose();
        }
    }
}
