using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModDependencyResolverTests
{

    [Fact]
    public void ResolveInstallOrder_rejects_missing_or_cyclic_dependencies()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            ModDependencyResolver.ResolveInstallOrder([Mod("hkmod:A", "hkmod:Missing")], ["hkmod:A"]));
        Assert.Throws<InvalidDataException>(() =>
            ModDependencyResolver.ResolveInstallOrder(
                [Mod("hkmod:A", "hkmod:B"), Mod("hkmod:B", "hkmod:A")],
                ["hkmod:A"]));
    }

    [Fact]
    public void FindDependents_returns_transitive_reverse_dependencies()
    {
        var mods = new[]
        {
            Mod("hkmod:Base"),
            Mod("hkmod:Middle", "hkmod:Base"),
            Mod("hkmod:Top", "hkmod:Middle")
        };

        var result = ModDependencyResolver.FindDependents(mods, "hkmod:Base");

        Assert.Equal(["hkmod:Middle", "hkmod:Top"], result.Select(mod => mod.Id));
    }

    private static ModManifest Mod(string id, params string[] dependencies) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api-77",
        SupportedBuildIds = ["1.5.78.11833"],
        Dependencies = dependencies
    };
}
