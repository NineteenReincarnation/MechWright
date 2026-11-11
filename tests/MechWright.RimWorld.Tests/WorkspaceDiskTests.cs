using MechWright.Infrastructure;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class WorkspaceDiskTests
{
    [Fact]
    public async Task ImportsMultipleDefFilesAndExportsStandardModLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var output = Path.Combine(root, "output");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "1.7", "Defs", "ThingDefs"));
            Directory.CreateDirectory(Path.Combine(source, "1.7", "Defs", "BodyDefs"));
            const string mech = "<Defs><!--keep--><ThingDef><defName>MP_Manta</defName><race><body>MP_Manta_Body</body></race></ThingDef></Defs>";
            await File.WriteAllTextAsync(Path.Combine(source, "1.7", "Defs", "ThingDefs", "Manta.xml"), mech);
            await File.WriteAllTextAsync(Path.Combine(source, "1.7", "Defs", "BodyDefs", "Body.xml"),
                "<Defs><BodyDef><defName>MP_Manta_Body</defName><corePart><def>MechanicalThorax</def></corePart></BodyDef></Defs>");

            var project = await WorkspaceDiskService.ImportAsync(source);
            Assert.Equal(2, project.Files.Count);
            var target = await WorkspaceDiskService.ExportAsync(project, output, "Manta Mod", "local.user.manta");
            Assert.True(File.Exists(Path.Combine(target, "About", "About.xml")));
            Assert.Equal(mech, await File.ReadAllTextAsync(
                Path.Combine(target, "1.7", "Defs", "ThingDefs", "Manta.xml")));
            Assert.Contains("<packageId>local.user.manta</packageId>",
                await File.ReadAllTextAsync(Path.Combine(target, "About", "About.xml")));
            Assert.False(project.IsDirty);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExportRefusesExistingDirectoryWithoutOverwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var output = Path.Combine(root, "existing");
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "note.txt"), "keep me");
            var workspace = new MechWorkspace("Test", root);
            workspace.AddXml("1.7/Defs/Foo.xml", "<Defs/>");
            await Assert.ThrowsAsync<IOException>(() =>
                WorkspaceDiskService.ExportAsync(workspace, output, "Test", "local.user.test"));
            Assert.Equal("keep me", await File.ReadAllTextAsync(Path.Combine(output, "note.txt")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InheritedUnknownAttributesSurviveProjectExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var workspace = new MechWorkspace("Test", root);
            workspace.AddXml("Defs/Tools.xml",
                "<Defs><ThingDef ParentName=\"Custom.A\"><defName>MP_Tool</defName><someMod Class=\"A.X\">z</someMod></ThingDef></Defs>");
            await WorkspaceDiskService.ExportAsync(workspace, Path.Combine(root, "result"),
                "Test", "local.user.test");
            var xml = await File.ReadAllTextAsync(Path.Combine(root, "result", "Defs", "Tools.xml"));
            Assert.Contains("ParentName=\"Custom.A\"", xml);
            Assert.Contains("Class=\"A.X\"", xml);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
