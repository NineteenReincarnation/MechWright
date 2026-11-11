using System.Xml.Linq;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class VisualMechDesignerTests
{
    [Fact]
    public void ChassisPresetEditsLinkedDefinitionsWithoutDiscardingUnknownFields()
    {
        var project = new MechWorkspace("Test", "");
        new MechanoidBlueprint("MP_Visual", "可视化测试").CreateIn(project);
        var doc = project.FindDefinitionFile("ThingDef", "MP_Visual")!.Document;
        var initial = doc.Export();
        var preset = VisualMechDesigner.Chassis.Single(x => x.Key == "assault");
        VisualMechDesigner.ApplyBasic(project, "MP_Visual",
            VisualMechDesigner.BasicSettings.From(preset));
        Assert.Contains("<MoveSpeed>3.9</MoveSpeed>", doc.Export());
        Assert.Contains("<baseBodySize>1.65</baseBodySize>", doc.Export());
        var kind = project.FindDefinitionFile("PawnKindDef", "MP_Visual")!.Document;
        Assert.Contains("<combatPower>420</combatPower>", kind.Export());
        Assert.True(doc.Undo());
        Assert.Equal(initial, doc.Export());
    }

    [Fact]
    public void MeleePresetCreatesActualNativeToolNotPlaceholderXml()
    {
        var project = new MechWorkspace("Test", "");
        new MechanoidBlueprint("MP_Test", "普通机体").CreateIn(project);
        var doc = project.FindDefinitionFile("ThingDef", "MP_Test")!.Document;
        VisualMechDesigner.AddMelee(doc, doc.Definitions().Single(),
            VisualMechDesigner.Melee[0], 11, 2.5);
        var tool = doc.Xml.Descendants("tools").Elements("li").Single();
        Assert.Equal("Blunt", tool.Element("capacities")?.Element("li")?.Value);
        Assert.Equal("11", tool.Element("power")?.Value);
        Assert.Equal("2.5", tool.Element("cooldownTime")?.Value);
        Assert.True(doc.Undo());
        Assert.Empty(doc.Xml.Descendants("tools"));
    }

    [Fact]
    public void BodyPresetReplacesOnlyCoreAndKeepsOwnDefName()
    {
        var project = new MechWorkspace("Test", "");
        new MechanoidBlueprint("MP_Test", "普通机体").CreateIn(project);
        var doc = project.FindDefinitionFile("BodyDef", "MP_Test_Body")!.Document;
        var before = doc.Export();
        var template = XElement.Parse("""
            <BodyDef><defName>MechanicalBody</defName>
              <corePart><def>MechanicalThorax</def><parts>
                <li><def>MechanicalHead</def><coverage>0.25</coverage></li>
              </parts></corePart></BodyDef>
            """);
        VisualMechDesigner.ApplyBodyTemplate(doc, doc.Definitions().Single(), template);
        Assert.Contains("MechanicalHead", doc.Export());
        Assert.Contains("<defName>MP_Test_Body</defName>", doc.Export());
        Assert.True(doc.Undo());
        Assert.Equal(before, doc.Export());
    }

    [Fact]
    public void PawnGraphicUsesSingleEditableNativeLifeStage()
    {
        var project = new MechWorkspace("Test", "");
        new MechanoidBlueprint("MP_Test", "普通机体").CreateIn(project);
        var doc = project.FindDefinitionFile("PawnKindDef", "MP_Test")!.Document;
        VisualMechDesigner.SetPawnGraphic(doc, doc.Definitions().Single(),
            "Things/Pawn/MP_Test", 2.8);
        Assert.Equal("Things/Pawn/MP_Test",
            doc.Xml.Descendants("bodyGraphicData").Elements("texPath").Single().Value);
        Assert.Equal("2.8", doc.Xml.Descendants("drawSize").Single().Value);
        Assert.Throws<ArgumentException>(() => VisualMechDesigner.SetPawnGraphic(
            doc, doc.Definitions().Single(), "../private", 2.8));
    }
}
