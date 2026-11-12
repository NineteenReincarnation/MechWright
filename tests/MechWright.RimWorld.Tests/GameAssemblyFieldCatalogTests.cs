using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests
{

public sealed class GameAssemblyFieldCatalogTests
{
    [Fact]
    public void ReadsFieldsAndEnumsFromPeMetadataWithoutExecutingAnyAssemblyCode()
    {
        var catalog = GameAssemblyFieldCatalog.Read(typeof(FixtureModels.ThingDef).Assembly.Location);
        var fields = catalog.For("ThingDef");
        Assert.Contains(fields, x => x.Path == "description" && x.Kind == "Text");
        Assert.Contains(fields, x => x.Path == "customFloat" && x.Kind == "Number");
        Assert.Contains(fields, x => x.Path == "race/needsRest" && x.Kind == "Boolean");
        Assert.Contains(fields, x => x.Path == "race/baseBodySize" && x.Kind == "Number");
        var weight = Assert.Single(fields.Where(x => x.Path == "race/mechWeightClass"));
        Assert.Equal("Enum", weight.Kind);
        Assert.Contains("Heavy", weight.Choices);
        Assert.DoesNotContain(fields, x => x.Path == "staticTag");
        Assert.DoesNotContain(fields, x => x.Path == "defName");
        Assert.DoesNotContain(fields, x => x.Path == "tools");
        Assert.Contains(catalog.For("PawnKindDef"), x =>
            x.Path == "combatPower" && x.Kind == "Number");
    }

    [Fact]
    public void RejectsNonAssembliesWithoutAttemptingToLoadThem()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll");
        try
        {
            File.WriteAllBytes(file, new byte[2048]);
            Assert.Throws<BadImageFormatException>(() =>
                GameAssemblyFieldCatalog.Read(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task FindsDllMetadataAlongsideInstalledCoreXmlInSingleScan()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWright_Metadata_" +
            Guid.NewGuid().ToString("N"));
        var defs = Path.Combine(root, "Data", "Core", "Defs");
        var managed = Path.Combine(root, "RimWorldWin64_Data", "Managed");
        Directory.CreateDirectory(defs);
        Directory.CreateDirectory(managed);
        try
        {
            File.WriteAllText(Path.Combine(defs, "Fixture.xml"),
                "<Defs><ThingDef><defName>Fixture</defName><statBases>" +
                "<MoveSpeed>4.5</MoveSpeed></statBases></ThingDef></Defs>");
            File.Copy(typeof(FixtureModels.ThingDef).Assembly.Location,
                Path.Combine(managed, "Assembly-CSharp.dll"));
            var scan = await DefCatalog.ScanFullAsync(root);
            Assert.True(scan.IsGameInstallation);
            Assert.Contains(scan.NativeFields.For("ThingDef"),
                x => x.Path == "statBases/MoveSpeed");
            Assert.Contains(scan.GameFields.For("ThingDef"),
                x => x.Path == "customFloat" && x.Kind == "Number");
            Assert.Contains(InspectorFieldCatalog.For("ThingDef", scan.NativeFields,
                scan.GameFields), x => x.Path == "customFloat" &&
                x.IsAssemblyCandidate && !x.IsObserved);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void InspectorMarksClrOnlyCandidatesAndKeepsBuiltInPriority()
    {
        var metadata = GameAssemblyFieldCatalog.Read(typeof(FixtureModels.ThingDef).Assembly.Location);
        var fields = InspectorFieldCatalog.For("ThingDef", NativeFieldCatalog.Empty, metadata);
        Assert.Contains(fields, x => x.Path == "customFloat" &&
            x.Kind == "Number" && x.SourceLabel.Contains("候选"));
        Assert.Single(fields.Where(x => x.Path == "race/baseBodySize"));
        var known = fields.Single(x => x.Path == "race/baseBodySize");
        Assert.True(known.IsStarter);
        Assert.Contains(fields, x => x.Path == "race/mechWeightClass" &&
            x.Kind == "Enum" && x.Choices.Contains("Heavy"));
        Assert.DoesNotContain(fields, x => x.Path == "combatPower");
    }
}
}

namespace MechWright.RimWorld.Tests.FixtureModels
{
    public class FakeDef
    {
        public string description = "";
        public string defName = "";
        private float privateField;
    }

    public enum WeightClass { Light, Medium, Heavy }

    public sealed class RaceProperties
    {
        public float baseBodySize;
        public bool needsRest;
        public WeightClass mechWeightClass;
        public List<string> tools = [];
    }

    public sealed class ThingDef : FakeDef
    {
        public static string staticTag = "";
        public float customFloat;
        public RaceProperties race = new();
        public string label = "";
    }

    public sealed class PawnKindDef : FakeDef
    {
        public float combatPower;
    }
}
