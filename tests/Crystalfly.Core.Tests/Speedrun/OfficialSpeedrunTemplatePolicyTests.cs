using Crystalfly.Core.Models;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class OfficialSpeedrunTemplatePolicyTests
{
    public static TheoryData<SpeedrunTemplate> OfficialTemplates => new()
    {
        Create("single-run-1221", "1.2.2.1", ["screen-shake-modifier-1221"]),
        Create("race-1221", "1.2.2.1", ["screen-shake-modifier-1221"]),
        Create("single-run-1578", "1.5.78.11833", []),
        Create(
            "race-1578",
            "1.5.78.11833",
            ["load-normaliser-1.1"],
            requiresSelection: true,
            allowedSeconds: [1, 2, 3, 5])
    };

    [Theory]
    [MemberData(nameof(OfficialTemplates))]
    public void Accepts_the_four_supported_official_templates(SpeedrunTemplate template)
    {
        Assert.Null(OfficialSpeedrunTemplatePolicy.GetViolation(template));
    }

    [Fact]
    public void Rejects_an_official_template_with_modified_constraints()
    {
        SpeedrunTemplate template = Create("race-1578", "1.5.78.11833", []);

        Assert.Equal(SpeedrunIssueCode.UnsupportedOfficialTemplate, OfficialSpeedrunTemplatePolicy.GetViolation(template));
    }

    [Fact]
    public void Rejects_an_official_template_that_requests_a_loader()
    {
        SpeedrunTemplate template = Create(
            "single-run-1221",
            "1.2.2.1",
            ["screen-shake-modifier-1221"]) with
        {
            LoaderId = "modding-api-37"
        };

        Assert.Equal(SpeedrunIssueCode.UnsupportedOfficialTemplate, OfficialSpeedrunTemplatePolicy.GetViolation(template));
    }

    [Fact]
    public void Rejects_an_official_template_without_a_file_manifest()
    {
        SpeedrunTemplate template = Create(
            "single-run-1221",
            "1.2.2.1",
            ["screen-shake-modifier-1221"]) with
        {
            FileManifestId = ""
        };

        Assert.Equal(SpeedrunIssueCode.UnsupportedOfficialTemplate, OfficialSpeedrunTemplatePolicy.GetViolation(template));
    }

    private static SpeedrunTemplate Create(
        string id,
        string buildId,
        IReadOnlyList<string> assets,
        bool requiresSelection = false,
        IReadOnlyList<int>? allowedSeconds = null) =>
        new()
        {
            Id = id,
            Name = id,
            BuildId = buildId,
            IsOfficial = true,
            RulesRevision = "rules-abc123",
            FileManifestId = $"files-{id}",
            RequiredAssetIds = assets,
            LoadNormaliserAvailable = id.EndsWith("1578", StringComparison.Ordinal),
            RequiresLoadNormaliserSelection = requiresSelection,
            AllowedLoadNormaliserSeconds = allowedSeconds ?? []
        };
}
