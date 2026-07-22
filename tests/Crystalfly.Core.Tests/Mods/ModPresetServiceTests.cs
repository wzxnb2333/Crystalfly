using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModPresetServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Create_copy_export_and_import_preserve_only_portable_entry_metadata()
    {
        var service = CreateService();
        var preset = Preset("speedrun", ModPresetApplyMode.Exact,
        [
            new ModPresetEntry
            {
                Name = "Local A",
                FileHashes = [new string('A', 64)]
            }
        ]);

        var created = await service.CreateAsync(preset);
        var copied = await service.CopyAsync(created.Id, "copy");
        var exported = await service.ExportAsync(created.Id);
        var imported = await service.ImportAsync(exported);

        Assert.Equal("speedrun", created.Name);
        Assert.NotEqual(created.Id, copied.Id);
        Assert.Equal("copy", copied.Name);
        Assert.NotEqual(created.Id, imported.Id);
        Assert.Equal(created.Entries.Select(entry => entry.Id), imported.Entries.Select(entry => entry.Id));
        Assert.Equal(created.Entries.Single().FileHashes, imported.Entries.Single().FileHashes);
        Assert.DoesNotContain("Path", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Url", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Binary", exported, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePlan_exact_disables_unlisted_unpinned_mod_and_keeps_pre_apply_state()
    {
        await WriteManagedLoaderAsync();
        await WriteInstalledModAsync("wanted", enabled: true);
        await WriteInstalledModAsync("unlisted", enabled: true);
        await WriteInstalledModAsync("pinned", enabled: true, pinned: true);
        var service = CreateService([Manifest("wanted"), Manifest("unlisted"), Manifest("pinned")]);

        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Exact,
        [new ModPresetEntry { Id = "wanted", Name = "Wanted", Version = "1.0" }]));

        Assert.False(plan.IsBlocked);
        Assert.Contains(plan.Steps, step => step.Kind == PresetApplyStepKind.Disable && step.ModId == "unlisted");
        Assert.DoesNotContain(plan.Steps, step => step.ModId == "pinned" && step.Kind == PresetApplyStepKind.Disable);
        Assert.Equal(["pinned", "unlisted", "wanted"], plan.PreApplyStates.Select(state => state.ModId));
    }

    [Fact]
    public async Task CreatePlan_exact_keeps_transitive_dependencies_of_enabled_pinned_mods()
    {
        await WriteManagedLoaderAsync();
        await WriteInstalledModAsync("base", enabled: true);
        await WriteInstalledModAsync("feature", enabled: true, pinned: true, dependencies: ["base"]);
        var service = CreateService(
        [
            Manifest("base"),
            Manifest("feature") with { Dependencies = ["base"] }
        ]);

        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Exact, []));

        Assert.DoesNotContain(plan.Steps, step =>
            step.Kind == PresetApplyStepKind.Disable
            && step.ModId is "base" or "feature");
    }

    [Fact]
    public async Task CreatePlan_exact_orders_disabled_dependents_before_dependencies()
    {
        await WriteManagedLoaderAsync();
        await WriteInstalledModAsync("base", enabled: true);
        await WriteInstalledModAsync("feature", enabled: true, dependencies: ["base"]);
        var service = CreateService(
        [
            Manifest("base"),
            Manifest("feature") with { Dependencies = ["base"] }
        ]);

        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Exact, []));

        Assert.Equal(["feature", "base"], plan.Steps
            .Where(step => step.Kind == PresetApplyStepKind.Disable)
            .Select(step => step.ModId));
    }

    [Fact]
    public async Task CreatePlan_blocks_different_build_or_loader()
    {
        await WriteManagedLoaderAsync();
        var service = CreateService([Manifest("wanted")]);
        var wrongBuild = Preset("preset", ModPresetApplyMode.Append,
            [new ModPresetEntry { Id = "wanted", Name = "Wanted", Version = "1.0" }]) with
        { GameBuildId = "other" };
        var wrongLoader = wrongBuild with { GameBuildId = "test", LoaderId = "bepinex-5.4.0" };

        Assert.True((await service.CreatePlanAsync(wrongBuild)).IsBlocked);
        Assert.True((await service.CreatePlanAsync(wrongLoader)).IsBlocked);
    }

    [Fact]
    public async Task CreatePlan_adds_missing_catalog_dependencies_using_install_step()
    {
        await WriteManagedLoaderAsync();
        var service = CreateService(
        [
            Manifest("wanted") with { Dependencies = ["dependency"] },
            Manifest("dependency")
        ]);

        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Append,
        [new ModPresetEntry { Id = "wanted", Name = "Wanted", Version = "1.0" }]));

        Assert.Equal(["dependency", "wanted"], plan.Steps
            .Where(step => step.Kind == PresetApplyStepKind.Install)
            .Select(step => step.ModId));
    }

    [Fact]
    public async Task CreatePlan_blocks_when_catalog_does_not_contain_the_preset_version()
    {
        await WriteManagedLoaderAsync();
        var service = CreateService([Manifest("wanted") with { Version = "2.0" }]);

        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Append,
        [new ModPresetEntry { Id = "wanted", Name = "Wanted", Version = "1.0" }]));

        Assert.True(plan.IsBlocked);
        Assert.Contains("1.0", Assert.Single(plan.Steps).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePlan_keeps_missing_local_entry_unresolved_without_blocking_official_mods()
    {
        await WriteManagedLoaderAsync();
        var service = CreateService([Manifest("wanted")]);

        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Append,
        [
            new ModPresetEntry { Id = "wanted", Name = "Wanted", Version = "1.0" },
            new ModPresetEntry { Name = "Local helper", FileHashes = [new string('B', 64)] }
        ]));

        Assert.False(plan.IsBlocked);
        Assert.Contains(plan.Steps, step => step.Kind == PresetApplyStepKind.Install && step.ModId == "wanted");
        Assert.Contains(plan.Steps, step => step.Kind == PresetApplyStepKind.Unresolved && step.ModId == "Local helper");
    }

    [Fact]
    public async Task Restore_point_survives_service_recreation_and_restores_enabled_state()
    {
        await WriteManagedLoaderAsync();
        await WriteInstalledModAsync("enabled", enabled: true);
        await WriteInstalledModAsync("disabled", enabled: false);
        var service = CreateService([Manifest("enabled"), Manifest("disabled")]);
        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Exact,
        [new ModPresetEntry { Id = "disabled", Name = "Disabled", Version = "1.0" }]));
        await service.SaveRestorePointAsync(plan);

        var manager = new ModManager(
            InstanceRoot,
            Path.Combine(root, "transactions"),
            Path.Combine(root, "state", "mods"));
        await manager.DisableIgnoringDependentsAsync("enabled");
        await manager.SetEnabledAsync("disabled", enabled: true);

        await CreateService([Manifest("enabled"), Manifest("disabled")]).RestoreLastAsync();

        var installed = await manager.GetInstalledAsync();
        Assert.True(installed.Single(receipt => receipt.Id == "enabled").Enabled);
        Assert.False(installed.Single(receipt => receipt.Id == "disabled").Enabled);
    }

    [Fact]
    public async Task Restore_point_uninstalls_mod_that_was_missing_when_execution_started()
    {
        await WriteManagedLoaderAsync();
        var service = CreateService([Manifest("new-mod")]);
        await service.CaptureRestorePointAsync("preset", ["new-mod"]);
        await WriteInstalledModAsync("new-mod", enabled: true);

        await service.RestoreLastAsync();

        Assert.Empty(await new ModManager(
            InstanceRoot,
            Path.Combine(root, "transactions"),
            Path.Combine(root, "state", "mods")).GetInstalledAsync());
    }

    [Fact]
    public async Task Restore_preflight_rejects_pinned_new_mod_before_removing_other_mods()
    {
        await WriteManagedLoaderAsync();
        var service = CreateService([Manifest("base"), Manifest("feature")]);
        await service.CaptureRestorePointAsync("preset", ["base", "feature"]);
        await WriteInstalledModAsync("base", enabled: true, pinned: true);
        await WriteInstalledModAsync("feature", enabled: true, dependencies: ["base"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreLastAsync());

        var installed = await new ModManager(
            InstanceRoot,
            Path.Combine(root, "transactions"),
            Path.Combine(root, "state", "mods")).GetInstalledAsync();
        Assert.Equal(["base", "feature"], installed.Select(receipt => receipt.Id));
    }

    [Fact]
    public async Task Capture_records_unmanaged_external_mod_as_name_and_hash_only()
    {
        await WriteManagedLoaderAsync();
        var externalPath = Path.Combine(
            InstanceRoot,
            "hollow_knight_Data",
            "Managed",
            "Mods",
            "External helper",
            "ExternalHelper.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(externalPath)!);
        await File.WriteAllTextAsync(externalPath, "external");

        var captured = await CreateService().CaptureAsync("capture", ModPresetApplyMode.Append);

        var entry = Assert.Single(captured.Entries);
        Assert.Null(entry.Id);
        Assert.Null(entry.Version);
        Assert.Equal("External helper", entry.Name);
        Assert.Equal(Hash(externalPath), Assert.Single(entry.FileHashes));

        var plan = await CreateService().CreatePlanAsync(captured);
        Assert.False(plan.IsBlocked);
        Assert.Contains(plan.Steps, step =>
            step.State == PresetApplyStepState.Satisfied
            && step.Kind == PresetApplyStepKind.Enable);
    }

    [Fact]
    public async Task Restore_enables_dependencies_before_dependents()
    {
        await WriteManagedLoaderAsync();
        await WriteInstalledModAsync("z-base", enabled: true);
        await WriteInstalledModAsync("a-feature", enabled: true, dependencies: ["z-base"]);
        var service = CreateService([Manifest("z-base"), Manifest("a-feature") with { Dependencies = ["z-base"] }]);
        var plan = await service.CreatePlanAsync(Preset("preset", ModPresetApplyMode.Append,
        [new ModPresetEntry { Id = "a-feature", Name = "Feature", Version = "1.0" }]));
        await service.SaveRestorePointAsync(plan);
        var manager = new ModManager(InstanceRoot, Path.Combine(root, "transactions"), Path.Combine(root, "state", "mods"));
        await manager.DisableIgnoringDependentsAsync("a-feature");
        await manager.DisableIgnoringDependentsAsync("z-base");

        await service.RestoreLastAsync();

        var installed = await manager.GetInstalledAsync();
        Assert.All(installed, receipt => Assert.True(receipt.Enabled));
    }

    [Fact]
    public async Task Append_plan_never_disables_unlisted_mods()
    {
        await WriteManagedLoaderAsync();
        await WriteInstalledModAsync("unlisted", enabled: true);

        var plan = await CreateService([Manifest("wanted"), Manifest("unlisted")])
            .CreatePlanAsync(Preset("preset", ModPresetApplyMode.Append,
            [new ModPresetEntry { Id = "wanted", Name = "Wanted", Version = "1.0" }]));

        Assert.DoesNotContain(plan.Steps, step => step.Kind == PresetApplyStepKind.Disable);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"id\":\"bad\",\"name\":\"Bad\",\"gameBuildId\":\"test\",\"loaderId\":\"modding-api-77\",\"applyMode\":99,\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"id\":\"bad\",\"name\":\"Bad\",\"gameBuildId\":\"test\",\"loaderId\":\"modding-api-77\",\"applyMode\":0,\"entries\":[{\"name\":\"Local\",\"fileHashes\":[null]}]}")]
    public async Task Import_rejects_invalid_enum_and_null_hash(string document)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().ImportAsync(document));
    }

    [Fact]
    public async Task Import_rejects_document_larger_than_the_shared_service_limit()
    {
        var document = "{" + new string(' ', ModPreset.MaxDocumentBytes) + "}";

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().ImportAsync(document));
    }

    [Fact]
    public async Task Import_file_rejects_oversized_content_before_reading_it()
    {
        var path = Path.Combine(root, "oversized.json");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(path, new byte[ModPreset.MaxDocumentBytes + 1]);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().ImportFileAsync(path));
    }

    [Fact]
    public async Task Create_rejects_more_than_one_thousand_entries()
    {
        var entries = Enumerable.Range(0, ModPreset.MaxEntries + 1)
            .Select(index => new ModPresetEntry
            {
                Id = $"mod-{index}",
                Name = $"Mod {index}",
                Version = "1.0"
            })
            .ToArray();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService().CreateAsync(Preset("large", ModPresetApplyMode.Append, entries)));
    }

    [Fact]
    public async Task Create_rejects_overlong_entry_fields()
    {
        var preset = Preset("large", ModPresetApplyMode.Append,
        [
            new ModPresetEntry
            {
                Id = new string('a', ModPreset.MaxEntryIdLength + 1),
                Name = "Large",
                Version = "1.0"
            }
        ]);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().CreateAsync(preset));
    }

    [Fact]
    public async Task Recapture_keeps_id_and_replaces_metadata_and_entries_from_current_instance()
    {
        await WriteManagedLoaderAsync();
        await WriteInstalledModAsync("first", enabled: true);
        var service = CreateService([Manifest("first"), Manifest("second")]);
        var created = await service.CaptureAsync("Original", ModPresetApplyMode.Append);
        await WriteInstalledModAsync("second", enabled: true);

        var updated = await service.RecaptureAsync(
            created.Id,
            "Updated",
            ModPresetApplyMode.Exact);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal(ModPresetApplyMode.Exact, updated.ApplyMode);
        Assert.Equal(["first", "second"], updated.Entries.Select(entry => entry.Id));
    }

    private ModPresetService CreateService(IReadOnlyList<ModManifest>? catalog = null)
    {
        Directory.CreateDirectory(InstanceRoot);
        return new ModPresetService(
            Instance(),
            catalog ?? [],
            new LoaderManager(InstanceRoot, Path.Combine(root, "transactions"), LoaderReceiptPath),
            new ModManager(InstanceRoot, Path.Combine(root, "transactions"), Path.Combine(root, "state", "mods")),
            Path.Combine(root, "state", "presets"));
    }

    private ModPreset Preset(string name, ModPresetApplyMode mode, IReadOnlyList<ModPresetEntry> entries) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name,
        GameBuildId = "test",
        LoaderId = "modding-api-77",
        ApplyMode = mode,
        Entries = entries
    };

    private InstanceRecord Instance() => new()
    {
        Id = "instance", Name = "Instance", RootPath = InstanceRoot, BuildId = "test", CreatedAt = DateTimeOffset.UtcNow
    };

    private string InstanceRoot => Path.Combine(root, "instance");
    private string LoaderReceiptPath => Path.Combine(root, "state", "loader.json");

    private async Task WriteManagedLoaderAsync()
    {
        var relative = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll";
        var path = Path.Combine(InstanceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "loader");
        await AtomicJsonStore.WriteAsync(LoaderReceiptPath, new InstalledPackageReceipt
        {
            PackageId = "modding-api-77", LoaderState = LoaderState.ModdingApi,
            Files = [new InstalledFileReceipt { RelativePath = relative, Sha256 = Hash(path) }]
        });
    }

    private async Task WriteInstalledModAsync(
        string id,
        bool enabled,
        bool pinned = false,
        IReadOnlyList<string>? dependencies = null)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        await AtomicJsonStore.WriteAsync(Path.Combine(root, "state", "mods", $"{hash}.json"), new InstalledModReceipt
        {
            Id = id, Name = id, Version = "1.0", LoaderId = "modding-api-77",
            InstallRoot = $"hollow_knight_Data/Managed/Mods/{id}", Enabled = enabled,
            Ownership = ModOwnership.Managed, Pinned = pinned, Dependencies = dependencies ?? [], Files = []
        });
    }

    private static ModManifest Manifest(string id) => new()
    {
        Id = id, Name = id, Version = "1.0", DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('A', 64), LoaderId = "modding-api-77", SupportedBuildIds = ["test"]
    };

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
