using System.IO.Compression;
using MechWright.Infrastructure;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class DirectionalTextureArchiveTests
{
    [Fact]
    public void InspectGroupsSpaceAndUnderscoreNamedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        try
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Add(zip, "PNG/BirgusAncient/BirgusAncient south.png", [1, 2, 3]);
                Add(zip, "PNG/BirgusAncient/BirgusAncient north.png", [4, 5]);
                Add(zip, "PNG/BirgusAncient/BirgusAncient east.png", [6, 7]);
                Add(zip, "Other/MP_Manta_south.png", [8]);
                Add(zip, "Other/MP_Manta_north.png", [9]);
                Add(zip, "Other/MP_Manta_east.png", [10]);
                Add(zip, "Incomplete/Single east.png", [11]);
            }
            var groups = DirectionalTextureArchive.Inspect(path);
            Assert.Equal(2, groups.Count);
            Assert.Contains(groups, x => x.Name == "BirgusAncient");
            Assert.Contains(groups, x => x.Name == "MP_Manta");
            Assert.DoesNotContain(groups, x => x.Name == "Single");

            var selected = groups.Single(x => x.Name == "BirgusAncient");
            var extracted = DirectionalTextureArchive.Extract(path, selected);
            Assert.Equal(3, extracted.Count);
            Assert.All(extracted, x => Assert.True(File.Exists(x)));
            Assert.Contains(extracted, x => Path.GetFileName(x) == "BirgusAncient_south.png");
            Assert.Equal(new byte[] { 1, 2, 3 },
                File.ReadAllBytes(extracted.Single(x => x.EndsWith("_south.png"))));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DuplicateDirectionIsNotSilentlyChosen()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        try
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Add(zip, "A/Unit south.png", [1]);
                Add(zip, "B/Unit south.png", [2]);
                Add(zip, "A/Unit north.png", [3]);
                Add(zip, "A/Unit east.png", [4]);
            }
            Assert.Empty(DirectionalTextureArchive.Inspect(path));
        }
        finally { File.Delete(path); }
    }

    private static void Add(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(data);
    }
}
