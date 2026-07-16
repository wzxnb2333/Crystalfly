using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Serialization;

public sealed class ManifestSerializationTests
{
    [Fact]
    public void GameBuild_round_trips_with_schema_version()
    {
        var build = new GameBuild
        {
            Id = "1.2.2.1",
            DisplayVersion = "1.2.2.1",
            DepotId = 367521,
            ManifestId = "648876203478229944",
            ExecutableSha256 = "1434454FCB5A1F4FFF329EA56182A7C7DA1581DC0F4B6DCEF8585E739F416217",
            GlobalGameManagersSha256 = "58BC88B74D6F05B9E00D7E1F2BC9B3BA6E9FC51F75C6915DF10BF10B90CDD749"
        };

        var json = CrystalflyJson.Serialize(build);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<GameBuild>(json);

        Assert.Equal(GameBuild.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(build, restored);
    }

    [Fact]
    public void GameChannel_round_trips_with_schema_version()
    {
        var channel = new GameChannel
        {
            Name = "latest",
            BuildId = "steam-public-257781644874438846"
        };

        var json = CrystalflyJson.Serialize(channel);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<GameChannel>(json);

        Assert.Equal(GameChannel.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(channel, restored);
    }

    [Fact]
    public void InstanceRecord_round_trips_with_schema_version()
    {
        var instance = new InstanceRecord
        {
            Id = "practice-1221",
            Name = "Practice 1.2.2.1",
            RootPath = @"D:\Games\Hollow Knight 1.2.2.1",
            BuildId = "1.2.2.1",
            LoaderId = "bepinex-5.4.23.4",
            CreatedAt = DateTimeOffset.Parse("2026-07-16T12:00:00Z")
        };

        var json = CrystalflyJson.Serialize(instance);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<InstanceRecord>(json);

        Assert.Equal(InstanceRecord.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(instance, restored);
    }

    [Fact]
    public void LoaderManifest_round_trips_with_schema_version()
    {
        var manifest = new LoaderManifest
        {
            Id = "bepinex-5.4.23.4",
            Name = "BepInEx",
            Version = "5.4.23.4",
            DownloadUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.4/BepInEx_win_x64_5.4.23.4.zip",
            SizeBytes = 638940,
            Sha256 = "F881201B79DA03E513BF97CDF39607FFA7F9E0D31A519B1AEECA8EB60F8309E7",
            SupportedBuildIds = ["1.2.2.1", "1.4.3.2", "1.5.78.11833"]
        };

        var json = CrystalflyJson.Serialize(manifest);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<LoaderManifest>(json);

        Assert.Equal(LoaderManifest.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(manifest.Id, restored.Id);
        Assert.Equal(manifest.SupportedBuildIds, restored.SupportedBuildIds);
    }

    [Fact]
    public void ModManifest_round_trips_with_schema_version()
    {
        var manifest = new ModManifest
        {
            Id = "benchwarp",
            Name = "Benchwarp",
            Version = "3.2.0",
            DownloadUrl = "https://example.invalid/benchwarp.zip",
            Sha256 = new string('A', 64),
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"],
            Dependencies = ["satchel"]
        };

        var json = CrystalflyJson.Serialize(manifest);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<ModManifest>(json);

        Assert.Equal(ModManifest.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(manifest.Id, restored.Id);
        Assert.Equal(manifest.Dependencies, restored.Dependencies);
    }

    [Fact]
    public void SpeedrunTemplate_round_trips_with_schema_version()
    {
        var template = new SpeedrunTemplate
        {
            Id = "race-1432",
            Name = "Race 1.4.3.2",
            BuildId = "1.4.3.2",
            RequiredAssetIds = ["load-normaliser-1.1"],
            RequiresLoadNormaliserSelection = true,
            AllowedLoadNormaliserSeconds = [1, 2, 3, 5]
        };

        var json = CrystalflyJson.Serialize(template);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<SpeedrunTemplate>(json);

        Assert.Equal(SpeedrunTemplate.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(template.Id, restored.Id);
        Assert.Equal(template.AllowedLoadNormaliserSeconds, restored.AllowedLoadNormaliserSeconds);
    }

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
    }

    [Fact]
    public void NamedSnapshot_round_trips_with_schema_version()
    {
        var snapshot = new NamedSnapshot
        {
            Id = "steel-soul-before-watcher-knights",
            Name = "Before Watcher Knights",
            SourcePath = @"D:\Saves\user1.dat",
            SnapshotPath = @"D:\Snapshots\steel-soul-before-watcher-knights\user1.dat",
            Sha256 = new string('B', 64),
            CreatedAt = DateTimeOffset.Parse("2026-07-16T12:00:00Z")
        };

        var json = CrystalflyJson.Serialize(snapshot);
        using var document = JsonDocument.Parse(json);
        var restored = CrystalflyJson.Deserialize<NamedSnapshot>(json);

        Assert.Equal(NamedSnapshot.CurrentSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(snapshot, restored);
    }
}
