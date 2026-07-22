using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Mods;

public enum PresetApplyStepKind
{
    Install,
    Enable,
    Disable,
    Unresolved,
    Blocked
}

public enum PresetApplyStepState
{
    Pending,
    Satisfied,
    Unresolved,
    Blocked
}

public sealed record PresetApplyStep
{
    public required PresetApplyStepKind Kind { get; init; }

    public required PresetApplyStepState State { get; init; }

    public required string ModId { get; init; }

    public string? Version { get; init; }

    public string? LoaderId { get; init; }

    public required string Reason { get; init; }
}

public sealed record PresetModState
{
    public required string ModId { get; init; }

    public required bool Enabled { get; init; }

    public bool WasInstalled { get; init; } = true;
}

public sealed record PresetApplyPlan
{
    public required ModPreset Preset { get; init; }

    public required IReadOnlyList<PresetApplyStep> Steps { get; init; }

    public required IReadOnlyList<PresetModState> PreApplyStates { get; init; }

    public bool IsBlocked => Steps.Any(step => step.State == PresetApplyStepState.Blocked);
}

public sealed class ModPresetService
{
    private const int RestorePointSchemaVersion = 1;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> StorageGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly InstanceRecord _instance;
    private readonly IReadOnlyList<ModManifest> _catalog;
    private readonly LoaderManager _loaderManager;
    private readonly ModManager _modManager;
    private readonly string _presetsRoot;
    private readonly SemaphoreSlim _storageGate;

