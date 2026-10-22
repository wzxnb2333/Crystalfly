using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;
using System.Text.Json.Nodes;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class SaveEditorViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.SaveEditor.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("modData.health")]
    [InlineData("playerData.gotCharm_41")]
    [InlineData("[\"playerData.geo\"]")]
    public void Unknown_or_mod_owned_fields_keep_the_original_name(string path)
    {
        var entry = new SaveEntryViewModel(new SaveEntry(path, "true", SaveEntry.KindString));
        Assert.Equal(path, entry.DisplayName);
        Assert.False(entry.HasLocalizedName);
        Assert.Contains("尚无已核实", entry.Description);
        Assert.False(entry.IsBoolean);
        Assert.Equal("true", entry.Value);
    }

    [Fact]
    public async Task Silksong_editor_lists_account_slots_and_imports_into_selected_profile()
    {
        const string json = """{"playerData":{"version":"1.0.28324","silk":3,"geo":50}}""";
        var directory = Path.Combine(root, "instances/instance/local-low/123456789");
        Directory.CreateDirectory(directory);
        await SaveFileCodec.EncryptAsync(Path.Combine(directory, "user2.dat"), json);
        var service = new NamedSnapshotService(root, "Crystalfly.ProfileEditor." + Guid.NewGuid().ToString("N"),
            new IdleProcessProbe(), Crystalfly.Core.Models.GameId.Silksong);
        var editor = new SaveEditorViewModel(service, "instance", null, "Profiles",
            gameId: Crystalfly.Core.Models.GameId.Silksong, targetVersion: "silksong-1.0.30000");
        await editor.InitializeAsync();
        Assert.Equal("123456789/user2.dat", editor.SelectedSlot);
        Assert.Equal(editor.SelectedSlot, editor.ImportSlot);
        Assert.Contains("123456789/user4.dat", editor.ImportSlots);
        Assert.Contains("default/user1.dat", editor.ImportSlots);
        Assert.DoesNotContain("user1.dat", editor.ImportSlots);
        var source = Path.Combine(root, "input.dat");
        await SaveFileCodec.EncryptAsync(source, json.Replace("50", "70", StringComparison.Ordinal));
        await editor.ImportFileAsync(source, "default/user1.dat");
        Assert.Equal("default/user1.dat", editor.SelectedSlot);
        Assert.Equal("70", editor.Entries.Single(item => item.Path == "playerData.geo").Value);
        Assert.Equal(json, await service.DecryptSaveAsync("instance", null, "123456789/user2.dat"));
    }

    [Fact]
    public async Task Language_search_and_boolean_controls_preserve_save_data_and_hidden_edits()
    {
        var saves = Path.Combine(root, "instances", "instance", "local-low");
        Directory.CreateDirectory(saves);
        const string original = """
            {"playerData":{"geo":1250,"hasDash":true,"health":5,"respawnScene":"Town","equippedCharms":[1,2]},"modData":{"health":15,"raw":"true","unknown":null,"custom.key":"保留"}}
            """;
        var savePath = Path.Combine(saves, "user1.dat");
        await SaveFileCodec.EncryptAsync(savePath, original);
        var loc = new LocalizationViewModel();
        var service = new NamedSnapshotService(root, $"Crystalfly.SaveLocalization.{Guid.NewGuid():N}", new IdleProcessProbe());
        var editor = new SaveEditorViewModel(service, "instance", null, "Test saves", loc);
        await editor.InitializeAsync();
        var originalEntries = editor.Entries.Select(entry => entry.ToEntry()).ToArray();
        Assert.False(editor.IsDirty);

        loc.Apply(UiLanguage.English);
        editor.RefreshLocalization(loc);
        Assert.False(editor.IsDirty);
        Assert.All(editor.Entries, entry => Assert.Equal(entry.Path, entry.DisplayName));
        Assert.Equal(originalEntries, editor.Entries.Select(entry => entry.ToEntry()).ToArray());
        loc.Apply(UiLanguage.SimplifiedChinese);
        editor.RefreshLocalization(loc);
        Assert.False(editor.IsDirty);

        editor.SearchText = " 游戏货币 ";
        var geo = Assert.Single(editor.VisibleEntries);
        Assert.Equal("playerData.geo", geo.Path);
        geo.Value = "777";
        editor.SearchText = "hasDash";
        var dash = Assert.Single(editor.VisibleEntries);
        Assert.Equal("是", dash.BooleanDisplayValue);
        dash.BooleanValue = false;
        Assert.Equal("否", dash.BooleanDisplayValue);
        Assert.Equal("false", dash.Value);
        Assert.True(editor.IsDirty);
        editor.SearchText = "does-not-exist";
        Assert.True(editor.HasNoSearchResults);
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = JsonNode.Parse(await SaveFileCodec.DecryptAsync(savePath))!;
        var expected = JsonNode.Parse(original)!;
        expected["playerData"]!["geo"] = 777;
        expected["playerData"]!["hasDash"] = false;
        Assert.True(JsonNode.DeepEquals(expected, saved));
        Assert.False(editor.IsDirty);
        editor.SearchText = string.Empty;
        Assert.Equal(editor.Entries.Count, editor.VisibleEntries.Count);
        Assert.False(editor.HasNoSearchResults);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_slot_switch_preserves_loaded_slot_and_does_not_overwrite_other_save(bool canceled)
    {
        string saves = Path.Combine(root, "instances", "instance", "local-low");
        Directory.CreateDirectory(saves);
        await SaveFileCodec.EncryptAsync(Path.Combine(saves, "user1.dat"), "{\"health\":5}");
        byte[] secondSave = canceled ? SaveFileCodec.Encrypt("{\"health\":3}") : [1, 2, 3];
        await File.WriteAllBytesAsync(Path.Combine(saves, "user2.dat"), secondSave);
        var service = new NamedSnapshotService(root, $"Crystalfly.SaveEditor.{Guid.NewGuid():N}", new IdleProcessProbe());
        var editor = new SaveEditorViewModel(service, "instance", null, "Test saves");
        await editor.InitializeAsync();
        Assert.Single(editor.Entries).Value = "9";
        editor.SelectedSlot = "user2.dat";

        if (canceled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                editor.SelectSlotAsync("user2.dat", new CancellationToken(canceled: true)));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => editor.SelectSlotAsync("user2.dat"));
        }

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(secondSave, await File.ReadAllBytesAsync(Path.Combine(saves, "user2.dat")));
        Assert.Equal("{\"health\":9}", await SaveFileCodec.DecryptAsync(Path.Combine(saves, "user1.dat")));
        Assert.Equal("user1.dat", editor.SelectedSlot);
        Assert.True(editor.IsLoaded);
        Assert.False(editor.IsDirty);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class IdleProcessProbe : IHollowKnightProcessProbe
    {
        public bool IsRunning() => false;
    }
}
