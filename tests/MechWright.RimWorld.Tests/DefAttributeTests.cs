using System.Xml;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class DefAttributeTests
{
    private const string Xml = """
        <Defs><ThingDef ParentName="BaseMechanoidWalker" MayRequire="Ludeon.RimWorld.Biotech">
        <defName>MP_X</defName><comp Class="Custom.Comp"><value>1</value></comp>
        </ThingDef></Defs>
        """;

    [Fact]
    public void EditsAnyNativeXmlAttributeWithoutLosingSiblings()
    {
        var doc = DefDocument.Parse(Xml);
        var def = doc.Definitions().Single();
        Assert.Equal(3, doc.Attributes(def).Count);
        var parent = doc.Attributes(def).Single(x => x.Name == "ParentName");
        doc.SetAttribute(parent, "BaseMechanoid");
        Assert.Contains("ParentName=\"BaseMechanoid\"", doc.Export());
        Assert.Contains("MayRequire=\"Ludeon.RimWorld.Biotech\"", doc.Export());
        Assert.Contains("Class=\"Custom.Comp\"", doc.Export());
        Assert.True(doc.Undo());
        Assert.Equal(Xml, doc.Export());
    }

    [Fact]
    public void AddAndRemoveDefinitionAttributesAreUndoable()
    {
        var doc = DefDocument.Parse(Xml);
        doc.AddDefinitionAttribute(doc.Definitions().Single(), "Abstract", "True");
        Assert.Contains("Abstract=\"True\"", doc.Export());
        var attr = doc.Attributes(doc.Definitions().Single()).Single(x => x.Name == "Abstract");
        doc.RemoveAttribute(attr);
        Assert.DoesNotContain("Abstract=\"True\"", doc.Export());
        Assert.True(doc.Undo());
        Assert.Contains("Abstract=\"True\"", doc.Export());
        Assert.True(doc.Undo());
        Assert.Equal(Xml, doc.Export());
    }

    [Fact]
    public void UnsafeOrDuplicateAttributeNamesAreRefused()
    {
        var doc = DefDocument.Parse(Xml);
        Assert.Throws<XmlException>(() =>
            doc.AddDefinitionAttribute(doc.Definitions().Single(), "Bad:name", "x"));
        Assert.Throws<InvalidOperationException>(() =>
            doc.AddDefinitionAttribute(doc.Definitions().Single(), "ParentName", "x"));
        Assert.Equal(Xml, doc.Export());
    }
}
