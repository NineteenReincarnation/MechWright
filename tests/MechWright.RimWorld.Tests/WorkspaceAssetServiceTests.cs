using MechWright.Infrastructure;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class WorkspaceAssetServiceTests
{
    [Fact]
    public async Task ImportedAssetsAreCopiedOnlyWhenExplicitlyEnabled()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightAssetTests", Guid.NewGuid().ToString("N"));
        var mod = Path.Combine(root, "Mod");
        try
        {
            Directory.CreateDirectory(Path.Combine(mod, "Defs"));
            Directory.CreateDirectory(Path.Combine(mod, "Textures", "Things", "Mechanoids"));
            Directory.CreateDirectory(Path.Combine(mod, "Sounds"));
            File.WriteAllText(Path.Combine(mod, "Defs", "Body.xml"), "<Defs/>");
            byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10, 11, 22, 33];
            File.WriteAllBytes(Path.Combine(mod, "Textures", "Things", "Mechanoids", "MP_Manta.png"), bytes);
            File.WriteAllBytes(Path.Combine(mod, "Sounds", "Manta.ogg"), [12, 13, 14]);
            File.WriteAllText(Path.Combine(mod, "Sounds", "private.txt"), "do not distribute");
            var project = await WorkspaceDiskService.ImportAsync(mod);
            Assert.Equal(2, project.Assets.Count);
            Assert.False(project.IsDirty);
            await WorkspaceDiskService.ExportAsync(project, Path.Combine(root, "xml-only"),
                "Mod", "local.me.manta");
            Assert.False(File.Exists(Path.Combine(root, "xml-only", "Textures",
                "Things", "Mechanoids", "MP_Manta.png")));
            await WorkspaceDiskService.ExportAsync(project, Path.Combine(root, "with-assets"),
                "Mod", "local.me.manta", includeAssets: true);
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(root, "with-assets",
                "Textures", "Things", "Mechanoids", "MP_Manta.png")));
            Assert.True(File.Exists(Path.Combine(root, "with-assets", "Sounds", "Manta.ogg")));
            Assert.False(File.Exists(Path.Combine(root, "with-assets", "Sounds", "private.txt")));
            Assert.True(File.Exists(Path.Combine(root, "with-assets", "About", "About.xml")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task UserCanRegisterExternalPNGAndExportItWithoutChangingOriginal()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightAssetTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var input = Path.Combine(root, "asset.png");
            byte[] bytes = [1, 2, 3, 4];
            File.WriteAllBytes(input, bytes);
            var project = new MechWorkspace("Test", "");
            project.AddXml("1.7/Defs/NewMech.xml", "<Defs/>");
            WorkspaceAssetService.Register(project, input, "Textures/Things/MP_NewMech_south.png");
            Assert.True(project.IsDirty);
            await WorkspaceDiskService.ExportAsync(project, Path.Combine(root, "output"),
                "Test", "local.me.test", includeAssets: true);
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(root, "output",
                "Textures", "Things", "MP_NewMech_south.png")));
            Assert.Equal(bytes, File.ReadAllBytes(input));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MissingSourceAbortsExportBeforePublishing()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightAssetTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var project = new MechWorkspace("Test", "");
            project.AddXml("1.7/Defs/Mech.xml", "<Defs/>");
            var png = Path.Combine(root, "gone.png");
            File.WriteAllText(png, "file");
            WorkspaceAssetService.Register(project, png, "Textures/Mech.png");
            File.Delete(png);
            var output = Path.Combine(root, "output");
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                WorkspaceDiskService.ExportAsync(project, output, "Test",
                    "local.me.test", includeAssets: true));
            Assert.False(Directory.Exists(output));
            Assert.False(Directory.EnumerateDirectories(root, ".mechwright-*").Any());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
