using System.Xml;
using MechWright.Core;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class DefDocumentTests
{
    private const string Input = """
        <?xml version="1.0" encoding="utf-8"?>
        <Defs>
          <!-- keep this comment -->
          <ThingDef ParentName="BaseMechanoidWalker" MayRequire="Ludeon.RimWorld.Biotech">
            <defName>MP_Sample</defName>
            <statBases>
              <MoveSpeed>4.2</MoveSpeed>
              <UnknownModField Class="Example.Custom">preserve me</UnknownModField>
            </statBases>
          </ThingDef>
        </Defs>
        """;

    [Fact]
    public void NoEdit_ExportsExactOriginalText()
    {
        var document = DefDocument.Parse(Input);
        Assert.Equal(Input, document.Export());
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void LeafEdit_RetainsUnknownFieldsAttributesAndComments()
    {
        var document = DefDocument.Parse(Input);
        var field = document.Fields(document.Definitions().Single())
            .Single(x => x.Name == "MoveSpeed");
        document.SetField(field, "3.9");
        var exported = document.Export();
        Assert.Contains("<MoveSpeed>3.9</MoveSpeed>", exported);
        Assert.Contains("Class=\"Example.Custom\"", exported);
        Assert.Contains("preserve me", exported);
        Assert.Contains("<!-- keep this comment -->", exported);
        Assert.Contains("ParentName=\"BaseMechanoidWalker\"", exported);
    }

    [Fact]
    public void UndoRedo_RestoresLeafValue()
    {
        var document = DefDocument.Parse(Input);
        var field = document.Fields(document.Definitions().Single())
            .Single(x => x.Name == "MoveSpeed");
        document.SetField(field, "5");
        Assert.True(document.Undo());
        Assert.Equal("4.2", field.Value);
        Assert.False(document.IsDirty);
        Assert.True(document.Redo());
        Assert.Equal("5", field.Value);
    }

    [Fact]
    public void Dtd_IsRejected()
    {
        Assert.Throws<XmlException>(() =>
            DefDocument.Parse("<!DOCTYPE Defs [<!ENTITY external SYSTEM 'file:///secret'>]><Defs/>"));
    }

    [Fact]
    public void DuplicateDefs_AreFlagged()
    {
        var doc = DefDocument.Parse("<Defs><ThingDef><defName>MP_X</defName></ThingDef><ThingDef><defName>MP_X</defName></ThingDef></Defs>");
        Assert.Contains(doc.Validate(), d => d.Code == "MW002" && d.Severity == Severity.Error);
    }

    [Fact]
    public void BodyCoverage_CalculatesParentRelativeFraction()
    {
        var xml = "<Defs><BodyDef><defName>MP_Body</defName><corePart><def>MechanicalThorax</def><parts><li><def>MechanicalHead</def><coverage>0.2</coverage><parts><li><def>ArtificialBrain</def><coverage>0.1</coverage></li></parts></li></parts></corePart></BodyDef></Defs>";
        var doc = DefDocument.Parse(xml);
        var parts = doc.BodyParts(doc.Definitions().Single());
        Assert.Equal(3, parts.Count);
        Assert.Equal(0.02, parts.Last().CumulativeCoverage, 6);
    }

    [Fact]
    public void InsertingDefinition_SupportsUndoAndRedo()
    {
        var document = DefDocument.CreateEmpty();
        document.AddDefinition("ThingDef", "MP_Test");
        Assert.True(document.CanUndo);
        Assert.True(document.Undo());
        Assert.Empty(document.Definitions());
        Assert.False(document.IsDirty);
        Assert.True(document.Redo());
        Assert.Single(document.Definitions());
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void SuccessfulSave_ResetsDirtyStateWithoutReparsing()
    {
        var document = DefDocument.Parse(Input);
        var field = document.Fields(document.Definitions().Single()).Single(x => x.Name == "MoveSpeed");
        document.SetField(field, "3.9");
        var saved = document.Export();
        document.MarkSaved();
        Assert.False(document.IsDirty);
        Assert.Equal(saved, document.Export());
        Assert.False(document.CanUndo);
    }

    [Fact]
    public void NewDefinition_UsesConfiguredPrefixAndAddsRealXml()
    {
        var doc = DefDocument.CreateEmpty();
        doc.AddDefinition("ThingDef", "MP_Sentinel");
        Assert.Contains("<defName>MP_Sentinel</defName>", doc.Export());
        Assert.True(doc.IsDirty);
        Assert.Throws<ArgumentException>(() => doc.AddDefinition("ThingDef", "WithoutPrefix"));
    }
}
