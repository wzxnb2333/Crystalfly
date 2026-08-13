using Crystalfly.Steam.Security;

namespace Crystalfly.Steam.Tests.Security;

public sealed class DpapiCredentialStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoadRoundTripsTheCredential()
    {
        string path = Path.Combine(directory, "steam-credentials.dat");
        var store = new DpapiCredentialStore(path);

        await store.SaveAsync(new SteamUsernameCredential("runner", "hunter2"));
        SteamUsernameCredential? loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("runner", loaded.Username);
        Assert.Equal("hunter2", loaded.Password);
    }

    [Fact]
    public async Task LoadReturnsNullWhenNoFileExists()
    {
        var store = new DpapiCredentialStore(Path.Combine(directory, "missing.dat"));

        SteamUsernameCredential? loaded = await store.LoadAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteRemovesTheStoredCredential()
    {
        string path = Path.Combine(directory, "steam-credentials.dat");
        var store = new DpapiCredentialStore(path);
        await store.SaveAsync(new SteamUsernameCredential("runner", "hunter2"));

        store.Delete();

        Assert.False(File.Exists(path));
        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task SaveRejectsEmptyPassword()
    {
        var store = new DpapiCredentialStore(Path.Combine(directory, "steam-credentials.dat"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(new SteamUsernameCredential("runner", string.Empty)));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
