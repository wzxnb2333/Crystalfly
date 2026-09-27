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
    [InlineData("playerData.geo", "吉欧")]
    [InlineData("playerData.health", "当前生命值")]
    [InlineData("playerData.hasDash", "蛾翼披风（冲刺）")]
    [InlineData("playerData.fireballLevel", "复仇之魂／暗影之魂等级")]
    [InlineData("playerData.gotCharm_1", "蜂群集结 · 已获得")]
    [InlineData("playerData.equippedCharm_2", "任性的指南针 · 已装备")]
    [InlineData("playerData.charmCost_40", "格林之子／无忧旋律 · 槽位消耗")]
    [InlineData("playerData.equippedCharms[0]", "已装备护符 · 第 1 项")]
    [InlineData("health", "当前生命值")]
    public void Known_fields_have_chinese_labels_without_changing_their_paths(string path, string expected)
    {
        var entry = new SaveEntryViewModel(new SaveEntry(path, "3", SaveEntry.KindNumber));
        Assert.Equal(expected, entry.DisplayName);
        Assert.True(entry.HasLocalizedName);
        Assert.Contains(path, entry.FieldToolTip);
        Assert.Equal(new SaveEntry(path, "3", SaveEntry.KindNumber), entry.ToEntry());
    }

    [Theory]
    [InlineData("modData.health")]
    [InlineData("playerData.customMod.geo")]
    [InlineData("playerData.unknownFutureField")]
    [InlineData("playerData.gotCharm_41")]
    [InlineData("playerData.equippedCharms[]")]
    [InlineData("playerData.equippedCharms[-1]")]
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

    [Theory]
    [InlineData(SaveEntry.KindNumber, "数值", "Number")]
    [InlineData(SaveEntry.KindString, "文本", "Text")]
    [InlineData(SaveEntry.KindBoolean, "开关", "Boolean")]
    [InlineData(SaveEntry.KindNull, "空值", "Null")]
    public void Type_labels_follow_language_without_changing_json_types(string kind, string chinese, string english)
    {
        var entry = new SaveEntryViewModel(new SaveEntry("playerData.geo", "true", kind));
        Assert.Equal(chinese, entry.KindDisplayName);
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.English);
        entry.RefreshLocalization(loc);
        Assert.Equal(english, entry.KindDisplayName);
        Assert.Equal("playerData.geo", entry.DisplayName);
        Assert.Equal(kind, entry.ToEntry().Kind);
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
