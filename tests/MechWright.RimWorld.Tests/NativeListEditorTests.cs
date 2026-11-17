using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class NativeListEditorTests
{
    private const string Source = """
        <Defs><!-- keep -->
          <ThingDef ParentName="BaseMechanoidWalker"><defName>MP_A</defName>
            <verbs Inherit="False"><li Class="VerbProperties"><verbClass>Verb_Shoot</verbClass>
              <defaultProjectile>MP_Bullet</defaultProjectile></li>
              <!-- preserve -->
              <li><verbClass>Verb_MeleeAttack</verbClass></li></verbs>
            <custom>keep me</custom>
          </ThingDef>
        </Defs>
        """;

    [Fact]
    public void AddEditDuplicateMoveAndUndoKeepOriginalElementsAndComments()
    {
        var doc = DefDocument.Parse(Source);
        var def = doc.Definitions().Single();
        Assert.Equal(2, NativeListEditor.Items(doc, def, "verbs").Count);
        var path = NativeListEditor.Add(doc, def, "verbs",
            "<li><verbClass>Verb_ShootBeam</verbClass></li>");
        Assert.Equal(3, NativeListEditor.Items(doc, doc.Definitions().Single(), "verbs").Count);
        var replaced = NativeListEditor.Replace(doc, doc.Definitions().Single(), "verbs",
            path, "<li Class='Custom.Verb'><verbClass>Verb_ShootBeam</verbClass><other>x</other></li>");
        var dup = NativeListEditor.Duplicate(doc, doc.Definitions().Single(), "verbs", replaced);
        var moved = NativeListEditor.Move(doc, doc.Definitions().Single(), "verbs", dup, -1);
        Assert.Equal(4, NativeListEditor.Items(doc, doc.Definitions().Single(), "verbs").Count);
        Assert.Contains("Class=\"Custom.Verb\"", doc.Export());
        Assert.Contains("<!-- preserve -->", doc.Export());
        Assert.Contains("Inherit=\"False\"", doc.Export());
        NativeListEditor.Remove(doc, doc.Definitions().Single(), "verbs", moved);
        Assert.Equal(3, NativeListEditor.Items(doc, doc.Definitions().Single(), "verbs").Count);
        Assert.True(doc.Undo());
        Assert.Equal(4, NativeListEditor.Items(doc, doc.Definitions().Single(), "verbs").Count);
        // Reordering two byte-identical siblings is a semantic no-op; no undo record.
        for (var i = 0; i < 3; i++) Assert.True(doc.Undo());
        Assert.Equal(Source, doc.Export());
    }

    [Fact]
    public void RefusesMultipleNodesAndDtdAndWrongContainerWithoutMutating()
    {
        var doc = DefDocument.Parse(Source);
        var def = doc.Definitions().Single();
        Assert.ThrowsAny<Exception>(() =>
            NativeListEditor.Add(doc, def, "verbs", "<li/><li/>"));
        Assert.ThrowsAny<Exception>(() =>
            NativeListEditor.Add(doc, def, "verbs", "<!DOCTYPE li><li/>"));
        Assert.Throws<InvalidOperationException>(() =>
            NativeListEditor.Add(doc, def, "custom", "<li/>"));
        Assert.Equal(Source, doc.Export());
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void CanCreateNewListContainerAndUndoIt()
    {
        var doc = DefDocument.Parse(Source);
        var result = NativeListEditor.Add(doc, doc.Definitions().Single(),
            "modExtensions", "<li Class='CompProperties'><customField>42</customField></li>");
        Assert.Contains("<modExtensions>", doc.Export());
        Assert.Contains("customField", doc.Export());
        Assert.True(doc.Undo());
        Assert.Equal(Source, doc.Export());
        Assert.True(doc.Redo());
        Assert.Single(NativeListEditor.Items(doc, doc.Definitions().Single(), "modExtensions"));
    }

    [Fact]
    public void ReorderingMovesInsteadOfCloning()
    {
        var doc = DefDocument.Parse(Source);
        var def = doc.Definitions().Single();
        var first = NativeListEditor.Items(doc, def, "verbs").First();
        NativeListEditor.Move(doc, def, "verbs", first.XmlPath, 1);
        var items = NativeListEditor.Items(doc, doc.Definitions().Single(), "verbs");
        Assert.Equal(2, items.Count);
        Assert.Contains("Verb_Shoot", items.Last().Source);
        Assert.True(doc.Undo());
        Assert.Equal(Source, doc.Export());
    }
    [Fact]
    public void ExplicitListLeafEditRoundTripsAndUndoPreservesUnknownXml()
    {
        var doc = DefDocument.Parse(Source);
        var first = NativeListEditor.Items(doc, doc.Definitions().Single(), "verbs").First();
        NativeListEditor.SetItemField(doc, doc.Definitions().Single(), "verbs",
            first.XmlPath, "defaultProjectile", "MP_NewBullet");
        Assert.Contains("<defaultProjectile>MP_NewBullet</defaultProjectile>", doc.Export());
        Assert.Contains("<!-- preserve -->", doc.Export());
        Assert.Contains("Inherit=\\\"False\\\"", doc.Export());
        var loaded = DefDocument.Parse(doc.Export());
        Assert.Equal("MP_NewBullet", loaded.Xml.Root!.Element("ThingDef")!
            .Element("verbs")!.Elements("li").First().Element("defaultProjectile")!.Value);
        Assert.True(doc.Undo());
        Assert.Equal(Source, doc.Export());
        Assert.True(doc.Redo());
        Assert.Contains("MP_NewBullet", doc.Export());
    }

    [Fact]
    public void AmbiguousOrMissingListLeavesAndContainersAreRejected()
    {
        var doc = DefDocument.Parse(Source);
        var first = NativeListEditor.Items(doc, doc.Definitions().Single(), "verbs").First();
        var before = doc.Export();
        Assert.Throws<InvalidOperationException>(() => NativeListEditor.SetItemField(
            doc, doc.Definitions().Single(), "verbs", first.XmlPath, "notThere", "x"));
        Assert.Throws<ArgumentException>(() => NativeListEditor.SetItemField(
            doc, doc.Definitions().Single(), "verbs", first.XmlPath, "../defName", "x"));
        Assert.Equal(before, doc.Export());
        Assert.False(doc.IsDirty);

        var duplicate = DefDocument.Parse("<Defs><ThingDef><defName>MP_X</defName>" +
            "<verbs><li><range>1</range></li></verbs>" +
            "<verbs><li><range>2</range></li></verbs></ThingDef></Defs>");
        Assert.Throws<InvalidOperationException>(() => NativeListEditor.Items(
            duplicate, duplicate.Definitions().Single(), "verbs"));
        Assert.False(duplicate.IsDirty);
    }


    [Fact]
    public void IndexedNestedListLeafTargetsOnlyChosenSiblingAndRoundTrips()
    {
        const string source = """
            <Defs><ThingDef ParentName="BaseMechanoidWalker"><defName>MP_Indexed</defName>
              <tools><li Class="Tool"><parts><li><coverage>0.2</coverage></li>
                <li><coverage>0.8</coverage></li></parts><unknown keep="yes">untouched</unknown></li></tools>
            </ThingDef></Defs>
            """;
        var doc = DefDocument.Parse(source);
        var item = NativeListEditor.Items(doc, doc.Definitions().Single(), "tools").Single();
        Assert.Throws<InvalidOperationException>(() => NativeListEditor.SetItemField(
            doc, doc.Definitions().Single(), "tools", item.XmlPath, "parts/li/coverage", "0.5"));
        Assert.Throws<InvalidOperationException>(() => NativeListEditor.SetItemField(
            doc, doc.Definitions().Single(), "tools", item.XmlPath, "parts/li[3]/coverage", "0.5"));
        Assert.Throws<ArgumentException>(() => NativeListEditor.SetItemField(
            doc, doc.Definitions().Single(), "tools", item.XmlPath, "parts/li[0]/coverage", "0.5"));
        Assert.Equal(source, doc.Export());
        Assert.False(doc.IsDirty);

        NativeListEditor.SetItemField(doc, doc.Definitions().Single(), "tools",
            item.XmlPath, "parts/li[2]/coverage", "0.5");
        var exported = doc.Export();
        var loaded = DefDocument.Parse(exported);
        var children = loaded.Xml.Root!.Element("ThingDef")!.Element("tools")!
            .Element("li")!.Element("parts")!.Elements("li").ToArray();
        Assert.Equal("0.2", children[0].Element("coverage")!.Value);
        Assert.Equal("0.5", children[1].Element("coverage")!.Value);
        Assert.Contains("""keep="yes" """.Trim(), exported);
        Assert.Contains("""ParentName="BaseMechanoidWalker" """.Trim(), exported);
        Assert.True(doc.Undo());
        Assert.Equal(source, doc.Export());
        Assert.True(doc.Redo());
        Assert.Equal(exported, doc.Export());
    }

}
