using MechWright.Core;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class DefinitionReferenceTests
{
    [Fact]
    public void ResolvesLocalBodyAndProjectileAndExternalVanillaPart()
    {
        const string xml = """
            <Defs>
            <ThingDef><defName>MP_X</defName><race><body>MP_Body</body></race>
              <verbs><li><defaultProjectile>MP_Bullet</defaultProjectile></li></verbs></ThingDef>
            <ThingDef><defName>MP_Bullet</defName></ThingDef>
            <BodyDef><defName>MP_Body</defName><corePart><def>MechanicalThorax</def></corePart></BodyDef>
            </Defs>
            """;
        var doc = DefDocument.Parse(xml);
        var refs = DefinitionReferences.Collect(doc);
        Assert.Contains(refs, x => x.TargetDefName == "MP_Body" &&
                                   x.Location == ReferenceLocation.CurrentDocument);
        Assert.Contains(refs, x => x.TargetDefName == "MP_Bullet" &&
                                   x.Location == ReferenceLocation.CurrentDocument);
        Assert.Contains(refs, x => x.TargetDefName == "MechanicalThorax" &&
                                   x.Location == ReferenceLocation.ExternalUnknown);
    }

    [Fact]
    public void CatalogResolvesExternalReferencesWithoutTreatingThemAsErrors()
    {
        const string xml = "<Defs><BodyDef><defName>MP_B</defName><corePart><def>MechanicalThorax</def></corePart></BodyDef></Defs>";
        var doc = DefDocument.Parse(xml);
        CatalogEntry[] catalog =
        [
            new(new DefinitionIdentity("BodyPartDef", "MechanicalThorax", "Core.xml"),
                "thorax", "Core.xml", "Core")
        ];
        var references = DefinitionReferences.Collect(doc, catalog);
        var found = Assert.Single(references);
        Assert.Equal(ReferenceLocation.GameCatalog, found.Location);
        Assert.Equal("Core", found.OriginPackage);
    }

    [Fact]
    public void RecipeProductsUseElementNameRatherThanProductQuantity()
    {
        const string xml = "<Defs><RecipeDef><defName>MP_Gestate</defName><products><MP_Mech>1</MP_Mech></products></RecipeDef></Defs>";
        var reference = Assert.Single(DefinitionReferences.Collect(DefDocument.Parse(xml)));
        Assert.Equal("MP_Mech", reference.TargetDefName);
        Assert.Equal("ThingDef", reference.TargetType);
    }

    [Fact]
    public void CustomTextIsNeverGuessedAsDefinitionReference()
    {
        const string xml = "<Defs><ThingDef><defName>MP_X</defName><unrelatedField>MP_Anything</unrelatedField></ThingDef></Defs>";
        Assert.Empty(DefinitionReferences.Collect(DefDocument.Parse(xml)));
    }
    [Fact]
    public void ExplicitNativeReferencePathsCoverProjectileResearchRecipesAndFaction()
    {
        const string xml = """
            <Defs>
              <ThingDef>
                <defName>MP_Weapon</defName>
                <projectile><damageDef>MP_Plasma</damageDef></projectile>
                <recipeMaker>
                  <researchPrerequisite>MP_ChargeTech</researchPrerequisite>
                  <recipeUsers><li>MP_Bench</li></recipeUsers>
                </recipeMaker>
                <costList><Steel>80</Steel></costList>
                <thingCategories><li>WeaponsRanged</li></thingCategories>
                <tools><li><capacities><li>Blunt</li></capacities></li></tools>
                <verbs><li><defaultProjectile>MP_Bolt</defaultProjectile>
                  <soundCast>MP_CastSound</soundCast></li></verbs>
                <weaponTags><li>MechanoidGunMedium</li></weaponTags>
                <tradeTags><li>RangedWeapon</li></tradeTags>
              </ThingDef>
              <RecipeDef><defName>MP_Gestate</defName>
                <ingredients><li><filter><thingDefs><li>MP_Steel</li></thingDefs></filter></li></ingredients>
                <fixedIngredientFilter><categories><li>MP_Materials</li></categories></fixedIngredientFilter>
                <products><MP_Mech>1</MP_Mech></products>
                <researchPrerequisite>MP_GestationTech</researchPrerequisite>
              </RecipeDef>
              <FactionDef><defName>MP_Faction</defName>
                <pawnGroupMakers><li><kindDef>Combat</kindDef>
                  <options><li><kind>MP_Scyther</kind></li></options></li></pawnGroupMakers>
              </FactionDef>
              <DamageDef><defName>MP_Plasma</defName><hediff>MP_Burn</hediff></DamageDef>
              <ThingDef><defName>MP_Mech</defName></ThingDef>
            </Defs>
            """;
        var references = DefinitionReferences.Collect(DefDocument.Parse(xml));
        Assert.Contains(references, r => r.TargetType == "DamageDef" &&
            r.TargetDefName == "MP_Plasma");
        Assert.Contains(references, r => r.TargetType == "ResearchProjectDef" &&
            r.TargetDefName == "MP_ChargeTech");
        Assert.Contains(references, r => r.TargetType == "ResearchProjectDef" &&
            r.TargetDefName == "MP_GestationTech");
        Assert.Contains(references, r => r.TargetType == "ThingDef" &&
            r.TargetDefName == "MP_Bench");
        Assert.Contains(references, r => r.TargetType == "ThingDef" &&
            r.TargetDefName == "Steel");
        Assert.Contains(references, r => r.TargetType == "ThingCategoryDef" &&
            r.TargetDefName == "WeaponsRanged");
        Assert.Contains(references, r => r.TargetType == "ThingDef" &&
            r.TargetDefName == "MP_Steel");
        Assert.Contains(references, r => r.TargetType == "ThingCategoryDef" &&
            r.TargetDefName == "MP_Materials");
        Assert.Contains(references, r => r.TargetType == "PawnKindDef" &&
            r.TargetDefName == "MP_Scyther");
        Assert.Contains(references, r => r.TargetType == "PawnGroupKindDef" &&
            r.TargetDefName == "Combat");
        Assert.Contains(references, r => r.TargetType == "HediffDef" &&
            r.TargetDefName == "MP_Burn");
        Assert.Contains(references, r => r.TargetType == "ThingDef" &&
            r.TargetDefName == "MP_Mech" &&
            r.Location == ReferenceLocation.CurrentDocument);
        Assert.DoesNotContain(references, r => r.TargetDefName is
            "MechanoidGunMedium" or "RangedWeapon");
        Assert.All(references, r => Assert.StartsWith("/Defs[1]/", r.FieldPath));
    }

    [Fact]
    public void ConditionalScopeIncludesNestedMayRequireAndAnyOfWithoutMarkingTags()
    {
        const string xml = """
            <Defs><ThingDef MayRequireAnyOf="Ludeon.RimWorld.Biotech, Optional.Mod">
              <defName>MP_Conditional</defName>
              <verbs><li MayRequire="Optional.Weapons">
                <defaultProjectile>MP_ConditionalBullet</defaultProjectile>
              </li></verbs>
              <weaponTags><li>MP_ThisIsNotADef</li></weaponTags>
            </ThingDef></Defs>
            """;
        var found = Assert.Single(DefinitionReferences.Collect(DefDocument.Parse(xml)));
        Assert.True(found.IsConditional);
        Assert.Contains("Optional.Mod", found.ConditionalPackages);
        Assert.Contains("Optional.Weapons", found.ConditionalPackages);
        Assert.Contains("Ludeon.RimWorld.Biotech", found.ConditionalPackages);
        Assert.Equal("ThingDef", found.TargetType);
    }

}
