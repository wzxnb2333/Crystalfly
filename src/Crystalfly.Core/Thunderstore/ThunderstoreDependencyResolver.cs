using Crystalfly.Core.Models;

namespace Crystalfly.Core.Thunderstore;

public static class SilksongLoaderAdapter
{
    public const string PackageName = "silksong_modding-BepInExPack_Silksong";
    public const string PackageVersion = "1.0.3";
    public const string RuntimeVersion = "5.4.23.4";
    public const string Sha256 = "A343275C6953FF12233E059D6D406FBDB9FAF795FA695F12D5E7AD5C70FB6DA7";
    // These historical dependencies are explicitly superseded by the official Silksong pack.
    // No fuzzy namespace or package-name matching is allowed.
    private static readonly HashSet<string> Aliases = new(StringComparer.Ordinal)
    {
        "BepInEx-BepInExPack_Silksong-5.4.2303",
        "BepInEx-BepInExPack_Silksong-5.4.2304",
        "BepInEx-BepInExPack_Silksong-5.4.75304"
    };
    public static string CanonicalDependency(string dependency) => Aliases.Contains(dependency)
        ? PackageName + "-" + PackageVersion : dependency;
    public static bool IsLoader(string name) => name == PackageName;
}

public sealed record ThunderstoreCompatibilityRule(string PackageName, string PackageVersion,
    string BuildId, GameCompatibility Compatibility, string Reason);

