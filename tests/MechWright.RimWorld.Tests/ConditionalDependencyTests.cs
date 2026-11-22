using MechWright.Core;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class ConditionalDependencyTests
{
    [Fact]
    public void ConditionalReferencesAreNotMisdiagnosedAsRequiredMissingDefs()
    {
        var project = new MechWorkspace("Optional refs", "");
        project.AddXml("Defs/Weapons.xml", """
            <Defs>
              <ThingDef><defName>MP_Weapon</defName>
                <verbs><li><defaultProjectile>MP_OptionalBullet</defaultProjectile></li></verbs>
              </ThingDef>
              <ThingDef MayRequire="Ludeon.RimWorld.Biotech">
                <defName>MP_OptionalBullet</defName>
              </ThingDef>
              <ThingDef MayRequireAnyOf="Ludeon.RimWorld.Biotech,ThirdParty.Mod">
                <defName>MP_OptionalWeapon</defName>
                <verbs><li><defaultProjectile>MP_OnlyFromAnotherMod</defaultProjectile></li></verbs>
              </ThingDef>
            </Defs>
            """);
        var availableGameDefs = Array.Empty<CatalogEntry>();
        var warnings = project.Validate(availableGameDefs);
        var gate = Assert.Single(warnings.Where(x => x.Issue.Code == "MW041"));
        Assert.Contains("MP_OptionalBullet", gate.Issue.Message);
        Assert.Equal("/Defs[1]/ThingDef[1]/verbs[1]/li[1]/defaultProjectile[1]",
            gate.Issue.Location);
        Assert.DoesNotContain(warnings, x => x.Issue.Code == "MW021");
        Assert.Equal(2, project.GetReferences(project.Files[0])
            .Count(x => x.Reference.TargetType == "ThingDef"));
        Assert.Contains(project.GetReferences(project.Files[0]), x =>
            x.Reference.TargetDefName == "MP_OnlyFromAnotherMod" &&
            x.Reference.IsConditional);
    }

    [Fact]
    public void OptionalMissingDlcIsInformationalAndAnyOfMayBeSatisfied()
    {
        var project = new MechWorkspace("DLC gates", "");
        project.AddXml("Defs/Dlc.xml", """
            <Defs>
              <ThingDef MayRequire="Ludeon.RimWorld.Biotech">
                <defName>MP_Biotech</defName>
              </ThingDef>
              <ThingDef MayRequireAnyOf="Ludeon.RimWorld.Royalty,Ludeon.RimWorld.Biotech">
                <defName>MP_Either</defName>
              </ThingDef>
              <ThingDef MayRequireAnyOf="Ludeon.RimWorld.Biotech,Optional.Mod">
                <defName>MP_OtherModCouldEnable</defName>
              </ThingDef>
              <ThingDef MayRequireAnyOf="Ludeon.RimWorld.Biotech,Ludeon.RimWorld.Anomaly">
                <defName>MP_BothAbsent</defName>
              </ThingDef>
            </Defs>
            """);
        DefCatalog.PackageScan[] packages =
        [
            new("Core", true, 100),
            new("Royalty", true, 30),
            new("Biotech", false, 0),
            new("Anomaly", false, 0)
        ];
        Assert.DoesNotContain(project.Validate(), x => x.Issue.Code == "MW040");
        var report = project.Validate(gamePackages: packages);
        var info = report.Where(x => x.Issue.Code == "MW040").ToArray();
        Assert.Equal(2, info.Length);
        Assert.All(info, x => Assert.Equal(Severity.Information, x.Issue.Severity));
        Assert.All(info, x => Assert.Equal("Defs/Dlc.xml", x.FilePath));
        Assert.Contains(info, x => x.Issue.Message.Contains("Biotech"));
        Assert.Contains(info, x => x.Issue.Message.Contains("Anomaly"));
        Assert.DoesNotContain(info, x => x.Issue.Message.Contains("Royalty"));
    }

    [Fact]
    public void MayRequireAnyOfOnNestedItemSuppressesFalseDefMismatch()
    {
        var project = new MechWorkspace("Nested", "");
        project.AddXml("Defs/Conditional.xml", """
            <Defs>
              <ThingDef><defName>MP_Base</defName>
                <verbs><li MayRequireAnyOf="Ludeon.RimWorld.Biotech,Other.Mod">
                  <defaultProjectile>MP_OptionalProjectile</defaultProjectile>
                </li></verbs>
              </ThingDef>
              <BodyDef><defName>MP_OptionalProjectile</defName>
                <corePart><def>MechanicalThorax</def></corePart>
              </BodyDef>
            </Defs>
            """);
        Assert.DoesNotContain(project.Validate(), x => x.Issue.Code == "MW022");
    }
}
