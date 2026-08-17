using Crystalfly.Core.Models;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class OfficialSpeedrunTemplatePolicyTests
{
    [Theory]
    [InlineData("1.2.2.1", RuntimePatchesFeature.ScreenShakeModifier | RuntimePatchesFeature.MiniSaveStates | RuntimePatchesFeature.TextMasher)]
    [InlineData("1.4.3.2", RuntimePatchesFeature.ScreenShakeModifier | RuntimePatchesFeature.MiniSaveStates | RuntimePatchesFeature.FasterIntroSkip | RuntimePatchesFeature.TextMasher)]
    [InlineData("1.5.78.11833", RuntimePatchesFeature.MiniSaveStates | RuntimePatchesFeature.FasterIntroSkip | RuntimePatchesFeature.TextMasher)]
    public void RuntimePatches_capabilities_match_the_release_binaries(
        string buildId,
        RuntimePatchesFeature expected)
    {
        Assert.Equal(expected, RuntimePatchesPolicy.GetSupportedFeatures(buildId));
    }

    [Fact]
    public void Unsupported_features_are_forced_off()
    {
        var enabled = new RuntimePatchesConfiguration
        {
            ScreenShakeModifier = true,
            MiniSaveStates = true,
            FasterIntroSkip = true,
            TextMasher = true
        };

        Assert.False(RuntimePatchesPolicy.Normalize("1.2.2.1", enabled).FasterIntroSkip);
        Assert.False(RuntimePatchesPolicy.Normalize("1.5.78.11833", enabled).ScreenShakeModifier);
    }

    [Theory]
    [InlineData("race-1221")]
    [InlineData("single-run-1221")]
    [InlineData("single-run-1578")]
    [InlineData("race-1578")]
    public void Old_template_ids_are_recognized_as_legacy(string templateId)
    {
        Assert.True(RuntimePatchesPolicy.IsLegacyTemplate(templateId));
    }

    [Theory]
    [InlineData("runtime-patches-1221", "1.2.2.1", "runtime-patches-1221-v1.0.2")]
    [InlineData("runtime-patches-1432", "1.4.3.2", "runtime-patches-1432-v1.0.2")]
    [InlineData("runtime-patches-1578", "1.5.78.11833", "runtime-patches-1578-v1.0.2")]
    [InlineData("runtime-patches-1578-multi", "1.5.78.11833", "multisavestates-1578")]
    public void Accepts_the_three_fixed_RuntimePatches_templates(
        string templateId,
        string buildId,
        string assetId)
    {
        var template = new SpeedrunTemplate
        {
            Id = templateId,
            Name = templateId,
            BuildId = buildId,
            IsOfficial = true,
            RulesRevision = "runtime-patches-v1.0.2",
            FileManifestId = $"files-{templateId}",
            RequiredAssetIds = [assetId]
        };

        Assert.Null(OfficialSpeedrunTemplatePolicy.GetViolation(template));
    }

    [Fact]
    public void Rejects_a_template_that_requests_a_loader()
    {
        var template = new SpeedrunTemplate
        {
            Id = "runtime-patches-1432",
            Name = "RuntimePatches 1.4.3.2",
            BuildId = "1.4.3.2",
            IsOfficial = true,
            RulesRevision = "runtime-patches-v1.0.2",
            FileManifestId = "files-runtime-patches-1432",
            LoaderId = "modding-api-37",
            RequiredAssetIds = ["runtime-patches-1432-v1.0.2"]
        };

        Assert.Equal(
            SpeedrunIssueCode.UnsupportedOfficialTemplate,
            OfficialSpeedrunTemplatePolicy.GetViolation(template));
    }
}