    public ModPresetService(
        InstanceRecord instance,
        IReadOnlyList<ModManifest> catalog,
        LoaderManager loaderManager,
        ModManager modManager,
        string presetsRoot)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _loaderManager = loaderManager ?? throw new ArgumentNullException(nameof(loaderManager));
        _modManager = modManager ?? throw new ArgumentNullException(nameof(modManager));
        _presetsRoot = Path.GetFullPath(presetsRoot);
        _storageGate = StorageGates.GetOrAdd(_presetsRoot, static _ => new SemaphoreSlim(1, 1));
    }

    public async Task<IReadOnlyList<ModPreset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_presetsRoot)) return [];
        var presets = new List<ModPreset>();
        foreach (var path in Directory.EnumerateFiles(_presetsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var preset = await ReadPresetAsync(path, cancellationToken);
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        PresetFileName(preset.Id),
                        StringComparison.OrdinalIgnoreCase))
                {
                    presets.Add(preset);
                }
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or JsonException
                or UnauthorizedAccessException)
            {
                // A damaged preset must not make the remaining instance presets unavailable.
            }
        }
        return presets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<ModPreset> CreateAsync(ModPreset preset, CancellationToken cancellationToken = default)
    {
        Validate(preset);
        await _storageGate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(PresetPath(preset.Id))
                || (await GetAllAsync(cancellationToken)).Any(candidate =>
                    string.Equals(candidate.Id, preset.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Mod preset '{preset.Id}' already exists.");
            }
            await AtomicJsonStore.WriteAsync(PresetPath(preset.Id), preset, cancellationToken);
            return preset;
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task<ModPreset> UpdateAsync(ModPreset preset, CancellationToken cancellationToken = default)
    {
        Validate(preset);
        await _storageGate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(PresetPath(preset.Id)))
            {
                throw new KeyNotFoundException($"Mod preset '{preset.Id}' was not found.");
            }
            await AtomicJsonStore.WriteAsync(PresetPath(preset.Id), preset, cancellationToken);
            return preset;
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task<ModPreset> CopyAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var source = await GetAsync(id, cancellationToken);
        return await CreateAsync(source with { Id = Guid.NewGuid().ToString("N"), Name = name }, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _storageGate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(PresetPath(id)))
            {
                throw new KeyNotFoundException($"Mod preset '{id}' was not found.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(PresetPath(id));
            File.Delete(PresetPath(id) + ".bak");
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task<string> ExportAsync(string id, CancellationToken cancellationToken = default)
    {
        var preset = await GetAsync(id, cancellationToken);
        return JsonSerializer.Serialize(preset, CrystalflyJson.Options);
    }

    public async Task<ModPreset> ImportAsync(string document, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        if (Encoding.UTF8.GetByteCount(document) > ModPreset.MaxDocumentBytes)
        {
            throw new InvalidDataException(
                $"Preset JSON cannot exceed {ModPreset.MaxDocumentBytes} bytes.");
        }
        var imported = JsonSerializer.Deserialize<ModPreset>(document, CrystalflyJson.Options)
            ?? throw new JsonException("Preset JSON did not contain a preset.");
        Validate(imported);
        return await CreateAsync(imported with { Id = Guid.NewGuid().ToString("N") }, cancellationToken);
    }

    public async Task<ModPreset> ImportFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = new FileInfo(Path.GetFullPath(path));
        if (!file.Exists)
        {
            throw new FileNotFoundException("Preset file was not found.", file.FullName);
        }
        if (file.Length > ModPreset.MaxDocumentBytes)
        {
            throw new InvalidDataException(
                $"Preset JSON cannot exceed {ModPreset.MaxDocumentBytes} bytes.");
        }
        return await ImportAsync(
            await File.ReadAllTextAsync(file.FullName, cancellationToken),
            cancellationToken);
    }

    public async Task<ModPreset> CaptureAsync(
        string name,
        ModPresetApplyMode applyMode,
        CancellationToken cancellationToken = default)
    {
        var preset = await CaptureDefinitionAsync(
            Guid.NewGuid().ToString("N"),
            name,
            applyMode,
            cancellationToken);
        return await CreateAsync(preset, cancellationToken);
    }

    public async Task<ModPreset> RecaptureAsync(
        string id,
        string name,
        ModPresetApplyMode applyMode,
        CancellationToken cancellationToken = default)
    {
        _ = await GetAsync(id, cancellationToken);
        var preset = await CaptureDefinitionAsync(id, name, applyMode, cancellationToken);
        return await UpdateAsync(preset, cancellationToken);
    }

    private async Task<ModPreset> CaptureDefinitionAsync(
        string id,
        string name,
        ModPresetApplyMode applyMode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var loader = await _loaderManager.InspectAsync(cancellationToken);
        if (loader.PackageId is null) throw new InvalidOperationException("A matching loader is required to capture a mod preset.");
        var entries = (await _modManager.GetInstalledAsync(cancellationToken)).Where(receipt => receipt.Enabled)
            .Select(receipt => new ModPresetEntry
            {
                Id = receipt.Ownership == ModOwnership.Managed ? receipt.Id : null,
                Name = receipt.Name,
                Version = receipt.Ownership == ModOwnership.Managed ? receipt.Version : null,
                FileHashes = receipt.Ownership == ModOwnership.Managed
                    ? []
                    : receipt.Files.Select(file => file.Sha256).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            })
            .ToList();
        var discovery = await _modManager.DiscoverAsync(loader.PackageId, cancellationToken);
        foreach (var external in discovery.Mods.Where(mod =>
                     mod.Ownership == ModOwnership.External && mod.Enabled))
        {
            var hashes = new List<string>(external.Files.Count);
            foreach (var file in external.Files.Order(StringComparer.OrdinalIgnoreCase))
            {
                hashes.Add(await HashInstanceFileAsync(file, cancellationToken));
            }
            entries.Add(new ModPresetEntry
            {
                Name = external.Name,
                FileHashes = hashes.Order(StringComparer.OrdinalIgnoreCase).ToArray()
            });
        }
        return new ModPreset
        {
            Id = id, Name = name, GameBuildId = _instance.BuildId,
            LoaderId = loader.PackageId, ApplyMode = applyMode, Entries = entries
        };
    }

    public async Task<PresetApplyPlan> CreatePlanAsync(ModPreset preset, CancellationToken cancellationToken = default)
    {
        Validate(preset);
        var installed = await _modManager.GetInstalledAsync(cancellationToken);
        var before = installed.Select(receipt => new PresetModState { ModId = receipt.Id, Enabled = receipt.Enabled }).ToArray();
        var steps = new List<PresetApplyStep>();
        if (!string.Equals(preset.GameBuildId, _instance.BuildId, StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(preset, before, $"Preset build '{preset.GameBuildId}' does not match instance build '{_instance.BuildId}'.");
        }
        var loader = await _loaderManager.InspectAsync(cancellationToken);
        if (loader.State is not (LoaderState.ModdingApi or LoaderState.BepInEx)
            || !string.Equals(loader.PackageId, preset.LoaderId, StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(preset, before, $"Installed loader '{loader.PackageId ?? loader.State.ToString()}' does not match '{preset.LoaderId}'.");
        }
        var installedById = installed.ToDictionary(receipt => receipt.Id, StringComparer.OrdinalIgnoreCase);
        var requestedEntries = preset.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .ToArray();
        foreach (var entry in requestedEntries)
        {
            var exact = _catalog.SingleOrDefault(manifest =>
                string.Equals(manifest.Id, entry.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(manifest.Version, entry.Version, StringComparison.OrdinalIgnoreCase)
                && string.Equals(manifest.LoaderId, preset.LoaderId, StringComparison.OrdinalIgnoreCase)
                && manifest.SupportedBuildIds.Contains(_instance.BuildId, StringComparer.OrdinalIgnoreCase));
            if (exact is null)
            {
                return Blocked(
                    preset,
                    before,
                    $"Mod '{entry.Id}' version '{entry.Version}' is not available for this preset binding.");
            }
        }
        var requestedManaged = requestedEntries.Select(entry => entry.Id!).ToArray();
        IReadOnlyList<ModManifest> order;
        try { order = ModDependencyResolver.ResolveInstallOrder(_catalog, requestedManaged); }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidDataException)
        {
            return Blocked(preset, before, exception.Message);
        }
        var requiredIds = new HashSet<string>(order.Select(manifest => manifest.Id), StringComparer.OrdinalIgnoreCase);
        var discovered = await _modManager.DiscoverAsync(preset.LoaderId, cancellationToken);
        foreach (var entry in preset.Entries.Where(entry => string.IsNullOrWhiteSpace(entry.Id)))
        {
            var receipt = installed.FirstOrDefault(candidate => candidate.Ownership != ModOwnership.Managed
                && string.Equals(candidate.Name, entry.Name, StringComparison.Ordinal)
                && HashesEqual(candidate, entry));
            if (receipt is null)
            {
                ModDiscoveryEntry? matchingExternal = null;
                foreach (var external in discovered.Mods.Where(candidate =>
                             candidate.Ownership == ModOwnership.External
                             && candidate.Enabled
                             && string.Equals(candidate.Name, entry.Name, StringComparison.Ordinal)))
                {
                    if ((await ExternalHashesAsync(external, cancellationToken)).SequenceEqual(
                            entry.FileHashes.Order(StringComparer.OrdinalIgnoreCase),
                            StringComparer.OrdinalIgnoreCase))
                    {
                        matchingExternal = external;
                        break;
                    }
                }
                if (matchingExternal is not null)
                {
                    steps.Add(new PresetApplyStep
                    {
                        Kind = PresetApplyStepKind.Enable,
                        State = PresetApplyStepState.Satisfied,
                        ModId = matchingExternal.Id,
                        Version = "local",
                        LoaderId = matchingExternal.LoaderId,
                        Reason = "Matching external mod is already enabled."
                    });
                    continue;
                }
                steps.Add(new PresetApplyStep
                {
                    Kind = PresetApplyStepKind.Unresolved,
                    State = PresetApplyStepState.Unresolved,
                    ModId = entry.Name,
                    Reason = $"Local or external mod '{entry.Name}' is not available on this instance."
                });
                continue;
            }
            requiredIds.Add(receipt.Id);
            AddInstalledDependencies(receipt.Id, installedById, requiredIds);
            steps.Add(new PresetApplyStep
            {
                Kind = PresetApplyStepKind.Enable,
                State = receipt.Enabled ? PresetApplyStepState.Satisfied : PresetApplyStepState.Pending,
                ModId = receipt.Id,
                Version = receipt.Version,
                LoaderId = receipt.LoaderId,
                Reason = receipt.Enabled ? "Mod is already enabled." : "Mod will be enabled."
            });
        }
        foreach (var manifest in order)
        {
            if (!string.Equals(manifest.LoaderId, preset.LoaderId, StringComparison.OrdinalIgnoreCase)
                || !manifest.SupportedBuildIds.Contains(_instance.BuildId, StringComparer.OrdinalIgnoreCase))
            {
                return Blocked(preset, before, $"Mod '{manifest.Id}' is not compatible with this preset binding.");
            }
            installedById.TryGetValue(manifest.Id, out var receipt);
            if (receipt is null && before.All(state =>
                    !string.Equals(state.ModId, manifest.Id, StringComparison.OrdinalIgnoreCase)))
            {
                before = [.. before, new PresetModState
                {
                    ModId = manifest.Id,
                    Enabled = false,
                    WasInstalled = false
                }];
            }
            if (receipt is not null && receipt.Ownership != ModOwnership.Managed)
            {
                return Blocked(preset, before, $"Local or external mod '{manifest.Id}' cannot be updated from the catalog.");
            }
            if (receipt is not null
                && (!string.Equals(receipt.Version, manifest.Version, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(receipt.LoaderId, manifest.LoaderId, StringComparison.OrdinalIgnoreCase)))
            {
                return Blocked(
                    preset,
                    before,
                    $"Installed mod '{manifest.Id}' does not match preset version '{manifest.Version}' and loader '{manifest.LoaderId}'.");
            }
            var installedMatching = receipt is not null;
            steps.Add(new PresetApplyStep
            {
                Kind = installedMatching ? PresetApplyStepKind.Enable : PresetApplyStepKind.Install,
                State = receipt?.Enabled == true
                    ? PresetApplyStepState.Satisfied
                    : PresetApplyStepState.Pending,
                ModId = manifest.Id,
                Version = manifest.Version,
                LoaderId = manifest.LoaderId,
                Reason = receipt?.Enabled == true
                    ? "Mod is already enabled."
                    : installedMatching
                        ? "Mod will be enabled."
                        : "Mod and dependencies will use the install queue."
            });
        }
        if (preset.ApplyMode == ModPresetApplyMode.Exact)
        {
            foreach (var pinned in installed.Where(receipt => receipt.Enabled && receipt.Pinned))
            {
                requiredIds.Add(pinned.Id);
                AddInstalledDependencies(pinned.Id, installedById, requiredIds);
            }
            var disableIds = installed
                .Where(receipt => receipt.Enabled
                    && !receipt.Pinned
                    && !requiredIds.Contains(receipt.Id))
                .Select(receipt => receipt.Id)
                .ToArray();
            foreach (var receipt in InstalledModDependencyGraph.OrderDependentsFirst(installed, disableIds))
            {
                steps.Add(new PresetApplyStep
                {
                    Kind = PresetApplyStepKind.Disable,
                    State = PresetApplyStepState.Pending,
                    ModId = receipt.Id,
                    Version = receipt.Version,
                    LoaderId = receipt.LoaderId,
                    Reason = "Exact preset disables unlisted mod."
                });
            }
        }
        return new PresetApplyPlan { Preset = preset, Steps = steps, PreApplyStates = before };
    }

    public async Task<PresetApplyPlan> CreatePlanAsync(string id, CancellationToken cancellationToken = default) =>
        await CreatePlanAsync(await GetAsync(id, cancellationToken), cancellationToken);

    public async Task RestoreAsync(PresetApplyPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await RestoreStatesAsync(plan.PreApplyStates, cancellationToken);
    }

    public async Task SaveRestorePointAsync(
        PresetApplyPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.IsBlocked)
        {
            throw new InvalidOperationException("A blocked preset plan cannot create a restore point.");
        }
        await WriteRestorePointAsync(plan.Preset.Id, plan.PreApplyStates, cancellationToken);
    }

    public async Task CaptureRestorePointAsync(
        string presetId,
        IEnumerable<string> affectedModIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetId);
        ArgumentNullException.ThrowIfNull(affectedModIds);
        var affected = affectedModIds
            .Select(id => string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Affected Mod IDs cannot contain an empty value.", nameof(affectedModIds))
                : id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (affected.Length == 0)
        {
            throw new ArgumentException("At least one affected Mod ID is required.", nameof(affectedModIds));
        }
        var installed = (await _modManager.GetInstalledAsync(cancellationToken))
            .ToDictionary(receipt => receipt.Id, StringComparer.OrdinalIgnoreCase);
        var states = affected.Select(id => installed.TryGetValue(id, out var receipt)
            ? new PresetModState
            {
                ModId = receipt.Id,
                Enabled = receipt.Enabled,
                WasInstalled = true
            }
            : new PresetModState
            {
                ModId = id,
                Enabled = false,
                WasInstalled = false
            }).ToArray();
        await WriteRestorePointAsync(presetId, states, cancellationToken);
    }

    public Task<bool> HasRestorePointAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(RestorePointPath) || File.Exists(RestorePointPath + ".bak"));
    }

    public async Task RestoreLastAsync(CancellationToken cancellationToken = default)
    {
        var restorePoint = await AtomicJsonStore.ReadAsync<PresetRestorePoint>(
            RestorePointPath,
            cancellationToken);
        if (restorePoint.SchemaVersion != RestorePointSchemaVersion || restorePoint.States is null)
        {
            throw new InvalidDataException("Unsupported or invalid preset restore point.");
        }
        await RestoreStatesAsync(restorePoint.States, cancellationToken);
    }

    private async Task<ModPreset> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var preset = (await GetAllAsync(cancellationToken)).FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
        return preset ?? throw new KeyNotFoundException($"Mod preset '{id}' was not found.");
    }

    private string PresetPath(string id) => Path.Combine(_presetsRoot, $"{PresetFileName(id)}.json");

    private string RestorePointPath => Path.Combine(_presetsRoot, ".state", "last-apply.json");

    private static string PresetFileName(string id) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));

    private static async Task<ModPreset> ReadPresetAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var preset = await AtomicJsonStore.ReadAsync<ModPreset>(path, cancellationToken);
            Validate(preset);
            return preset;
        }
        catch (InvalidDataException) when (File.Exists(path + ".bak"))
        {
            var backup = await AtomicJsonStore.ReadAsync<ModPreset>(path + ".bak", cancellationToken);
            Validate(backup);
            return backup;
        }
    }

    private static PresetApplyPlan Blocked(ModPreset preset, IReadOnlyList<PresetModState> before, string reason) => new()
    {
        Preset = preset, PreApplyStates = before,
        Steps = [new PresetApplyStep { Kind = PresetApplyStepKind.Blocked, State = PresetApplyStepState.Blocked, ModId = string.Empty, Reason = reason }]
    };

    private static void AddInstalledDependencies(string id, IReadOnlyDictionary<string, InstalledModReceipt> installed, ISet<string> required)
    {
        if (!installed.TryGetValue(id, out var receipt)) return;
        foreach (var dependency in receipt.Dependencies)
            if (required.Add(dependency)) AddInstalledDependencies(dependency, installed, required);
    }

    private static bool HashesEqual(InstalledModReceipt receipt, ModPresetEntry entry) =>
        receipt.Files.Select(file => file.Sha256).Order(StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(entry.FileHashes.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

    private static void Validate(ModPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.SchemaVersion != ModPreset.CurrentSchemaVersion) throw new InvalidDataException("Unsupported mod preset schema.");
        Required(preset.Id, "Preset ID", ModPreset.MaxPresetIdLength);
        Required(preset.Name, "Preset name", ModPreset.MaxPresetNameLength);
        Required(preset.GameBuildId, "Preset build ID", ModPreset.MaxBuildIdLength);
        Required(preset.LoaderId, "Preset loader ID", ModPreset.MaxLoaderIdLength);
        if (!Enum.IsDefined(preset.ApplyMode)) throw new InvalidDataException("Preset apply mode is invalid.");
        if (preset.Entries is null) throw new InvalidDataException("Preset entries are required.");
        if (preset.Entries.Count > ModPreset.MaxEntries) throw new InvalidDataException($"Preset entries cannot exceed {ModPreset.MaxEntries} items.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localEntries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in preset.Entries)
        {
            if (entry is null) throw new InvalidDataException("Preset contains a null entry.");
            Required(entry.Name, "Preset entry name", ModPreset.MaxEntryNameLength);
            if (entry.Id is not null
                && (string.IsNullOrWhiteSpace(entry.Id)
                    || entry.Id.Length > ModPreset.MaxEntryIdLength
                    || string.IsNullOrWhiteSpace(entry.Version)
                    || entry.Version.Length > ModPreset.MaxEntryVersionLength
                    || !ids.Add(entry.Id))) throw new InvalidDataException($"Preset entry '{entry.Name}' is invalid.");
            if (entry.Id is null && entry.Version is not null) throw new InvalidDataException($"Local or external preset entry '{entry.Name}' may only store file hashes.");
            if (entry.FileHashes is null
                || entry.FileHashes.Count > ModPreset.MaxFileHashesPerEntry
                || entry.FileHashes.Any(hash => string.IsNullOrWhiteSpace(hash) || hash.Length != 64 || !IsHash(hash)))
            {
                throw new InvalidDataException($"Preset entry '{entry.Name}' is invalid.");
            }
            if (entry.Id is null)
            {
                if (entry.FileHashes.Count == 0)
                {
                    throw new InvalidDataException($"Local or external preset entry '{entry.Name}' requires file hashes.");
                }
                var localKey = $"{entry.Name}\n{string.Join('\n', entry.FileHashes.Order(StringComparer.OrdinalIgnoreCase))}";
                if (!localEntries.Add(localKey))
                {
                    throw new InvalidDataException($"Local or external preset entry '{entry.Name}' is duplicated.");
                }
            }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(preset, CrystalflyJson.Options).Length > ModPreset.MaxDocumentBytes)
        {
            throw new InvalidDataException(
                $"Preset JSON cannot exceed {ModPreset.MaxDocumentBytes} bytes.");
        }
    }

    private static bool IsHash(string value) { try { return Convert.FromHexString(value).Length == 32; } catch (FormatException) { return false; } }
    private static void Required(string? value, string label, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidDataException($"{label} is required and cannot exceed {maximumLength} characters.");
        }
    }

    private async Task<string> HashInstanceFileAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(_instance.RootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new InvalidDataException($"External mod file is outside the instance or missing: '{relativePath}'.");
        }
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private async Task<IReadOnlyList<string>> ExternalHashesAsync(
        ModDiscoveryEntry external,
        CancellationToken cancellationToken)
    {
        var hashes = new List<string>(external.Files.Count);
        foreach (var file in external.Files.Order(StringComparer.OrdinalIgnoreCase))
        {
            hashes.Add(await HashInstanceFileAsync(file, cancellationToken));
        }
        return hashes.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task RestoreStatesAsync(
        IReadOnlyList<PresetModState> states,
        CancellationToken cancellationToken)
    {
        var installed = await _modManager.GetInstalledAsync(cancellationToken);
        var current = installed.ToDictionary(receipt => receipt.Id, StringComparer.OrdinalIgnoreCase);
        var stateById = states.ToDictionary(state => state.ModId, StringComparer.OrdinalIgnoreCase);
        await ValidateRestorePreconditionsAsync(states, installed, current, cancellationToken);
        var uninstallIds = states.Where(state =>
                !state.WasInstalled && current.ContainsKey(state.ModId))
            .Select(state => state.ModId)
            .ToArray();
        foreach (var receipt in InstalledModDependencyGraph.OrderDependentsFirst(installed, uninstallIds))
        {
            await _modManager.UninstallIgnoringDependentsAsync(receipt.Id, cancellationToken);
        }

        installed = await _modManager.GetInstalledAsync(cancellationToken);
        current = installed.ToDictionary(receipt => receipt.Id, StringComparer.OrdinalIgnoreCase);
        var disableIds = states.Where(state =>
                state.WasInstalled
                && !state.Enabled
                && current.TryGetValue(state.ModId, out var receipt)
                && receipt.Enabled)
            .Select(state => state.ModId)
            .ToArray();
        foreach (var receipt in InstalledModDependencyGraph.OrderDependentsFirst(installed, disableIds))
        {
            await _modManager.DisableIgnoringDependentsAsync(receipt.Id, cancellationToken);
        }
        var enableIds = states.Where(state =>
                state.WasInstalled
                && state.Enabled
                && current.TryGetValue(state.ModId, out var receipt)
                && !receipt.Enabled)
            .Select(state => state.ModId)
            .ToArray();
        foreach (var receipt in InstalledModDependencyGraph
                     .OrderDependentsFirst(installed, enableIds)
                     .Reverse())
        {
            if (stateById[receipt.Id].Enabled)
            {
                await _modManager.SetEnabledAsync(receipt.Id, enabled: true, cancellationToken);
            }
        }
    }

    private async Task ValidateRestorePreconditionsAsync(
        IReadOnlyList<PresetModState> states,
        IReadOnlyList<InstalledModReceipt> installed,
        IReadOnlyDictionary<string, InstalledModReceipt> current,
        CancellationToken cancellationToken)
    {
        foreach (var state in states.Where(state => state.WasInstalled))
        {
            if (!current.ContainsKey(state.ModId))
            {
                throw new InvalidOperationException(
                    $"Mod '{state.ModId}' was installed before applying the preset but is now missing.");
            }
        }

        var changing = states
            .Where(state => current.TryGetValue(state.ModId, out var receipt)
                && (!state.WasInstalled || receipt.Enabled != state.Enabled))
            .Select(state => current[state.ModId])
            .DistinctBy(receipt => receipt.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pinnedRemoval = changing.FirstOrDefault(receipt =>
            receipt.Pinned
            && states.Any(state =>
                !state.WasInstalled
                && string.Equals(state.ModId, receipt.Id, StringComparison.OrdinalIgnoreCase)));
        if (pinnedRemoval is not null)
        {
            throw new InvalidOperationException(
                $"Pinned mod '{pinnedRemoval.Id}' must be unpinned before restoring the preset state.");
        }

        var healthService = new ModHealthService(_instance.RootPath);
        foreach (var receipt in changing.Where(receipt => receipt.Files.Count != 0))
        {
            var health = await healthService.AssessAsync(receipt, installed, cancellationToken);
            if (health.Status is not (ModHealthStatus.Healthy or ModHealthStatus.ExtraFile))
            {
                throw new InvalidOperationException(
                    $"Mod '{receipt.Id}' must be healthy before restoring the preset state: {health.Status}.");
            }
        }
    }

    private Task WriteRestorePointAsync(
        string presetId,
        IReadOnlyList<PresetModState> states,
        CancellationToken cancellationToken) => AtomicJsonStore.WriteAsync(
            RestorePointPath,
            new PresetRestorePoint
            {
                SchemaVersion = RestorePointSchemaVersion,
                PresetId = presetId,
                CreatedAt = DateTimeOffset.UtcNow,
                States = states
            },
            cancellationToken);

    private sealed record PresetRestorePoint
    {
        public required int SchemaVersion { get; init; }

        public required string PresetId { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required IReadOnlyList<PresetModState> States { get; init; }
    }
}
