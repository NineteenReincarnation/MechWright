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
}
