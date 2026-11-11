using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class VanillaMechComparisonTests
{
    private static VanillaMechanoidLibrary.Profile Original() =>
        new("Mech_Vanilla", "Vanilla mech", "", "Core", "Core.xml", "BaseMechanoid",
            [
                new("战斗点数 CP", "400", "combatPower", null, "Core"),
                new("移动速度", "4.7", "statBases/MoveSpeed", "BaseMechanoid", "Core"),
                new("身体大小", "1.2", "race/baseBodySize", null, "Core")
            ], [], [], [], [], [], "MechanicalBody", "Medium", "", []);

    [Fact]
    public void CompareShowsExplicitValuesAndDeltaWithoutInventingInheritedStats()
    {
        var doc = DefDocument.Parse(
            "<Defs><ThingDef><defName>MP_Manta</defName><statBases><MoveSpeed>3.9</MoveSpeed></statBases></ThingDef></Defs>");
        var result = VanillaMechComparison.Compare(Original(), doc, doc.Definitions().Single());
        Assert.Equal("-0.8", result.Rows.Single(r => r.XmlPath == "statBases/MoveSpeed").Delta);
        Assert.Equal("未显式配置", result.Rows.Single(r => r.XmlPath == "race/baseBodySize").UserExplicitValue);
        Assert.Equal("未显式配置", result.Rows.Single(r => r.XmlPath == "combatPower").UserExplicitValue);
        Assert.Contains("PawnKindDef", result.Notice);
    }

    [Fact]
    public void CompareCanReadPawnKindFromAnotherProjectFile()
    {
        var project = new MechWorkspace("Test", "");
        var file = project.AddXml("1.7/Defs/Mech.xml",
            "<Defs><ThingDef><defName>MP_Manta</defName><statBases><MoveSpeed>4.7</MoveSpeed></statBases></ThingDef></Defs>");
        project.AddXml("1.7/Defs/Kinds.xml",
            "<Defs><PawnKindDef><defName>MP_Manta</defName><race>MP_Manta</race><combatPower>450</combatPower></PawnKindDef></Defs>");
        var result = VanillaMechComparison.Compare(Original(), file.Document,
            file.Document.Definitions().Single(), project);
        Assert.Equal("+50", result.Rows.Single(r => r.XmlPath == "combatPower").Delta);
        Assert.DoesNotContain("未关联", result.Notice);
    }

    [Fact]
    public void CannotCompareAgainstBodyDefOrOldDocumentSelection()
    {
        var doc = DefDocument.Parse("<Defs><BodyDef><defName>MP_B</defName><corePart><def>Mechanics</def></corePart></BodyDef></Defs>");
        Assert.Throws<ArgumentException>(() =>
            VanillaMechComparison.Compare(Original(), doc, doc.Definitions().Single()));
    }
}
