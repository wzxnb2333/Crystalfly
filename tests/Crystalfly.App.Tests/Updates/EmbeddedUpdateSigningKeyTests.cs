using Crystalfly.App.Updates;

namespace Crystalfly.App.Tests.Updates;

public sealed class EmbeddedUpdateSigningKeyTests
{
    [Fact]
    public void Load_returns_the_embedded_ed25519_public_key()
    {
        byte[] key = EmbeddedUpdateSigningKey.Load();

        Assert.Equal(32, key.Length);
        Assert.Contains(key, value => value != 0);
    }
}
