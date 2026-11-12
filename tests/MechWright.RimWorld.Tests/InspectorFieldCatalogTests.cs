using System.Xml.Linq;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class InspectorFieldCatalogTests
{
    [Fact]
    public void OfferOnlyFieldsBelongingToTheSelectedDefinitionType()
    {
        var thing = InspectorFieldCatalog.For("ThingDef", NativeFieldCatalog.Empty);
        Assert.Contains(thing, x => x.Path == "statBases/MoveSpeed" && x.Kind == "Number");
        Assert.DoesNotContain(thing, x => x.Path == "combatPower");
        var kind = InspectorFieldCatalog.For("PawnKindDef", NativeFieldCatalog.Empty);
        Assert.Contains(kind, x => x.Path == "combatPower" && x.Kind == "Number");
        Assert.DoesNotContain(kind, x => x.Path == "statBases/MoveSpeed");
        Assert.All(thing, x => Assert.StartsWith("ThingDef", x.DefType));
    }

    [Fact]
    public void ObservedFieldsAreAdditiveAndAreLabeledAsObservations()
    {
        var source = new VanillaMechanoidLibrary.SourceDef(
            "ThingDef", "Test", null, null, "Biotech", "Defs/Test.xml",
            XElement.Parse("""
                <ThingDef>
                    <defName>MechTest</defName>
                    <statBases><MoveSpeed>5.7</MoveSpeed><CustomSpeed>1.2</CustomSpeed></statBases>
                    <customFlags><usesHeat>true</usesHeat></customFlags>
                    <tools><li><power>12</power></li></tools>
                </ThingDef>
                """));
        var catalog = NativeFieldCatalog.Build([source]);
        var fields = InspectorFieldCatalog.For("ThingDef", catalog);
        Assert.Single(fields.Where(x => x.Path == "statBases/MoveSpeed"));
        Assert.Contains(fields, x => x.Path == "statBases/CustomSpeed" &&
            x.Kind == "Number" && x.IsObserved && x.SourceLabel.Contains("Biotech"));
        Assert.Contains(fields, x => x.Path == "customFlags/usesHeat" && x.Kind == "Boolean");
        Assert.DoesNotContain(fields, x => x.Path.Contains("/li/"));
    }

    [Fact]
    public void SearchIsAcrossFriendlyNamesAndNativePathsWithoutLosingCategories()
    {
        var fields = InspectorFieldCatalog.For("ThingDef", NativeFieldCatalog.Empty);
        Assert.Contains(InspectorFieldCatalog.Filter(fields, "全部", "MoveSpeed"),
            x => x.Path == "statBases/MoveSpeed");
        Assert.Contains(InspectorFieldCatalog.Filter(fields, "全部", "移动"),
            x => x.Path == "statBases/MoveSpeed");
        Assert.DoesNotContain(InspectorFieldCatalog.Filter(fields, "机体", ""),
            x => x.Path == "statBases/MoveSpeed");
        var used = new HashSet<string> { "statBases/MoveSpeed" };
        Assert.DoesNotContain(InspectorFieldCatalog.Filter(fields, "全部", "", used),
            x => x.Path == "statBases/MoveSpeed");
    }

    [Fact]
    public void XmlPathsIdentifySimpleFieldsButNotRepeatedListEntries()
    {
        Assert.Equal("statBases/MoveSpeed", InspectorFieldCatalog.ToRelativePath(
            "/Defs[1]/ThingDef[1]/statBases[1]/MoveSpeed[1]"));
        Assert.Null(InspectorFieldCatalog.ToRelativePath(
            "/Defs[1]/ThingDef[1]/tools[1]/li[2]/power[1]"));
        Assert.Null(InspectorFieldCatalog.ToRelativePath(
            "/Defs[1]/ThingDef[1]/comps[1]/li[1]/compClass[1]"));
    }

    [Fact]
    public void EnumMetadataSuppliesItsActualChoices()
    {
        var fields = InspectorFieldCatalog.For("ThingDef", NativeFieldCatalog.Empty);
        var weight = fields.Single(x => x.Path == "race/mechWeightClass");
        Assert.Equal("Enum", weight.Kind);
        Assert.Contains("Heavy", weight.Choices);
        Assert.Equal("机体", weight.Category);
    }
}
