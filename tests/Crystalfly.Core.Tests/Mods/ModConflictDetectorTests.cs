using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModConflictDetectorTests
{
    [Fact]
    public void Detect_returns_pairs_for_mods_sharing_files()
    {
        var result = ModConflictDetector.Detect(
        [
            new("a", "Mod A", ["hollow_knight_Data/Managed/Mods/A.dll"]),
            new("b", "Mod B", ["hollow_knight_Data/Managed/Mods/A.dll"]),
            new("c", "Mod C", ["hollow_knight_Data/Managed/Mods/C.dll"])
        ]);

        Assert.Single(result);
        Assert.Equal(("a", "b"), (result[0].ModA, result[0].ModB));
        Assert.Equal(["hollow_knight_Data/Managed/Mods/A.dll"], result[0].OverlappingFiles);
    }

    [Fact]
    public void Detect_returns_empty_when_no_overlap()
    {
        var result = ModConflictDetector.Detect(
        [
            new("a", "Mod A", ["hollow_knight_Data/Managed/Mods/A.dll"]),
            new("b", "Mod B", ["hollow_knight_Data/Managed/Mods/B.dll"]),
            new("c", "Mod C", ["hollow_knight_Data/Managed/Mods/C.dll"])
        ]);

        Assert.Empty(result);
    }

    [Fact]
    public void Detect_matches_file_paths_case_insensitively()
    {
        var result = ModConflictDetector.Detect(
        [
            new("a", "Mod A", ["hollow_knight_Data/Managed/A.dll"]),
            new("b", "Mod B", ["hollow_knight_Data/managed/a.dll"])
        ]);

        Assert.Single(result);
        Assert.Equal(("a", "b"), (result[0].ModA, result[0].ModB));
    }

    [Fact]
    public void Detect_reports_multi_file_overlap_once_per_pair()
    {
        var result = ModConflictDetector.Detect(
        [
            new("a", "Mod A", ["hollow_knight_Data/Managed/Shared/A.dll", "hollow_knight_Data/Managed/Shared/B.dll"]),
            new("b", "Mod B", ["hollow_knight_Data/Managed/Shared/A.dll", "hollow_knight_Data/Managed/Shared/B.dll"])
        ]);

        var pair = Assert.Single(result);
        Assert.Equal(["hollow_knight_Data/Managed/Shared/A.dll", "hollow_knight_Data/Managed/Shared/B.dll"], pair.OverlappingFiles);
    }

    [Fact]
    public void Detect_returns_empty_when_no_mods_are_provided() =>
        Assert.Empty(ModConflictDetector.Detect([]));
}
