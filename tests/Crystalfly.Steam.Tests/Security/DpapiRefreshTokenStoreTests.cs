using System.Text;
using Crystalfly.Steam.Security;

namespace Crystalfly.Steam.Tests.Security;

public sealed class DpapiRefreshTokenStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"crystalfly-steam-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndLoadRoundTripUsesEncryptedFile()
    {
        string path = Path.Combine(_directory, "steam-token.bin");
        var store = new DpapiRefreshTokenStore(path);
        var expected = new RefreshTokenCredential("runner", "secret-refresh-token");

        await store.SaveAsync(expected);

        Assert.Equal(expected, await store.LoadAsync());
        byte[] persisted = await File.ReadAllBytesAsync(path);
        Assert.DoesNotContain("secret-refresh-token", Encoding.UTF8.GetString(persisted));
    }

    [Fact]
    public async Task DeleteRemovesStoredCredential()
    {
        string path = Path.Combine(_directory, "steam-token.bin");
        var store = new DpapiRefreshTokenStore(path);
        await store.SaveAsync(new RefreshTokenCredential("runner", "token"));
        await File.WriteAllTextAsync(path + ".tmp", "incomplete");

        store.Delete();

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Null(await store.LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
