using MechWright.Core;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class StandardFieldRuleTests
{
    [Theory]
    [InlineData("baseHealthScale", "1.35")]
    [InlineData("ArmorRating_Sharp", "0.55")]
    [InlineData("BandwidthCost", "3")]
    [InlineData("mechWeightClass", "Medium")]
    [InlineData("allowInMechClusters", "true")]
    [InlineData("coverage", "0.17")]
    public void ValidValuesAreAccepted(string field, string value) =>
        Assert.Null(StandardFieldRules.Validate(field, value));

    [Theory]
    [InlineData("baseHealthScale", "-1")]
    [InlineData("MoveSpeed", "NaN")]
    [InlineData("BandwidthCost", "1.4")]
    [InlineData("burstShotCount", "0")]
    [InlineData("mechWeightClass", "Giant")]
    [InlineData("coverage", "1.5")]
    [InlineData("allowInMechClusters", "maybe")]
    public void InvalidStandardValuesAreRejected(string field, string value) =>
        Assert.NotNull(StandardFieldRules.Validate(field, value));

    [Fact]
    public void CustomModFieldsRemainRoundTripSafe()
    {
        const string custom = "<Defs><ThingDef><defName>MP_X</defName><CustomThing Class=\"Mod.Custom\">NaN!</CustomThing></ThingDef></Defs>";
        var document = DefDocument.Parse(custom);
        Assert.Null(StandardFieldRules.Validate("CustomThing", "NaN!"));
        Assert.DoesNotContain(document.Validate(), x => x.Severity == Severity.Error);
        Assert.Equal(custom, document.Export());
    }

    [Fact]
    public void InvalidKnownFieldBlocksExportThroughDiagnostics()
    {
        const string xml = "<Defs><ThingDef><defName>MP_X</defName><statBases><MoveSpeed>wrong</MoveSpeed></statBases></ThingDef></Defs>";
        var document = DefDocument.Parse(xml);
        Assert.Contains(document.Validate(), x => x.Code == "MW010" && x.Severity == Severity.Error);
    }
}
