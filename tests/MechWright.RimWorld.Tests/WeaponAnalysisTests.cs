using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class WeaponAnalysisTests
{
    private const string Sample = """
        <Defs>
        <ThingDef><defName>MP_NeedleGun</defName>
          <statBases><RangedWeapon_Cooldown>2.3</RangedWeapon_Cooldown></statBases>
          <verbs><li><verbClass>Verb_Shoot</verbClass><defaultProjectile>MP_Needle</defaultProjectile>
          <warmupTime>0.8</warmupTime><range>22.9</range><burstShotCount>3</burstShotCount>
          <ticksBetweenBurstShots>10</ticksBetweenBurstShots></li></verbs>
        </ThingDef>
        <ThingDef><defName>MP_Needle</defName><projectile><damageAmountBase>7</damageAmountBase></projectile></ThingDef>
        </Defs>
        """;

    [Fact]
    public void ComputesBasicTimeComponentsAndTheoreticalDps()
    {
        var doc = DefDocument.Parse(Sample);
        var profile = Assert.Single(WeaponAnalysis.Inspect(doc, doc.Definitions().First()));
        Assert.Equal(22.9, profile.Range);
        Assert.Equal(3, profile.BurstShots);
        Assert.Equal(7, profile.ShotDamage);
        Assert.Equal(20.0 / 60.0, profile.BurstDuration!.Value, 6);
        Assert.Equal(21 / (0.8 + 2.3 + 20d / 60), profile.TheoreticalDps!.Value, 6);
        Assert.Equal(100, profile.WarmupPercentage + profile.BurstPercentage + profile.CooldownPercentage, 5);
    }

    [Fact]
    public void UnresolvedProjectileOrInheritedCooldownDoesNotInventDps()
    {
        const string xml = "<Defs><ThingDef><defName>MP_UnknownGun</defName><verbs><li><defaultProjectile>ExternalBullet</defaultProjectile><warmupTime>1</warmupTime></li></verbs></ThingDef></Defs>";
        var doc = DefDocument.Parse(xml);
        var profile = Assert.Single(WeaponAnalysis.Inspect(doc, doc.Definitions().Single()));
        Assert.Null(profile.TheoreticalDps);
        Assert.Null(profile.ShotDamage);
        Assert.Null(profile.CycleSeconds);
    }

    [Fact]
    public void NonWeaponDefinitionReturnsNoProfile()
    {
        var doc = DefDocument.Parse(Sample);
        Assert.Empty(WeaponAnalysis.Inspect(doc, doc.Definitions().Last()));
    }
}
