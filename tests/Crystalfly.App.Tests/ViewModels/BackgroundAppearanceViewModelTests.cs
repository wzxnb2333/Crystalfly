using System.Buffers.Binary;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class BackgroundAppearanceViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-background-vm-{Guid.NewGuid():N}");

    [AvaloniaFact]
    public async Task Initialize_loads_global_background_and_opacity()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data")).FullName;
        var appearance = Directory.CreateDirectory(Path.Combine(appData, "appearance")).FullName;
        var image = CreateBmp(Path.Combine(appearance, "global.bmp"), 2, 2);
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                OfflineMode = true,
                BackgroundImage = new BackgroundImageSettings
                {
                    FileName = Path.GetFileName(image),
                    OpacityPercent = 47
                }
            });
        await using var viewModel = CreateViewModel(appData);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasActiveBackgroundImage);
        Assert.Equal(0.47, viewModel.ActiveBackgroundOpacity, 3);
        Assert.Equal(47, viewModel.BackgroundOpacityPercent);
    }

    [AvaloniaFact]
    public async Task Instance_override_wins_and_missing_override_falls_back_to_global()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data")).FullName;
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var record = Instance("practice", versionRoot);
        var globalDirectory = Directory.CreateDirectory(Path.Combine(appData, "appearance")).FullName;
        CreateBmp(Path.Combine(globalDirectory, "global.bmp"), 2, 2);
        var instanceAppearance = Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            record.Id,
            "appearance")).FullName;
        CreateBmp(Path.Combine(instanceAppearance, "instance.bmp"), 3, 2);
        await InstanceAppearanceSettingsStore.SaveAsync(
            Path.Combine(instanceAppearance, "appearance.json"),
            new InstanceAppearanceSettings
            {
                BackgroundImage = new BackgroundImageSettings { FileName = "instance.bmp", OpacityPercent = 70 }
            });
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                OfflineMode = true,
                VersionRoot = versionRoot,
                CurrentInstanceId = record.Id,
                BackgroundImage = new BackgroundImageSettings { FileName = "global.bmp", OpacityPercent = 25 }
            });
        await using var viewModel = CreateViewModel(appData);
        SetInstanceDiscovery(viewModel, record);
        await viewModel.InitializeAsync();

        await viewModel.RefreshBackgroundAppearanceAsync();

        Assert.True(viewModel.HasInstanceBackgroundOverride);
        Assert.Equal(0.70, viewModel.ActiveBackgroundOpacity, 3);

        File.Delete(Path.Combine(instanceAppearance, "instance.bmp"));
        await viewModel.RefreshBackgroundAppearanceAsync();

        Assert.False(viewModel.HasInstanceBackgroundOverride);
        Assert.Equal(0.25, viewModel.ActiveBackgroundOpacity, 3);
    }

    [AvaloniaFact]
    public async Task Removing_instance_override_restores_global_background()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data")).FullName;
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var record = Instance("practice", versionRoot);
        var globalDirectory = Directory.CreateDirectory(Path.Combine(appData, "appearance")).FullName;
        CreateBmp(Path.Combine(globalDirectory, "global.bmp"), 2, 2);
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                OfflineMode = true,
                VersionRoot = versionRoot,
                CurrentInstanceId = record.Id,
                BackgroundImage = new BackgroundImageSettings { FileName = "global.bmp", OpacityPercent = 20 }
            });
        await using var viewModel = CreateViewModel(appData);
        SetInstanceDiscovery(viewModel, record);
        await viewModel.InitializeAsync();
        viewModel.SelectedBackgroundScope = viewModel.BackgroundScopeOptions.Single(option =>
            option.Value == BackgroundEditScope.CurrentInstance);
        var source = CreateBmp(Path.Combine(root, "instance.bmp"), 3, 2);
        await viewModel.SetBackgroundImageAsync(source);
        Assert.Equal(0.35, viewModel.ActiveBackgroundOpacity, 3);

        await viewModel.RemoveBackgroundImageAsync();

        Assert.False(viewModel.HasInstanceBackgroundOverride);
        Assert.Equal(0.20, viewModel.ActiveBackgroundOpacity, 3);
    }

    [AvaloniaFact]
    public async Task Instance_scope_is_disabled_without_current_instance()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data")).FullName;
        await using var viewModel = CreateViewModel(appData);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.CanEditInstanceBackground);
        Assert.False(viewModel.CanChangeBackgroundOpacity);
    }

    [AvaloniaFact]
    public async Task Debounced_instance_opacity_save_stays_with_the_instance_that_was_edited()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data")).FullName;
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var first = Instance("first", versionRoot);
        var second = Instance("second", versionRoot);
        await CreateInstanceBackgroundAsync(versionRoot, first.Id, "first.bmp", 30);
        await CreateInstanceBackgroundAsync(versionRoot, second.Id, "second.bmp", 80);
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                OfflineMode = true,
                VersionRoot = versionRoot,
                CurrentInstanceId = first.Id
            });
        await using var viewModel = CreateViewModel(appData);
        SetInstanceDiscovery(viewModel, first, second);
        await viewModel.InitializeAsync();
        viewModel.SelectedBackgroundScope = viewModel.BackgroundScopeOptions.Single(option =>
            option.Value == BackgroundEditScope.CurrentInstance);
        await viewModel.RefreshBackgroundAppearanceAsync();

        viewModel.BackgroundOpacityPercent = 55;
        viewModel.SelectedInstance = viewModel.Instances.Single(instance => instance.Id == second.Id);
        await Task.Delay(700);

        var firstSettings = await InstanceAppearanceSettingsStore.LoadAsync(InstanceAppearancePath(versionRoot, first.Id));
        var secondSettings = await InstanceAppearanceSettingsStore.LoadAsync(InstanceAppearancePath(versionRoot, second.Id));
        Assert.Equal(55, firstSettings.BackgroundImage?.OpacityPercent);
        Assert.Equal(80, secondSettings.BackgroundImage?.OpacityPercent);
    }

    [AvaloniaFact]
    public async Task Global_opacity_save_failure_restores_last_persisted_value()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data-failure")).FullName;
        var appearance = Directory.CreateDirectory(Path.Combine(appData, "appearance")).FullName;
        CreateBmp(Path.Combine(appearance, "global.bmp"), 2, 2);
        var settingsPath = Path.Combine(appData, "settings.json");
        await CrystalflySettingsStore.SaveAsync(
            settingsPath,
            new CrystalflySettings
            {
                OfflineMode = true,
                BackgroundImage = new BackgroundImageSettings { FileName = "global.bmp", OpacityPercent = 30 }
            });
        await using var viewModel = CreateViewModel(appData);
        await viewModel.InitializeAsync();
        await using var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None);

        viewModel.BackgroundOpacityPercent = 61;
        await viewModel.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(30, viewModel.BackgroundOpacityPercent);
        Assert.Equal(0.30, viewModel.ActiveBackgroundOpacity, 3);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [AvaloniaFact]
    public async Task Global_opacity_survives_restart()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data-restart")).FullName;
        var appearance = Directory.CreateDirectory(Path.Combine(appData, "appearance")).FullName;
        CreateBmp(Path.Combine(appearance, "global.bmp"), 2, 2);
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                OfflineMode = true,
                BackgroundImage = new BackgroundImageSettings { FileName = "global.bmp", OpacityPercent = 30 }
            });
        await using (var first = CreateViewModel(appData))
        {
            await first.InitializeAsync();
            first.BackgroundOpacityPercent = 63;
            await Task.Delay(700);
        }

        await using var second = CreateViewModel(appData);
        await second.InitializeAsync();

        Assert.Equal(63, second.BackgroundOpacityPercent);
        Assert.Equal(0.63, second.ActiveBackgroundOpacity, 3);
    }

    [AvaloniaFact]
    public async Task Dispose_releases_loaded_background_file()
    {
        var appData = Directory.CreateDirectory(Path.Combine(root, "app-data-dispose")).FullName;
        var appearance = Directory.CreateDirectory(Path.Combine(appData, "appearance")).FullName;
        var imagePath = CreateBmp(Path.Combine(appearance, "global.bmp"), 2, 2);
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                OfflineMode = true,
                BackgroundImage = new BackgroundImageSettings { FileName = "global.bmp", OpacityPercent = 35 }
            });
        var viewModel = CreateViewModel(appData);
        await viewModel.InitializeAsync();

        await viewModel.DisposeAsync();
        File.Delete(imagePath);

        Assert.False(File.Exists(imagePath));
    }

    private static MainViewModel CreateViewModel(string appData)
    {
        var viewModel = new MainViewModel(appData);
        SetPrivateField(
            viewModel,
            "catalogLoader",
            new Func<CancellationToken, Task<GameCatalog>>(_ => Task.FromResult(new GameCatalog())));
        SetPrivateField(viewModel, "steamReconnect", new Func<Task>(() => Task.CompletedTask));
        return viewModel;
    }

    private static void SetInstanceDiscovery(MainViewModel viewModel, params InstanceRecord[] records) => SetPrivateField(
        viewModel,
        "instanceDiscovery",
        new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
            (_, _, _) => Task.FromResult<IReadOnlyList<InstanceRecord>>(records)));

    private async Task CreateInstanceBackgroundAsync(
        string versionRoot,
        string instanceId,
        string fileName,
        int opacity)
    {
        var directory = Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            instanceId,
            "appearance")).FullName;
        CreateBmp(Path.Combine(directory, fileName), 2, 2);
        await InstanceAppearanceSettingsStore.SaveAsync(
            Path.Combine(directory, "appearance.json"),
            new InstanceAppearanceSettings
            {
                BackgroundImage = new BackgroundImageSettings
                {
                    FileName = fileName,
                    OpacityPercent = opacity
                }
            });
    }

    private static string InstanceAppearancePath(string versionRoot, string instanceId) => Path.Combine(
        versionRoot,
        ".crystalfly",
        "instances",
        instanceId,
        "appearance",
        "appearance.json");

    private static InstanceRecord Instance(string id, string versionRoot) => new()
    {
        Id = id,
        Name = "Practice",
        RootPath = Directory.CreateDirectory(Path.Combine(versionRoot, id)).FullName,
        BuildId = "1.5.78.11833",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static void SetPrivateField<T>(MainViewModel viewModel, string name, T value)
    {
        var field = typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, value);
    }

    private static string CreateBmp(string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var rowSize = (width * 3 + 3) & ~3;
        var bytes = new byte[54 + rowSize * height];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 24);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
