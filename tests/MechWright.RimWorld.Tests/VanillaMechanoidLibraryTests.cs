using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class VanillaMechanoidLibraryTests
{
    [Fact]
    public async Task LoadsCoreAndDlcAndLinksMechKindsBodiesWeaponsAndRecipes()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightVanillaTests", Guid.NewGuid().ToString("N"));
        try
        {
            var core = Path.Combine(root, "Data", "Core", "Defs");
            var biotech = Path.Combine(root, "Data", "Biotech", "Defs");
            Directory.CreateDirectory(core);
            Directory.CreateDirectory(biotech);
            File.WriteAllText(Path.Combine(core, "Base.xml"), """
                <Defs>
                  <ThingDef Name="BaseMechanoid" Abstract="True">
                    <race><thinkTreeMain>Mechanoid</thinkTreeMain><baseBodySize>1</baseBodySize></race>
                    <statBases><MoveSpeed>4.7</MoveSpeed><ArmorRating_Sharp>0.4</ArmorRating_Sharp></statBases>
                  </ThingDef>
                  <PawnKindDef Name="BaseMechanoidKind" Abstract="True">
                    <weaponMoney>9999~9999</weaponMoney>
                  </PawnKindDef>
                  <BodyDef><defName>MechanicalBody</defName><corePart><def>MechanicalThorax</def>
                    <parts><li><def>ArtificialBrain</def><coverage>0.1</coverage>
                    <depth>Inside</depth></li></parts></corePart></BodyDef>
                  <ThingDef><defName>Mech_Gun</defName><label>charged gun</label>
                    <weaponTags><li>TestMechGun</li></weaponTags>
                    <verbs><li><defaultProjectile>Mech_Bullet</defaultProjectile><range>24</range></li></verbs>
                  </ThingDef>
                  <ThingDef><defName>Mech_Bullet</defName><projectile>
                    <damageAmountBase>12</damageAmountBase><armorPenetrationBase>0.25</armorPenetrationBase>
                  </projectile></ThingDef>
                </Defs>
                """);
            File.WriteAllText(Path.Combine(biotech, "Units.xml"), """
                <Defs>
                  <ThingDef ParentName="BaseMechanoid"><defName>Mech_Test</defName>
                    <label>mechanical test unit</label>
                    <statBases><MoveSpeed>3.9</MoveSpeed></statBases>
                    <race><body>MechanicalBody</body><mechWeightClass>Medium</mechWeightClass></race>
                  </ThingDef>
                  <PawnKindDef ParentName="BaseMechanoidKind">
                    <defName>Mech_Test</defName><race>Mech_Test</race>
                    <combatPower>400</combatPower><weaponTags><li>TestMechGun</li></weaponTags>
                  </PawnKindDef>
                  <RecipeDef><defName>TestGestate</defName><products><Mech_Test>1</Mech_Test></products></RecipeDef>
                </Defs>
                """);
            var scan = await DefCatalog.ScanFullAsync(root);
            Assert.True(scan.IsGameInstallation);
            Assert.True(scan.Packages.Single(p => p.Name == "Core").Present);
            Assert.True(scan.Packages.Single(p => p.Name == "Biotech").Present);
            Assert.False(scan.Packages.Single(p => p.Name == "Anomaly").Present);
            var mech = Assert.Single(scan.Mechanoids.Entries);
            Assert.Equal("Mech_Test", mech.DefName);
            Assert.Equal("Biotech", mech.Package);
            Assert.Equal("Medium", mech.WeightClass);
            Assert.Equal("3.9", mech.Fields.Single(f => f.XmlPath == "statBases/MoveSpeed").Value);
            var inherited = mech.Fields.Single(f => f.XmlPath == "statBases/ArmorRating_Sharp");
            Assert.Equal("0.4", inherited.Value);
            Assert.Equal("BaseMechanoid", inherited.SourceTemplate);
            Assert.Equal("400", mech.Fields.Single(f => f.XmlPath == "combatPower").Value);
            Assert.Equal(2, mech.BodyParts.Count);
            var weapon = Assert.Single(mech.WeaponCandidates);
            Assert.Contains("射程 24", weapon.Description);
            Assert.Contains("基础伤害 12", weapon.Description);
            Assert.Contains("穿甲 0.25", weapon.Description);
            Assert.Single(mech.Recipes);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AnArbitraryModFolderDoesNotPretendToBeVanilla()
    {
        var root = Path.Combine(Path.GetTempPath(), "MechWrightVanillaTests", Guid.NewGuid().ToString("N"));
        try
        {
            var defs = Path.Combine(root, "Defs");
            Directory.CreateDirectory(defs);
            File.WriteAllText(Path.Combine(defs, "Mech.xml"),
                "<Defs><ThingDef ParentName=\"BaseMechanoid\"><defName>CustomModMech</defName></ThingDef></Defs>");
            var scan = await DefCatalog.ScanFullAsync(root);
            Assert.False(scan.IsGameInstallation);
            Assert.Empty(scan.Mechanoids.Entries);
            Assert.Single(scan.Entries);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ParentNameCycleDoesNotLoopForever()
    {
        static VanillaMechanoidLibrary.SourceDef S(string template, string parent, string xml) =>
            new("ThingDef", null, template, parent, "Core", "Core.xml",
                System.Xml.Linq.XElement.Parse(xml));
        var inputs = new[]
        {
            S("BaseMechanoid", "Loop", "<ThingDef Name='BaseMechanoid' ParentName='Loop' Abstract='True'><race><thinkTreeMain>Mechanoid</thinkTreeMain></race></ThingDef>"),
            S("Loop", "BaseMechanoid", "<ThingDef Name='Loop' ParentName='BaseMechanoid' Abstract='True' />"),
            new VanillaMechanoidLibrary.SourceDef("ThingDef", "Mech_Test", null,
                "BaseMechanoid", "Core", "Race.xml",
                System.Xml.Linq.XElement.Parse("<ThingDef ParentName='BaseMechanoid'><defName>Mech_Test</defName></ThingDef>"))
        };
        var result = VanillaMechanoidLibrary.Build(inputs);
        Assert.Single(result.Entries);
    }
}
