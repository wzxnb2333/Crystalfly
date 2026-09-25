using Crystalfly.App.ViewModels;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class SaveEditorViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.SaveEditor.Tests", Guid.NewGuid().ToString("N"));

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
