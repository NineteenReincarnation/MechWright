using System.Xml.Linq;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class VisualGestationEditorTests
{
    private static MechWorkspace Project()
    {
        var p=new MechWorkspace("Recipe Test","");
        new MechanoidBlueprint("MP_Test","测试机械体").CreateIn(p);
        return p;
    }
    private static VisualGestationEditor.Configuration Settings(int steel=100)=>
        new("MediumMechanoidRecipe",4,"HighMechtech",
        [
            new("Steel",steel),
            new("Plasteel",90),
            new("ComponentIndustrial",6),
            new("SubcoreHigh",1)
        ]);

    [Fact]
    public void CreatesLinkedNativeGestationRecipeWithIngredientsAndPrerequisites()
    {
        var ws=Project();
        VisualGestationEditor.Apply(ws,"MP_Test","测试机械体",Settings());
        var file=ws.FindDefinitionFile("RecipeDef","MP_Test_Gestate");
        Assert.NotNull(file);
        var xml=file!.Document.Export();
        Assert.Contains("ParentName=\"MediumMechanoidRecipe\"",xml);
        Assert.Contains("<products>",xml);
        Assert.Contains("<MP_Test>1</MP_Test>",xml);
        Assert.Contains("<gestationCycles>4</gestationCycles>",xml);
        Assert.Contains("<researchPrerequisite>HighMechtech</researchPrerequisite>",xml);
        Assert.Contains("Inherit=\"False\"",xml);
        var root=file.Document.Xml.Descendants("RecipeDef").Single();
        var materials=root.Element("ingredients")!.Elements("li").ToArray();
        Assert.Equal(4,materials.Length);
        Assert.Equal("Steel",materials[0].Element("filter")?.Element("thingDefs")?.Element("li")?.Value);
        Assert.Equal("100",materials[0].Element("count")?.Value);
        Assert.Equal(4,VisualGestationEditor.Read(ws,"MP_Test")?.Cycles);
    }

    [Fact]
    public void UpdatesRecipeWithoutReplacingUnknownMetadataAndUndoWorks()
    {
        var ws=Project();
        VisualGestationEditor.Apply(ws,"MP_Test","test",Settings());
        var file=ws.FindDefinitionFile("RecipeDef","MP_Test_Gestate")!;
        var before=file.Document.Export();
        VisualGestationEditor.Apply(ws,"MP_Test","test",Settings(170));
        var after=file.Document.Export();
        Assert.Contains("<count>170</count>",after);
        Assert.True(file.Document.Undo());
        Assert.Equal(before,file.Document.Export());
    }

    [Fact]
    public void RejectsInvalidOrDuplicateMaterialsWithoutCreatingPartialXml()
    {
        var ws=Project();
        var before=ws.Files.Count;
        Assert.Throws<ArgumentException>(()=>VisualGestationEditor.Apply(ws,
            "MP_Test","test",Settings() with
            {
                Ingredients=[new("Steel",10),new("Steel",20)]
            }));
        Assert.Equal(before,ws.Files.Count);
    }

    [Fact]
    public void RefusesToReplaceCustomIngredientLogic()
    {
        var ws=Project();
        VisualGestationEditor.Apply(ws,"MP_Test","test",Settings());
        var file=ws.FindDefinitionFile("RecipeDef","MP_Test_Gestate")!;
        var original=file.Document.Export();
        file.Document.Transform(_=>file.Document.Xml.Descendants("ingredients")
            .Single().Add(new XElement("customFilter","keep")));
        var before=file.Document.Export();
        Assert.Throws<InvalidOperationException>(()=>
            VisualGestationEditor.Apply(ws,"MP_Test","test",Settings(160)));
        Assert.Equal(before,file.Document.Export());
        Assert.NotEqual(original,before);
    }
}
