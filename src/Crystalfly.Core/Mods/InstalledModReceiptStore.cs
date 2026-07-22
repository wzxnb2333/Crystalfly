using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Mods;

internal static class InstalledModReceiptStore
{
    public static async Task<IReadOnlyList<InstalledModReceipt>> ReadAllAsync(
        string receiptsRoot,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(receiptsRoot))
        {
            return [];
        }
        var receipts = new List<InstalledModReceipt>();
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
                await AtomicJsonStore.WriteAsync(path, receipt, cancellationToken);
            }
            receipts.Add(receipt);
        }
        return receipts.OrderBy(receipt => receipt.Name, StringComparer.OrdinalIgnoreCase).ToArray();
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
