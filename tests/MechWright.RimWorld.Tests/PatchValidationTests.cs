using MechWright.Core;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class PatchValidationTests
{
    [Fact]
    public void PatchXpathSyntaxIsCheckedWithoutClaimingTheTargetExists()
    {
        const string xml = """
            <Patch><Operation Class="PatchOperationReplace">
              <xpath>Defs/ThingDef[defName="MP_Target"]/statBases</xpath>
              <value><statBases><Mass>50</Mass></statBases></value>
            </Operation></Patch>
            """;
        var document = DefDocument.Parse(xml);
        Assert.DoesNotContain(document.Validate(), x => x.Code == "MW024");
        Assert.Equal(xml, document.Export());
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void BrokenOrEmptyPatchXpathProducesNonBlockingWarningAtFieldPath()
    {
        const string xml = """
            <Patch><Operation Class="PatchOperationSequence">
              <operations>
                <li Class="PatchOperationReplace">
                  <xpath>Defs/ThingDef[defName=</xpath>
                </li>
                <li Class="PatchOperationReplace"><xpath>  </xpath></li>
              </operations>
            </Operation></Patch>
            """;
        var document = DefDocument.Parse(xml);
        var warnings = document.Validate().Where(x => x.Code == "MW024").ToArray();
        Assert.Equal(2, warnings.Length);
        Assert.All(warnings, x =>
        {
            Assert.Equal(Severity.Warning, x.Severity);
            Assert.Contains("/xpath", x.Location);
        });
        Assert.Equal(xml, document.Export());
        Assert.False(document.IsDirty);

        var workspace = new MechWorkspace("Patches", "");
        workspace.AddXml("Patches/MechPatch.xml", xml);
        Assert.Equal(2, workspace.Validate().Count(x => x.Issue.Code == "MW024"));
    }
}
