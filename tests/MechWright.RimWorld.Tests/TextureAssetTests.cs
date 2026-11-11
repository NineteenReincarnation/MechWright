using MechWright.Infrastructure;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class TextureAssetTests
{
    [Fact]
    public void FindsThreeDirectionalTexturesWithoutInventingWest()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightGraphicTests", Guid.NewGuid().ToString("N"));
        try
        {
            var dir = Path.Combine(root, "1.7", "Textures", "Things", "Pawn", "Manta");
            Directory.CreateDirectory(dir);
            foreach (var d in new[] { "north", "south", "east" })
                File.WriteAllBytes(Path.Combine(dir, "Manta_" + d + ".png"), [137, 80, 78, 71]);

            var doc = DefDocument.Parse(
                "<Defs><PawnKindDef><defName>MP_Manta</defName><lifeStages><li><bodyGraphicData>" +
                "<texPath>Things/Pawn/Manta/Manta</texPath><graphicClass>Graphic_Multi</graphicClass>" +
                "<drawSize>2.8</drawSize></bodyGraphicData></li></lifeStages></PawnKindDef></Defs>");
            var descriptor = Assert.Single(GraphicReferences.Inspect(doc, doc.Definitions().Single()));
            Assert.Equal("2.8", descriptor.DrawSize);
            var files = TextureAssetLocator.Resolve(root, descriptor);
            Assert.Equal(4, files.Count);
            Assert.Equal(3, files.Count(f => f.Exists));
            Assert.False(files.Single(f => f.Direction == "west").Exists);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void NewlyRegisteredTextureShowsUpBeforeProjectExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightGraphicTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var local = Path.Combine(root, "Unit_east.png");
            File.WriteAllBytes(local, [137, 80, 78, 71]);
            var workspace = new MechWorkspace("New", "");
            workspace.AddAsset("Textures/Things/Pawn/Unit_east.png", local, 4);
            var descriptor = new GraphicReferences.GraphicDescriptor("",
                "Things/Pawn/Unit", "Graphic_Multi", "1.5", true);
            var candidates = TextureAssetLocator.ResolveWorkspace(workspace, descriptor);
            Assert.Single(candidates.Where(x => x.Exists));
            Assert.Equal(local, candidates.Single(x => x.Direction == "east").LocalPath);
            Assert.False(candidates.Single(x => x.Direction == "north").Exists);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void RejectsTexturePathTraversal()
    {
        var descriptor = new GraphicReferences.GraphicDescriptor("/", "../out/file",
            "Graphic_Single", "1", true);
        var result = Assert.Single(TextureAssetLocator.Resolve("/tmp", descriptor));
        Assert.False(result.Exists);
        Assert.Null(result.LocalPath);
    }

    [Fact]
    public void UnspecifiedGraphicClassUsesSingleTextureWithoutGuesswork()
    {
        var doc = DefDocument.Parse(
            "<Defs><ThingDef><defName>MP_X</defName><graphicData><texPath>Things/Pawn/X</texPath></graphicData></ThingDef></Defs>");
        var descriptor = Assert.Single(GraphicReferences.Inspect(doc, doc.Definitions().Single()));
        Assert.False(descriptor.ExplicitGraphicClass);
        var item = Assert.Single(TextureAssetLocator.Resolve(null, descriptor));
        Assert.Equal("single", item.Direction);
        Assert.False(item.Exists);
    }
}
