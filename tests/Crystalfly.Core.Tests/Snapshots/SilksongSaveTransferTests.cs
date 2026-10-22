using System.Text.Json.Nodes;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.Core.Tests.Snapshots;

public sealed class SilksongSaveTransferTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Silksong.Saves", Guid.NewGuid().ToString("N"));
    private const string Json = """{"playerData":{"version":"1.0.30000","geo":10,"silk":3,"CurrentCrestID":"Hunter"},"future":{"nested":[null,true,{"raw.key":"未知"}]}}""";
    private NamedSnapshotService Service(bool running = false, GameId game = GameId.Silksong) =>
        new(root, "Crystalfly.SaveTest." + Guid.NewGuid().ToString("N"), new Probe(running), game);

    [Fact]
    public async Task Steam_and_default_profiles_remain_separate_through_edit_snapshot_import_and_restore()
    {
        var local = Path.Combine(root, "instances/test/local-low");
        var profiles = new[] { "123456789", "default" };
        foreach (var profile in profiles)
        {
            Directory.CreateDirectory(Path.Combine(local, profile));
            for (var slot = 1; slot <= 4; slot++)
                await SaveFileCodec.EncryptAsync(Path.Combine(local, profile, $"user{slot}.dat"), Json);
        }
        await File.WriteAllTextAsync(Path.Combine(local, "default", "settings.json"), "keep");
        var service = Service();
        var slots = await service.ListSaveSlotsAsync("test", null);
        Assert.True(SaveSlotPath.HasSteamProfileSaves(local));
        Assert.False(SaveSlotPath.HasSteamProfileSaves(Path.Combine(local, "default")));
        Assert.Equal(8, slots.Count);
        Assert.Contains("123456789/user1.dat", slots);
        Assert.Contains("default/user4.dat", slots);
        var snapshot = await service.CreateAsync("test", "profiles");
        var edited = Json.Replace("\"geo\":10", "\"geo\":777", StringComparison.Ordinal);
        await service.UpdateSaveAsync("test", null, "default/user1.dat", edited);
        Assert.Equal(edited, await service.DecryptSaveAsync("test", null, "default/user1.dat"));
        Assert.Equal(Json, await service.DecryptSaveAsync("test", null, "123456789/user1.dat"));
        await service.UpdateSaveAsync("test", snapshot.Id, "123456789/user2.dat", edited);
        var output = Path.Combine(root, "export.dat");
        await service.ExportSaveAsync("test", null, "default/user1.dat", output);
        await service.ImportSaveAsync("test", "123456789/user3.dat", output, "silksong-1.0.30000");
        Assert.Equal(edited, await service.DecryptSaveAsync("test", null, "123456789/user3.dat"));
        await service.RestoreAsync("test", snapshot.Id);
        Assert.Equal(Json, await service.DecryptSaveAsync("test", null, "default/user1.dat"));
        Assert.Equal(edited, await service.DecryptSaveAsync("test", null, "123456789/user2.dat"));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(local, "default", "settings.json")));
    }

    [Theory]
    [InlineData("../user1.dat")]
    [InlineData("default/../user1.dat")]
    [InlineData("arbitrary/user1.dat")]
    [InlineData("123/nested/user1.dat")]
    [InlineData("default/user5.dat")]
    public async Task Profile_slot_paths_reject_traversal_or_unrecognized_profiles(string slot)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().DecryptSaveAsync("test", null, slot));
    }

    [Fact]
    public async Task Four_slots_round_trip_edit_export_and_restore_unknown_fields()
    {
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "input.dat");
        await SaveFileCodec.EncryptAsync(input, Json);
        var service = Service();
        for (var slot = 1; slot <= 4; slot++)
        {
            await service.ImportSaveAsync("test", $"user{slot}.dat", input, "silksong-1.0.30000");
            Assert.True(await service.CanEditSaveAsync("test", null, $"user{slot}.dat"));
        }
        Assert.Equal(4, (await service.ListSaveSlotsAsync("test", null)).Count);
        var snapshot = await service.CreateAsync("test", "四槽快照");
        Assert.Equal(GameId.Silksong, snapshot.GameId);
        var entries = SaveGameEditor.Flatten(Json).Select(entry => entry.Path == "playerData.geo" ? entry with { Value = "777" } : entry).ToArray();
        await service.UpdateSaveAsync("test", null, "user1.dat", SaveGameEditor.Rebuild(Json, entries));
        var edited = JsonNode.Parse(await service.DecryptSaveAsync("test", null, "user1.dat"))!;
        Assert.Equal(777, edited["playerData"]!["geo"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Json)!["future"], edited["future"]));
        Assert.Single(Directory.GetFiles(Path.Combine(root, "instances/test/save-edit-backups")));
        var output = Path.Combine(root, "export.dat");
        await service.ExportSaveAsync("test", null, "user1.dat", output);
        Assert.Equal(edited.ToJsonString(), JsonNode.Parse(await SaveFileCodec.DecryptAsync(output))!.ToJsonString());
        await service.RestoreAsync("test", snapshot.Id);
        Assert.Equal(Json, await service.DecryptSaveAsync("test", null, "user1.dat"));
    }

    [Fact]
    public async Task Import_requires_version_acknowledgement_and_rejects_other_games()
    {
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "input.dat");
        await SaveFileCodec.EncryptAsync(input, Json);
        var service = Service();
        Assert.True(await service.ImportNeedsVersionWarningAsync(input, "1.0.29980"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportSaveAsync("test", "user1.dat", input, "1.0.29980"));
        await service.ImportSaveAsync("test", "user1.dat", input, "1.0.29980", acceptVersionRisk: true);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(game: GameId.HollowKnight).ImportSaveAsync("test", "user1.dat", input, "1.5.78"));
        await SaveFileCodec.EncryptAsync(input, """{"playerData":{"MPCharge":1,"version":"1.5.78"}}""");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportSaveAsync("test", "user1.dat", input, "1.0.30000"));
        Assert.Equal(Json, await service.DecryptSaveAsync("test", null, "user1.dat"));
    }

    [Fact]
    public async Task Running_game_cancelled_and_corrupt_import_cannot_replace_existing_save()
    {
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "input.dat");
        await SaveFileCodec.EncryptAsync(input, Json);
        var service = Service();
        await service.ImportSaveAsync("test", "user1.dat", input, "1.0.30000");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(true).ImportSaveAsync("test", "user1.dat", input, "1.0.30000"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ImportSaveAsync("test", "user1.dat", input, "1.0.30000", token: cancellation.Token));
        await File.WriteAllTextAsync(input, "corrupt");
        await Assert.ThrowsAnyAsync<Exception>(() => service.ImportSaveAsync("test", "user1.dat", input, "1.0.30000"));
        Assert.Equal(Json, await service.DecryptSaveAsync("test", null, "user1.dat"));
    }

    private sealed class Probe(bool running) : IHollowKnightProcessProbe { public bool IsRunning() => running; }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
