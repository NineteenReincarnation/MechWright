using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class DefValidationNavigationTests
{
    [Fact]
    public void MixedDefTypesAndNestedBodyPartsUseActualDocumentPaths()
    {
        const string xml = """
            <Defs>
              <ThingDef><defName>MP_Duplicate</defName></ThingDef>
              <BodyDef><defName>MP_Structure</defName>
                <corePart><def>MP_Core</def>
                  <parts><li><def>MP_Left</def><coverage>1.5</coverage></li></parts>
                </corePart>
              </BodyDef>
              <ThingDef><defName>MP_Duplicate</defName></ThingDef>
            </Defs>
            """;
        var document = DefDocument.Parse(xml);
        var results = document.Validate();
        Assert.Equal(
            ["/Defs[1]/ThingDef[1]", "/Defs[1]/ThingDef[2]"],
            results.Where(x => x.Code == "MW002").Select(x => x.Location).ToArray());
        Assert.Contains(results, x => x.Code == "MW005" &&
            x.Location == "/Defs[1]/BodyDef[1]/corePart[1]/parts[1]/li[1]/coverage[1]");
        Assert.Equal(xml, document.Export());
        Assert.False(document.IsDirty);
    }
}
