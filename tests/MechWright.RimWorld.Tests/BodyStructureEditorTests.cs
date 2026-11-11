using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class BodyStructureEditorTests
{
    private const string Sample = """
        <Defs><BodyDef><defName>MP_StructuralBody</defName>
        <corePart><def>MechanicalThorax</def><height>Middle</height><depth>Outside</depth>
        <parts><li><def>MechanicalHead</def><coverage>0.20</coverage><height>Top</height>
        <depth>Outside</depth><parts><li><def>ArtificialBrain</def><coverage>0.1</coverage>
        <height>Middle</height><depth>Inside</depth></li></parts></li></parts>
        </corePart></BodyDef></Defs>
        """;

    private static (DefDocument Doc, DefEntry Body) Load()
    {
        var document = DefDocument.Parse(Sample);
        return (document, document.Definitions().Single());
    }

    [Fact]
    public void InsertedChildHasCorrectCoverageAndUndoRedo()
    {
        var (doc, body) = Load();
        var core = doc.BodyParts(body)[0];
        BodyStructureEditor.AddChild(doc, core, "MechanicalArm", "0.15", "Middle", "Outside");
        var parts = doc.BodyParts(doc.Definitions().Single());
        Assert.Equal(4, parts.Count);
        Assert.Equal(0.15, parts.Last().CumulativeCoverage, 6);
        Assert.True(doc.Undo());
        Assert.Equal(3, doc.BodyParts(doc.Definitions().Single()).Count);
        Assert.True(doc.Redo());
        Assert.Equal(4, doc.BodyParts(doc.Definitions().Single()).Count);
    }

    [Fact]
    public void ChildUpdateModifiesOnlySelectedPart()
    {
        var (doc, body) = Load();
        var head = doc.BodyParts(body)[1];
        BodyStructureEditor.Update(doc, head, "MechanicalHead", "0.25",
            "Top", "Outside", "optical armor");
        Assert.Contains("<customLabel>optical armor</customLabel>", doc.Export());
        var brain = doc.BodyParts(doc.Definitions().Single()).Last();
        Assert.Equal(0.025, brain.CumulativeCoverage, 6);
        Assert.True(doc.Undo());
        Assert.Equal(Sample, doc.Export());
    }

    [Fact]
    public void RemovingParentRemovesChildAndUndoRestoresBoth()
    {
        var (doc, body) = Load();
        BodyStructureEditor.Remove(doc, doc.BodyParts(body)[1]);
        Assert.Single(doc.BodyParts(doc.Definitions().Single()));
        Assert.True(doc.Undo());
        Assert.Equal(3, doc.BodyParts(doc.Definitions().Single()).Count);
    }

    [Fact]
    public void RootCannotBeRemovedAndInvalidCoverageIsTransactional()
    {
        var (doc, body) = Load();
        Assert.Throws<InvalidOperationException>(() => BodyStructureEditor.Remove(doc, doc.BodyParts(body)[0]));
        Assert.Throws<ArgumentException>(() =>
            BodyStructureEditor.AddChild(doc, doc.BodyParts(body)[0], "MechanicalArm", "1.5"));
        Assert.Equal(Sample, doc.Export());
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void SiblingReorderWorksWithoutLosingNestedParts()
    {
        var (doc, body) = Load();
        BodyStructureEditor.AddChild(doc, doc.BodyParts(body)[0], "Reactor", "0.05");
        var third = doc.BodyParts(doc.Definitions().Single()).Last();
        BodyStructureEditor.Move(doc, third, -1);
        var current = doc.BodyParts(doc.Definitions().Single());
        Assert.Equal("Reactor", current[1].PartDef);
        Assert.Equal("ArtificialBrain", current.Last().PartDef);
        Assert.True(doc.Undo());
        Assert.Equal("MechanicalHead", doc.BodyParts(doc.Definitions().Single())[1].PartDef);
    }
}
