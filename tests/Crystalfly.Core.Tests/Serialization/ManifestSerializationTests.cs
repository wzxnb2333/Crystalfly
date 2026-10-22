using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Serialization;

public sealed class ManifestSerializationTests
{

    [Fact]
    public void TransactionJournal_round_trips_with_schema_version()
    {
        var journal = new TransactionJournal
        {
            Id = "install-practice-1221",
            Operation = "install-instance",
            State = TransactionState.Prepared,
            CreatedAt = DateTimeOffset.Parse("2026-07-16T12:00:00Z"),
            CreatedPaths = [@"D:\Games\Hollow Knight 1.2.2.1"],
            RemovedDirectories = [@"D:\Games\Hollow Knight 1.2.2.1\slot"],
            BackupPaths = new Dictionary<string, string>
            {
                [@"D:\Games\Hollow Knight 1.2.2.1\hollow_knight.exe"] = @"D:\Backups\hollow_knight.exe"
            }
        };

        var json = CrystalflyJson.Serialize(journal);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<TransactionJournal>(json);

        Assert.Equal(TransactionJournal.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("prepared", document.RootElement.GetProperty("state").GetString());
        Assert.Equal(journal.BackupPaths, restored.BackupPaths);
        Assert.Equal(journal.RemovedDirectories, restored.RemovedDirectories);
    }

    [Fact]
    public void TransactionJournal_without_removed_directories_uses_empty_collection()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "legacy-transaction",
              "operation": "install-package",
              "state": "applying",
              "createdAt": "2026-07-16T12:00:00Z",
              "rootPath": "D:\\Games\\Hollow Knight",
              "restorePointPath": "D:\\Backups\\legacy-transaction"
            }
            """;

        var restored = CrystalflyJson.Deserialize<TransactionJournal>(json);

        Assert.Empty(restored.RemovedDirectories);
    }
}
