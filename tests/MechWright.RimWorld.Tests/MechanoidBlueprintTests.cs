using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class MechanoidBlueprintTests
{
    [Fact]
    public void GeneratesThreeCoherentNativeDefFilesWithUserChosenValues()
    {
        var blueprint = new MechanoidBlueprint("MP_Manta", "机械蝠鲼",
            WeightClass: "Medium", CombatPower: 400, BodySize: 1.6,
            HealthScale: 1.35, MoveSpeed: 3.9, Mass: 120,
            SharpArmor: .55, BluntArmor: .35, HeatArmor: 2.0,
            BandwidthCost: 3);
        var files = blueprint.GenerateFiles();
        Assert.Equal(3, files.Count);
        var project = new MechWorkspace("MechProtocol", "");
        Assert.Equal(3, blueprint.CreateIn(project).Count);
        Assert.Equal(3, project.DefinitionCount);
        Assert.NotNull(project.FindDefinitionFile("ThingDef", "MP_Manta"));
        Assert.NotNull(project.FindDefinitionFile("BodyDef", "MP_Manta_Body"));
        var body = project.FindDefinitionFile("BodyDef", "MP_Manta_Body")!.Document;
        Assert.Single(body.BodyParts(body.Definitions().Single()));
        Assert.Contains("MP_Manta_Body", project.FindDefinitionFile("ThingDef", "MP_Manta")!.Document.Export());
        Assert.Contains("<combatPower>400</combatPower>",
            project.FindDefinitionFile("PawnKindDef", "MP_Manta")!.Document.Export());
        Assert.DoesNotContain(project.Validate(), x => x.Issue.Code == "MW020");
        Assert.True(project.IsDirty);
    }

    [Fact]
    public void RejectsConflictingDefinitionsWithoutPartialProjectInsertion()
    {
        var project = new MechWorkspace("Test", "");
        project.AddXml("Defs/Existing.xml",
            "<Defs><ThingDef><defName>MP_Scout</defName></ThingDef></Defs>");
        var before = project.Files.Count;
        Assert.Throws<InvalidOperationException>(() =>
            new MechanoidBlueprint("MP_Scout", "scout").CreateIn(project));
        Assert.Equal(before, project.Files.Count);
    }

    [Fact]
    public void InvalidBlueprintCannotCreateAnyFiles()
    {
        var project = new MechWorkspace("Test", "");
        Assert.Throws<ArgumentException>(() =>
            new MechanoidBlueprint("Invalid", "test").CreateIn(project));
        Assert.Throws<ArgumentException>(() =>
            new MechanoidBlueprint("MP_Test", "test", BodySize: 0).CreateIn(project));
        Assert.Throws<ArgumentException>(() =>
            new MechanoidBlueprint("MP_Test", "test", MoveSpeed: double.NaN).CreateIn(project));
        Assert.Empty(project.Files);
    }

    [Fact]
    public void BatchXmlInsertionIsAtomicOnMalformedDocumentOrCollision()
    {
        var project = new MechWorkspace("Test", "");
        Assert.ThrowsAny<Exception>(() => project.AddXmlBatch(new Dictionary<string, string>
        {
            ["1.7/Defs/A.xml"] = "<Defs/>",
            ["1.7/Defs/B.xml"] = "<Defs><ThingDef>"
        }));
        Assert.Empty(project.Files);
        project.AddXml("Defs/A.xml", "<Defs/>");
        Assert.Throws<InvalidOperationException>(() =>
            project.AddXmlBatch(new Dictionary<string, string>
            {
                ["Defs/A.xml"] = "<Defs/>",
                ["Defs/New.xml"] = "<Defs/>"
            }));
        Assert.Single(project.Files);
    }
}
