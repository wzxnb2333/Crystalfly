using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class BuildIdentityTests
{
    [Theory]
    [InlineData("unknown")]
    [InlineData("steam-public-42")]
    [InlineData("steam-manifest-42")]
    public void Unverified_build_ids_are_not_known(string buildId) =>
        Assert.False(BuildIdentity.IsKnown(buildId));

    [Theory]
    [InlineData("steam-manifest-42", 42UL)]
    [InlineData("steam-public-42", 42UL)]
    public void Steam_unverified_build_ids_expose_their_manifest(string buildId, ulong manifestId)
    {
        Assert.True(BuildIdentity.TryGetSteamManifestId(buildId, out var parsed));
        Assert.Equal(manifestId, parsed);
    }
}