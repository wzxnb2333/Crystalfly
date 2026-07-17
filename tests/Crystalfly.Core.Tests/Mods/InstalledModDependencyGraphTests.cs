using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class InstalledModDependencyGraphTests
{
    [Fact]
    public void Finds_external_transitive_dependents()
    {
        var mods = new[]
        {
            Receipt("library"),
            Receipt("feature", ["library"]),
            Receipt("addon", ["feature"]),
            Receipt("other")
        };

        var dependents = InstalledModDependencyGraph.FindExternalDependents(mods, ["library"]);

        Assert.Equal(["addon", "feature"], dependents.Select(mod => mod.Id).Order());
    }

    [Fact]
    public void Selected_dependents_are_not_reported_as_external()
    {
        var mods = new[]
        {
            Receipt("library"),
            Receipt("feature", ["library"]),
            Receipt("addon", ["feature"])
        };

        var dependents = InstalledModDependencyGraph.FindExternalDependents(
            mods,
            ["library", "feature", "addon"]);

        Assert.Empty(dependents);
    }

    [Fact]
    public void Orders_dependents_before_dependencies()
    {
        var mods = new[]
        {
            Receipt("library"),
            Receipt("feature", ["library"]),
            Receipt("addon", ["feature"])
        };

        var ordered = InstalledModDependencyGraph.OrderDependentsFirst(
            mods,
            ["library", "feature", "addon"]);

        Assert.Equal(["addon", "feature", "library"], ordered.Select(mod => mod.Id));
    }

    private static InstalledModReceipt Receipt(string id, IReadOnlyList<string>? dependencies = null) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0.0",
        LoaderId = "modding-api",
        InstallRoot = $"Mods/{id}",
        Dependencies = dependencies ?? []
    };
}
