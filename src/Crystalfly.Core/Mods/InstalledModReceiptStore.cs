using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Mods;

internal static class InstalledModReceiptStore
{
    public static async Task<IReadOnlyList<InstalledModReceipt>> ReadAllAsync(
        string instanceRoot,
        string receiptsRoot,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(receiptsRoot))
        {
            return [];
        }
        var receipts = new List<(string Path, InstalledModReceipt Receipt, bool Migrated)>();
        foreach (var path in Directory.EnumerateFiles(receiptsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            var receipt = await AtomicJsonStore.ReadAsync<InstalledModReceipt>(path, cancellationToken);
            if (receipt.SchemaVersion > InstalledModReceipt.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Mod receipt '{receipt.Id}' uses unsupported schema version {receipt.SchemaVersion}.");
            }
            if (receipt.SchemaVersion < InstalledModReceipt.CurrentSchemaVersion)
            {
                receipt = Migrate(receipt);
                receipts.Add((path, receipt, true));
            }
            else
            {
                receipts.Add((path, receipt, false));
            }
        }
        var validated = ValidateAll(instanceRoot, receipts.Select(item => item.Receipt).ToArray());
        foreach (var item in receipts.Where(item => item.Migrated))
        {
            await AtomicJsonStore.WriteAsync(item.Path, item.Receipt, cancellationToken);
        }
        return validated.OrderBy(receipt => receipt.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static IReadOnlyList<InstalledModReceipt> ValidateAll(
        string instanceRoot,
        IReadOnlyList<InstalledModReceipt> receipts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceRoot);
        ArgumentNullException.ThrowIfNull(receipts);
        var pathPolicy = new ModPathPolicy(instanceRoot);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ownedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var receipt in receipts)
        {
            if (receipt is null)
            {
                throw new InvalidDataException("Mod receipt collection contains a null entry.");
            }
            Required(receipt.Id, "ID");
            Required(receipt.Name, $"Mod receipt '{receipt.Id}' name");
            Required(receipt.Version, $"Mod receipt '{receipt.Id}' version");
            Required(receipt.LoaderId, $"Mod receipt '{receipt.Id}' loader ID");
            Required(receipt.InstallRoot, $"Mod receipt '{receipt.Id}' install root");
            if (!ids.Add(receipt.Id))
            {
                throw new InvalidDataException($"Duplicate mod receipt ID '{receipt.Id}'.");
            }
            if (receipt.Ownership == ModOwnership.External
                || receipt.IsLocal != (receipt.Ownership == ModOwnership.LocalTakenOver))
            {
                throw new InvalidDataException($"Mod receipt '{receipt.Id}' has invalid ownership metadata.");
            }
            var installRoot = pathPolicy.ResolveRecognized(receipt.InstallRoot);
            pathPolicy.EnsureNoReparsePoints(installRoot.FullPath);
            if (receipt.Files is null || receipt.EntryFiles is null || receipt.Dependencies is null)
            {
                throw new InvalidDataException($"Mod receipt '{receipt.Id}' contains a null collection.");
            }

            var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in receipt.Files)
            {
                if (file is null)
                {
                    throw new InvalidDataException($"Mod receipt '{receipt.Id}' contains a null file entry.");
                }
                var resolved = pathPolicy.ResolveUnderOwnedRoot(file.RelativePath, installRoot);
                pathPolicy.EnsureNoReparsePoints(resolved.FullPath);
                if (!filePaths.Add(resolved.RelativePath))
                {
                    throw new InvalidDataException(
                        $"Mod receipt '{receipt.Id}' contains duplicate file '{resolved.RelativePath}'.");
                }
                ValidateSha256(file.Sha256, $"Mod receipt '{receipt.Id}' file '{resolved.RelativePath}'");
                if (ownedPaths.TryGetValue(resolved.RelativePath, out var owner))
                {
                    throw new InvalidDataException(
                        $"Mod receipt '{receipt.Id}' file '{resolved.RelativePath}' is already owned by '{owner}'.");
                }
                ownedPaths.Add(resolved.RelativePath, receipt.Id);
            }

            var entryFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entryFile in receipt.EntryFiles)
            {
                var resolved = pathPolicy.ResolveUnderOwnedRoot(entryFile, installRoot);
                if (!entryFiles.Add(resolved.RelativePath) || !filePaths.Contains(resolved.RelativePath))
                {
                    throw new InvalidDataException(
                        $"Mod receipt '{receipt.Id}' entry file '{resolved.RelativePath}' is invalid.");
                }
            }
            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in receipt.Dependencies)
            {
                Required(dependency, $"Mod receipt '{receipt.Id}' dependency");
                if (!dependencies.Add(dependency)
                    || string.Equals(dependency, receipt.Id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Mod receipt '{receipt.Id}' contains invalid dependency '{dependency}'.");
                }
            }
        }
        return receipts;
    }

    private static void ValidateSha256(string? value, string description)
    {
        try
        {
            if (value?.Length != 64 || Convert.FromHexString(value).Length != 32)
            {
                throw new FormatException();
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentNullException)
        {
            throw new InvalidDataException($"{description} SHA-256 is invalid.", exception);
        }
    }

    private static void Required(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{description} is required.");
        }
    }

    private static InstalledModReceipt Migrate(InstalledModReceipt receipt)
    {
        if (receipt.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Mod receipt '{receipt.Id}' uses unsupported schema version {receipt.SchemaVersion}.");
        }
        return receipt with
        {
            SchemaVersion = InstalledModReceipt.CurrentSchemaVersion,
            Ownership = receipt.IsLocal ? ModOwnership.LocalTakenOver : ModOwnership.Managed,
            EntryFiles = receipt.Files
                .Where(file => string.Equals(
                    Path.GetExtension(file.RelativePath), ".dll", StringComparison.OrdinalIgnoreCase))
                .Select(file => file.RelativePath)
                .ToArray()
        };
    }
}
