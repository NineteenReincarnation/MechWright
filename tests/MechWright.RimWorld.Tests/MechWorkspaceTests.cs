using MechWright.Core;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public class MechWorkspaceTests
{
    private static MechWorkspace Create()
    {
        var project = new MechWorkspace("Test Mod", "/example/Mod");
        project.AddXml("1.7/Defs/ThingDefs/MP_Manta.xml",
            "<Defs><ThingDef><defName>MP_Manta</defName><race><body>MP_MantaBody</body></race></ThingDef></Defs>");
        project.AddXml("1.7/Defs/BodyDefs/MP_MantaBody.xml",
            "<Defs><BodyDef><defName>MP_MantaBody</defName><corePart><def>MechanicalThorax</def></corePart></BodyDef></Defs>");
        return project;
    }

    [Fact]
    public void TracksDocumentsAndResolvesCrossFileDefinitions()
    {
        var project = Create();
        Assert.Equal(2, project.Files.Count);
        Assert.Equal(2, project.DefinitionCount);
        var file = project.FindDefinitionFile("BodyDef", "MP_MantaBody");
        Assert.NotNull(file);
        Assert.Equal("1.7/Defs/BodyDefs/MP_MantaBody.xml", file.RelativePath);
        var references = project.GetReferences(project.Files[0]);
        var body = Assert.Single(references);
        Assert.Equal(ReferenceLocation.ProjectFile, body.Reference.Location);
        Assert.Equal(file.RelativePath, body.TargetFile);
    }

    [Fact]
    public void CrossFileDuplicateNamesAreBlockingErrors()
    {
        var project = Create();
        project.AddXml("Defs/Other.xml",
            "<Defs><ThingDef><defName>MP_Manta</defName></ThingDef></Defs>");
        Assert.Contains(project.Validate(), x => x.Issue.Code == "MW020" &&
                                              x.Issue.Severity == Severity.Error);
    }

    [Fact]
    public void ProjectEditsRemainDirtyWhenAnotherFileSelected()
    {
        var project = Create();
        var file = project.Files[0];
        var field = file.Document.Fields(file.Document.Definitions().Single())
            .Single(x => x.Name == "defName");
        file.Document.SetField(field, "MP_Manta2");
        Assert.True(project.IsDirty);
        Assert.NotNull(project.FindDefinitionFile("ThingDef", "MP_Manta2"));
        Assert.False(project.Files[1].Document.IsDirty);
        project.MarkAllSaved();
        Assert.False(project.IsDirty);
    }

    [Theory]
    [InlineData("../other.xml")]
    [InlineData("/absolute/evil.xml")]
    [InlineData("Defs/../../outside.xml")]
    [InlineData("C:/Windows/evil.xml")]
    [InlineData("Defs/not-xml.txt")]
    [InlineData("Defs/CON.xml")]
    public void RejectsUnsafeProjectPaths(string path)
    {
        var project = Create();
        Assert.Throws<ArgumentException>(() => project.AddXml(path, "<Defs/>"));
    }

    [Fact]
    public void NewProjectFileIsDirtyUntilFullProjectExport()
    {
        var project = new MechWorkspace("New", "");
        project.AddXml("1.7/Defs/New.xml", "<Defs/>");
        Assert.True(project.IsDirty);
        project.MarkAllSaved();
        Assert.False(project.IsDirty);
        project.AddXml("1.7/Defs/Another.xml", "<Defs/>");
        Assert.True(project.IsDirty);
    }

    [Fact]
    public void ImportedProjectFilesStartClean()
    {
        var project = new MechWorkspace("Imported", "folder");
        project.AddXml("1.7/Defs/Body.xml", "<Defs/>", fromDisk: true);
        Assert.False(project.IsDirty);
    }

    [Fact]
    public void CrossFileIndexRefreshesAfterUndoAndRedo()
    {
        var project = Create();
        var file = project.Files[0];
        Assert.NotNull(project.FindDefinitionFile("ThingDef", "MP_Manta"));
        var field = file.Document.Fields(file.Document.Definitions().Single()).Single(x => x.Name == "defName");
        file.Document.SetField(field, "MP_Manta2");
        Assert.Null(project.FindDefinitionFile("ThingDef", "MP_Manta"));
        Assert.NotNull(project.FindDefinitionFile("ThingDef", "MP_Manta2"));
        Assert.True(file.Document.Undo());
        Assert.NotNull(project.FindDefinitionFile("ThingDef", "MP_Manta"));
        Assert.True(file.Document.Redo());
        Assert.Null(project.FindDefinitionFile("ThingDef", "MP_Manta"));
    }

    [Fact]
    public void RoundTripDoesNotDropUnknownXml()
    {
        var project = new MechWorkspace("Custom", ".");
        const string xml = "<Defs><!--keep--><ThingDef MayRequire=\"Custom.Mod\"><defName>MP_X</defName><custom Class=\"External.X\">data</custom></ThingDef></Defs>";
        var file = project.AddXml("Defs/Custom.xml", xml);
        Assert.Equal(xml, file.Document.Export());
    }
}
