using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class MechanoidPropertyTests
{
    private static readonly string Sample =
        "<Defs><!-- keep --><ThingDef ParentName=\"BaseMechanoidWalker\"><defName>MP_Manta</defName><extra Class=\"Mod.X\">yes</extra></ThingDef></Defs>";

    [Fact]
    public void SuggestedDefaultsDoNotWriteToXmlUntilApplied()
    {
        var doc = DefDocument.Parse(Sample);
        var spec = MechanoidProperties.For("ThingDef").Single(p => p.FieldName == "MoveSpeed");
        Assert.Equal("4.7", spec.Suggestion);
        Assert.Null(MechanoidProperties.Read(doc.Definitions().Single(), spec));
        Assert.Equal(Sample, doc.Export());
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void ApplyingOverrideCreatesOnlyRequiredContainers()
    {
        var doc = DefDocument.Parse(Sample);
        var spec = MechanoidProperties.For("ThingDef").Single(p => p.FieldName == "MoveSpeed");
        MechanoidProperties.Apply(doc, doc.Definitions().Single(), spec, "3.9");
        Assert.Contains("<statBases><MoveSpeed>3.9</MoveSpeed></statBases>", doc.Export());
        Assert.Contains("Class=\"Mod.X\"", doc.Export());
        Assert.Contains("<!-- keep -->", doc.Export());
        Assert.True(doc.Undo());
        Assert.Equal(Sample, doc.Export());
        Assert.True(doc.Redo());
        Assert.Equal("3.9", MechanoidProperties.Read(doc.Definitions().Single(), spec));
    }

    [Fact]
    public void ResetRemovesOnlyExplicitFieldAndPreservesSiblings()
    {
        const string xml = "<Defs><ThingDef><defName>MP_X</defName><statBases><MoveSpeed>3</MoveSpeed><Mass>100</Mass></statBases></ThingDef></Defs>";
        var doc = DefDocument.Parse(xml);
        var spec = MechanoidProperties.For("ThingDef").Single(p => p.FieldName == "MoveSpeed");
        MechanoidProperties.Reset(doc, doc.Definitions().Single(), spec);
        Assert.Contains("<Mass>100</Mass>", doc.Export());
        Assert.DoesNotContain("<MoveSpeed>", doc.Export());
        Assert.True(doc.Undo());
        Assert.Equal(xml, doc.Export());
    }

    [Fact]
    public void InvalidValuesAndWrongDefTypesDoNotChangeXml()
    {
        var doc = DefDocument.Parse(Sample);
        var move = MechanoidProperties.For("ThingDef").Single(p => p.FieldName == "MoveSpeed");
        Assert.Throws<ArgumentException>(() => MechanoidProperties.Apply(doc, doc.Definitions().Single(), move, "abc"));
        Assert.Throws<ArgumentException>(() => MechanoidProperties.Apply(doc, doc.Definitions().Single(), move, "-2"));
        Assert.Equal(Sample, doc.Export());
    }
}
