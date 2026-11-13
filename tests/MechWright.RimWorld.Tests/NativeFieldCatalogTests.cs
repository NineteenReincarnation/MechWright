using System.Xml.Linq;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class NativeFieldCatalogTests
{
    private static NativeFieldCatalog Create()
    {
        var core = new VanillaMechanoidLibrary.SourceDef(
            "ThingDef", "Mech_One", null, null, "Core", "Core.xml",
            XElement.Parse("<ThingDef><defName>Mech_One</defName><statBases><MoveSpeed>3.9</MoveSpeed></statBases><tools><li><power>8</power></li></tools></ThingDef>"));
        var biotech = new VanillaMechanoidLibrary.SourceDef(
            "ThingDef", "Mech_Two", null, null, "Biotech", "Biotech.xml",
            XElement.Parse("<ThingDef><defName>Mech_Two</defName><statBases><MoveSpeed>4.7</MoveSpeed><BandwidthCost>3</BandwidthCost></statBases></ThingDef>"));
        return NativeFieldCatalog.Build([core, biotech]);
    }

    [Fact]
    public void DiscoversCrossDlcScalarsAndLabelsObservedTypes()
    {
        var catalog = Create();
        var speed = Assert.Single(catalog.For("ThingDef").Where(x => x.Path == "statBases/MoveSpeed"));
        Assert.Equal("Number", speed.ObservedType);
        Assert.Equal(2, speed.Observations);
        Assert.Contains("Core", speed.SourcePackages);
        Assert.Contains("Biotech", speed.SourcePackages);
        Assert.True(speed.CanInsert);
    }

    [Fact]
    public void DoesNotOfferDirectInsertionIntoListMembers()
    {
        var field = Assert.Single(Create().For("ThingDef").Where(x => x.Path.EndsWith("li/power")));
        Assert.False(field.CanInsert);
        var doc = DefDocument.Parse("<Defs><ThingDef><defName>MP_X</defName></ThingDef></Defs>");
        Assert.Throws<InvalidOperationException>(() =>
            NativeFieldCatalog.Apply(doc, doc.Definitions().Single(), field, "12"));
    }

    [Fact]
    public void ApplyingSelectedObservedFieldIsExplicitAndUndoable()
    {
        var field = Assert.Single(Create().For("ThingDef").Where(x => x.Path == "statBases/MoveSpeed"));
        var document = DefDocument.Parse("<Defs><ThingDef><defName>MP_X</defName></ThingDef></Defs>");
        var original = document.Export();
        Assert.False(document.IsDirty);
        NativeFieldCatalog.Apply(document, document.Definitions().Single(), field, "4.5");
        Assert.Contains("<MoveSpeed>4.5</MoveSpeed>", document.Export());
        Assert.True(document.Undo());
        Assert.Equal(original, document.Export());
    }

    [Fact]
    public void FieldsAreRestrictedToTheirOwnDefType()
    {
        var field = Assert.Single(Create().For("ThingDef").Where(x => x.Path == "statBases/MoveSpeed"));
        var doc = DefDocument.Parse("<Defs><PawnKindDef><defName>MP_X</defName></PawnKindDef></Defs>");
        Assert.Throws<ArgumentException>(() =>
            NativeFieldCatalog.Apply(doc, doc.Definitions().Single(), field, "4.5"));
    }
    [Fact]
    public void PolymorphicAndCollectionLeavesRemainVisibleButCannotBeInserted()
    {
        var source = new VanillaMechanoidLibrary.SourceDef(
            "ThingDef", "MP_Fixture", null, null, "Biotech", "Fixture.xml",
            XElement.Parse("<ThingDef><defName>MP_Fixture</defName>" +
                "<comps><li Class='CompProperties_Test'><cooldown>12</cooldown></li></comps>" +
                "<verbs><li><range>20</range></li></verbs>" +
                "<lifeStages><li><label>adult</label></li></lifeStages>" +
                "<race><baseBodySize>1.2</baseBodySize></race></ThingDef>"));
        var catalog = NativeFieldCatalog.Build([source]);
        Assert.False(catalog.For("ThingDef").Single(x =>
            x.Path == "comps/li/cooldown").CanInsert);
        Assert.False(catalog.For("ThingDef").Single(x =>
            x.Path == "verbs/li/range").CanInsert);
        Assert.False(catalog.For("ThingDef").Single(x =>
            x.Path == "lifeStages/li/label").CanInsert);
        Assert.True(catalog.For("ThingDef").Single(x =>
            x.Path == "race/baseBodySize").CanInsert);
        var document = DefDocument.Parse(
            "<Defs><ThingDef><defName>MP_New</defName></ThingDef></Defs>");
        var before = document.Export();
        Assert.Throws<InvalidOperationException>(() => NativeFieldCatalog.Apply(
            document, document.Definitions().Single(),
            catalog.For("ThingDef").Single(x => x.Path == "comps/li/cooldown"), "10"));
        Assert.Equal(before, document.Export());
    }

}
