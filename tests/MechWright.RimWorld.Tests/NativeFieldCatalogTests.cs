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
}
