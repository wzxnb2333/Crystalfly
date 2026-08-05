namespace Crystalfly.Core.Mods;

public sealed record ModConflictInput(
    string ModId,
    string ModName,
    IReadOnlyList<string> ModifiedFiles);

public sealed record ModConflictPair(
    string ModA,
    string ModB,
    IReadOnlyList<string> OverlappingFiles);

public static class ModConflictDetector
{
    public static IReadOnlyList<ModConflictPair> Detect(IReadOnlyList<ModConflictInput> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);
        var pairs = new List<ModConflictPair>();
        for (var i = 0; i < mods.Count - 1; i++)
        {
            for (var j = i + 1; j < mods.Count; j++)
            {
                var overlap = mods[i].ModifiedFiles
                    .Intersect(mods[j].ModifiedFiles, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (overlap.Length > 0)
                {
                    pairs.Add(new(mods[i].ModId, mods[j].ModId, overlap));
                }
            }
        }
        return pairs;
    }
}
