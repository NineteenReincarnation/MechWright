using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class DocumentWorksheetEditorTests
{
    [Fact]
    public void BlankFileRemainsBlankUntilUserAddsDefinition()
    {
        var document = DefDocument.CreateEmpty();
        Assert.Empty(document.Definitions());
        var entry = DocumentWorksheetEditor.AppendDefinition(
            document, "ThingDef", "MP_NewPart", "");
        Assert.Equal("MP_NewPart", entry.DefName);
        Assert.Null(document.Xml.Root!.Element("ThingDef")!.Element("statBases"));
        Assert.Null(document.Xml.Root!.Element("ThingDef")!.Element("race"));
        Assert.Null(document.Xml.Root!.Element("ThingDef")!.Attribute("ParentName"));
        Assert.Null(document.Xml.Root!.Element("ThingDef")!.Element("label"));
        Assert.Single(document.Definitions());
        Assert.True(document.Undo());
        Assert.Empty(document.Definitions());
        Assert.True(document.Redo());
        Assert.Single(document.Definitions());
    }

    [Fact]
    public void ListsAndEmptyContainersAreEditableWithoutReplacingUnrelatedNodes()
    {
        var document = DefDocument.Parse("""
            <Defs>
              <!-- Preserve my comment -->
              <ThingDef MayRequire="Ludeon.RimWorld.Biotech">
                <defName>MP_Demo</defName>
                <strangeField Flag="keep">original</strangeField>
              </ThingDef>
            </Defs>
            """);
        var entry = document.Definitions().Single();
        var root = FreeformDefinitionEditor.Read(document, entry)[0].Path;
        var statPath = DocumentWorksheetEditor.AddField(document, entry,
            root, "statBases", "", true);
        Assert.True(DocumentWorksheetEditor.CanContainFields(
            FreeformDefinitionEditor.Read(document, entry)
                .Single(x => x.Path == statPath)));
        var move = DocumentWorksheetEditor.AddField(document, entry,
            statPath, "MoveSpeed", "4.7", false);
        DocumentWorksheetEditor.UpdateValue(document, entry, move, "5.3");
        Assert.Equal("5.3", document.Xml.Root!.Element("ThingDef")!.Element("statBases")?.Element("MoveSpeed")?.Value);

        var list = DocumentWorksheetEditor.AddField(document, entry, root,
            "tools", "", true);
        DocumentWorksheetEditor.AddField(document, entry, list, "li", "", true);
        DocumentWorksheetEditor.AddField(document, entry, list, "li", "", true);
        Assert.Equal(2, document.Xml.Root!.Element("ThingDef")!.Element("tools")?.Elements("li").Count());

        Assert.Contains("Preserve my comment", document.Export());
        Assert.Contains("Flag=\"keep\"", document.Export());
        Assert.Equal("original", document.Xml.Root!.Element("ThingDef")!.Element("strangeField")?.Value);
        Assert.Throws<InvalidOperationException>(() =>
            DocumentWorksheetEditor.AddField(document, entry, statPath,
                "MoveSpeed", "1", false));
    }

    [Fact]
    public void QuickAddPropertyCreatesRequiredParentsInOneUndoStep()
    {
        var document = DefDocument.Parse("""
            <Defs>
              <!-- Preserve settings -->
              <ThingDef><defName>MP_Test</defName><customValue X="retain">yes</customValue></ThingDef>
            </Defs>
            """);
        var def = document.Definitions().Single();
        DocumentWorksheetEditor.AppendFieldPath(document, def, "statBases/MoveSpeed", "5.6");
        Assert.Equal("5.6", document.Xml.Root!.Element("ThingDef")!
            .Element("statBases")?.Element("MoveSpeed")?.Value);
        Assert.Throws<InvalidOperationException>(() =>
            DocumentWorksheetEditor.AppendFieldPath(document, def, "statBases/MoveSpeed", "4"));
        Assert.Contains("X=\"retain\"", document.Export());
        Assert.True(document.Undo());
        Assert.Null(document.Xml.Root!.Element("ThingDef")!.Element("statBases"));
        Assert.True(document.Redo());
        Assert.Equal("5.6", document.Xml.Root!.Element("ThingDef")!
            .Element("statBases")?.Element("MoveSpeed")?.Value);
    }

    [Fact]
    public void ValidationDoesNotPartiallyCreateParents()
    {
        var document = DefDocument.CreateEmpty();
        var def = DocumentWorksheetEditor.AppendDefinition(
            document, "ThingDef", "MP_Test", "");
        Assert.Throws<ArgumentException>(() =>
            DocumentWorksheetEditor.AppendFieldPath(document, def, "statBases/MoveSpeed", "-10"));
        Assert.Null(document.Xml.Root!.Element("ThingDef")!.Element("statBases"));
        DocumentWorksheetEditor.SetDisplayLabel(document, def, "机体测试");
        Assert.Equal("机体测试", document.Xml.Root!.Element("ThingDef")!.Element("label")?.Value);
    }

    [Theory]
    [InlineData("<race Class=\"CustomRace\"><baseBodySize>1</baseBodySize></race>", "race/newField")]
    [InlineData("<race><baseBodySize>1</baseBodySize></race><race><baseBodySize>2</baseBodySize></race>", "race/newField")]
    [InlineData("<race><stats Class=\"CustomStats\" /></race>", "race/stats/newField")]
    public void QuickAddRejectsAmbiguousOrPolymorphicParentsWithoutMutation(
        string fragment, string path)
    {
        var document = DefDocument.Parse(
            "<Defs><ThingDef><defName>MP_Test</defName>" + fragment + "</ThingDef></Defs>");
        var before = document.Export();
        var revision = document.Revision;
        Assert.Throws<InvalidOperationException>(() =>
            DocumentWorksheetEditor.AppendFieldPath(
                document, document.Definitions().Single(), path, "5"));
        Assert.Equal(before, document.Export());
        Assert.Equal(revision, document.Revision);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void QuickAddIntoUnambiguousOrdinaryGroupIsReversible()
    {
        var document = DefDocument.Parse("""
            <Defs><ThingDef ParentName="BaseMechanoidWalker"><defName>MP_Test</defName>
            <!-- retained --><race><baseBodySize>1</baseBodySize></race>
            <unknown Flag="keep">value</unknown></ThingDef></Defs>
            """);
        DocumentWorksheetEditor.AppendFieldPath(document,
            document.Definitions().Single(), "race/baseHealthScale", "1.4");
        Assert.Equal("1.4", document.Xml.Root!.Element("ThingDef")!
            .Element("race")!.Element("baseHealthScale")!.Value);
        Assert.Contains("Flag=\"keep\"", document.Export());
        Assert.Contains("retained", document.Export());
        Assert.True(document.Undo());
        Assert.Null(document.Xml.Root!.Element("ThingDef")!
            .Element("race")!.Element("baseHealthScale"));
        Assert.True(document.Redo());
        Assert.Equal("1.4", document.Xml.Root!.Element("ThingDef")!
            .Element("race")!.Element("baseHealthScale")!.Value);
    }

    [Fact]
    public void InvalidIdentityCannotMutateXml()
    {
        var doc = DefDocument.CreateEmpty();
        Assert.Throws<ArgumentException>(() =>
            DocumentWorksheetEditor.AppendDefinition(doc, "ThingDef", "Bad", ""));
        Assert.Empty(doc.Definitions());
    }
}
