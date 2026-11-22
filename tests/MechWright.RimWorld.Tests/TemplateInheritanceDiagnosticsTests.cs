using MechWright.Core;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class TemplateInheritanceDiagnosticsTests
{
    [Fact]
    public void LocalTemplateCyclesAndDuplicatesAreReportedWithExactXmlPaths()
    {
        var project = new MechWorkspace("Parent graph", "");
        project.AddXml("Defs/Parents.xml", """
            <Defs>
              <ThingDef Name="MP_BaseA" ParentName="MP_BaseB" Abstract="True" />
              <ThingDef Name="MP_BaseB" ParentName="MP_BaseA" Abstract="True" />
              <ThingDef ParentName="BaseMechanoidWalker">
                <defName>MP_Valid</defName>
              </ThingDef>
            </Defs>
            """);
        var diagnostics = project.Validate();
        var cycles = diagnostics.Where(x => x.Issue.Code == "MW032").ToArray();
        Assert.Equal(2, cycles.Length);
        Assert.All(cycles, x =>
        {
            Assert.Equal(Severity.Warning, x.Issue.Severity);
            Assert.StartsWith("/Defs[1]/ThingDef[", x.Issue.Location);
            Assert.Equal("Defs/Parents.xml", x.FilePath);
        });
        // Without authoritative game template data, BaseMechanoidWalker
        // is unknown rather than definitely missing.
        Assert.DoesNotContain(diagnostics, x => x.Issue.Code == "MW030");

        project.AddXml("Defs/Duplicate.xml",
            "<Defs><ThingDef Name='MP_BaseA' Abstract='True'/></Defs>");
        Assert.Equal(2, project.Validate().Count(x => x.Issue.Code == "MW031"));
    }

    [Fact]
    public void InstalledGameTemplateIndexDistinguishesUnknownAndConditionalParents()
    {
        var project = new MechWorkspace("Parent scan", "");
        project.AddXml("Defs/Units.xml", """
            <Defs>
              <ThingDef ParentName="BaseMechanoidWalker"><defName>MP_Valid</defName></ThingDef>
              <ThingDef ParentName="MP_UnknownBase"><defName>MP_Unknown</defName></ThingDef>
              <ThingDef ParentName="MP_OptionalBase" MayRequire="Some.Optional.Mod">
                <defName>MP_Optional</defName>
              </ThingDef>
            </Defs>
            """);
        DefCatalog.ParentTemplate[] scanned =
            [new("ThingDef", "BaseMechanoidWalker", "Core")];

        Assert.DoesNotContain(project.Validate(), x => x.Issue.Code == "MW030");
        var warnings = project.Validate(gameTemplates: scanned)
            .Where(x => x.Issue.Code == "MW030").ToArray();
        var warning = Assert.Single(warnings);
        Assert.Contains("MP_UnknownBase", warning.Issue.Message);
        Assert.Equal("/Defs[1]/ThingDef[2]", warning.Issue.Location);
        Assert.Equal(Severity.Warning, warning.Issue.Severity);
    }
}