public sealed class ThunderstoreDependencyResolver(ThunderstoreCatalog catalog,
    IReadOnlyList<ThunderstoreCompatibilityRule>? compatibilityRules = null)
{
    private sealed record Requirement(string Version, bool Exact, string Parent);

    public ThunderstoreInstallPlan Resolve(GameId gameId, string buildId,
        IReadOnlyList<(string Name, string? Version)> requests,
        IReadOnlyList<ThunderstoreInstalledPackage>? installed = null)
    {
        if (gameId != GameId.Silksong || catalog.GameId != gameId)
            throw new InvalidDataException("Thunderstore community does not match the target game.");
        installed ??= [];
        var packages = catalog.Packages.ToDictionary(item => item.FullName, StringComparer.OrdinalIgnoreCase);
        foreach (var local in installed.Where(item => item.IsLocal && item.LocalPackage is not null))
            packages[local.FullName] = local.LocalPackage!;
        var previous = installed.ToDictionary(item => item.FullName, StringComparer.OrdinalIgnoreCase);
        var roots = new Dictionary<string, List<Requirement>>(StringComparer.OrdinalIgnoreCase);
        void Add(Dictionary<string, List<Requirement>> map, string name, Requirement requirement)
        {
            if (!map.TryGetValue(name, out var list)) map[name] = list = [];
            if (!list.Contains(requirement)) list.Add(requirement);
        }
        foreach (var request in requests)
        {
            var name = ThunderstoreIdentity.SafeName(request.Name);
            if (!packages.TryGetValue(name, out var package)) throw new InvalidDataException($"Package is missing: {name}");
            var version = request.Version ?? package.Latest?.VersionNumber
                ?? throw new InvalidDataException($"No active version: {name}");
            var canonical = ThunderstoreIdentity.Dependency(SilksongLoaderAdapter.CanonicalDependency(name + "-" + version));
            Add(roots, canonical.Name, new(canonical.Version, true, "selected"));
        }
        // Every already installed package participates, so changing a dependency cannot break an unrelated Mod.
        foreach (var package in installed)
        {
            if (!roots.ContainsKey(package.FullName)) Add(roots, package.FullName, new(package.Version, package.Pinned, "installed"));
            if (package.Pinned) Add(roots, package.FullName, new(package.Version, true, "pinned"));
        }
        Add(roots, SilksongLoaderAdapter.PackageName, new(SilksongLoaderAdapter.PackageVersion, true, "loader"));
        var selected = new Dictionary<string, ThunderstoreResolvedPackage>(StringComparer.OrdinalIgnoreCase);
        for (var iteration = 0; iteration < 128; iteration++)
        {
            var requirements = roots.ToDictionary(item => item.Key, item => item.Value.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var item in selected.Values)
            {
                foreach (var raw in item.Version.Dependencies)
                {
                    var dependency = ThunderstoreIdentity.Dependency(SilksongLoaderAdapter.CanonicalDependency(raw));
                    Add(requirements, dependency.Name, new(dependency.Version, item.Package.IsModpack, item.Package.FullName));
                }
            }
            var next = new Dictionary<string, ThunderstoreResolvedPackage>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, constraints) in requirements)
            {
                if (!packages.TryGetValue(name, out var package)) throw new InvalidDataException($"Missing dependency: {name}");
                bool Accepts(ThunderstoreVersion candidate) => candidate.IsActive && constraints.All(requirement => requirement.Exact
                    ? candidate.VersionNumber == requirement.Version
                    : ThunderstoreIdentity.Version(candidate.VersionNumber) >= ThunderstoreIdentity.Version(requirement.Version));
                previous.TryGetValue(name, out var old);
                var chosen = old is not null ? package.Versions.FirstOrDefault(version => version.VersionNumber == old.Version && Accepts(version)) : null;
                chosen ??= package.Versions.Where(Accepts).OrderByDescending(version => ThunderstoreIdentity.Version(version.VersionNumber)).FirstOrDefault();
                if (chosen is null) throw new InvalidDataException($"Dependency conflict: {name}: "
                    + string.Join(", ", constraints.Select(item => $"{item.Parent} {(item.Exact ? "=" : ">=")} {item.Version}")));
                if (old?.Pinned == true && old.Version != chosen.VersionNumber)
                    throw new InvalidDataException($"Version is locked: {name} {old.Version}");
                next[name] = new(package, chosen, old?.Version, old?.Pinned ?? false) { IsLocal = old?.IsLocal == true || package.Owner == "Local" };
            }
            var stable = next.Count == selected.Count && next.All(item => selected.TryGetValue(item.Key, out var old)
                && item.Value.Version.VersionNumber == old.Version.VersionNumber);
            selected = next;
            if (!stable) continue;
            var warnings = new List<string>();
            foreach (var item in selected.Values)
            {
                var rule = compatibilityRules?.FirstOrDefault(rule => rule.PackageName == item.Package.FullName
                    && rule.PackageVersion == item.Version.VersionNumber && rule.BuildId == buildId);
                if (rule?.Compatibility == GameCompatibility.Incompatible)
                    throw new InvalidDataException($"Incompatible: {item.Package.FullName}: {rule.Reason}");
                if (rule?.Compatibility != GameCompatibility.Verified) warnings.Add(item.Package.FullName);
            }
            ValidateAcyclic(selected);
            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Enable(string name)
            {
                if (!enabled.Add(name)) return;
                foreach (var raw in selected[name].Version.Dependencies)
                    Enable(ThunderstoreIdentity.Dependency(SilksongLoaderAdapter.CanonicalDependency(raw)).Name);
            }
            foreach (var request in requests)
            {
                var name = request.Name;
                if (name == "BepInEx-BepInExPack_Silksong") name = SilksongLoaderAdapter.PackageName;
                Enable(name);
            }
            Enable(SilksongLoaderAdapter.PackageName);
            return new(gameId, buildId, selected.Values.OrderBy(item => item.Package.FullName, StringComparer.Ordinal).ToArray(), warnings)
            { RequiredEnabled = enabled.ToArray() };
        }
        throw new InvalidDataException("Dependency graph did not converge.");
    }

    private static void ValidateAcyclic(IReadOnlyDictionary<string, ThunderstoreResolvedPackage> packages)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string name)
        {
            if (visited.Contains(name)) return;
            if (!active.Add(name)) throw new InvalidDataException($"Dependency cycle: {name}");
            foreach (var raw in packages[name].Version.Dependencies)
                Visit(ThunderstoreIdentity.Dependency(SilksongLoaderAdapter.CanonicalDependency(raw)).Name);
            active.Remove(name);
            visited.Add(name);
        }
        foreach (var name in packages.Keys) Visit(name);
    }
}
