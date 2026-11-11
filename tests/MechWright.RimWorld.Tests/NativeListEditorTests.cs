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
        for (var i = 0; i < 5; i++) Assert.True(doc.Undo());
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
}
