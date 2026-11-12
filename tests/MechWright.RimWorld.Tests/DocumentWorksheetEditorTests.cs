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
    public void InvalidIdentityCannotMutateXml()
    {
        var doc = DefDocument.CreateEmpty();
        Assert.Throws<ArgumentException>(() =>
            DocumentWorksheetEditor.AppendDefinition(doc, "ThingDef", "Bad", ""));
        Assert.Empty(doc.Definitions());
    }
}
