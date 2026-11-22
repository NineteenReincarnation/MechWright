using MechWright.Infrastructure;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class GameExportDiskRoundtripTests
{
    [Fact]
    public async Task ModExporterAndImporterProduceAuditableEquivalentXml()
    {
        var root = Path.Combine(Path.GetTempPath(), "mechwright-game-audit-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = new MechWorkspace("Audit", "");
            source.AddXml("1.7/Defs/Mech.xml", """
                <Defs>
                  <!-- authored -->
                  <ThingDef ParentName="BaseMechanoidWalker"
                      MayRequire="Ludeon.RimWorld.Biotech">
                    <defName>MP_AuditMech</defName>
                    <comps><li Class="CompProperties"><customFlag>true</customFlag></li></comps>
                  </ThingDef>
                </Defs>
                """, fromDisk: true);
            source.AddXml("1.7/Defs/Extra.xml",
                "<Defs><ThingDef><defName>MP_AuditResource</defName></ThingDef></Defs>",
                fromDisk: true);
            var before = GameLoadEvidence.ProjectFingerprint(source);
            var exportFolder = Path.Combine(root, "Exported-Mod");

            await WorkspaceDiskService.ExportAsync(source, exportFolder,
                "MechWright XML Audit", "local.mechwright.audit");
            var reimported = await WorkspaceDiskService.ImportAsync(exportFolder);
            var audit = GameExportAudit.Compare(source, reimported);
            Assert.True(audit.XmlMatches, audit.Summary);
            Assert.Equal(2, audit.ExactMatches);
            Assert.Equal(before, audit.ImportedXmlSha256);
            Assert.NotNull(reimported.OriginalAboutXml);
            Assert.Equal(before, GameLoadEvidence.ProjectFingerprint(source));
            Assert.False(source.IsDirty);

            var exportedXml = Path.Combine(exportFolder, "1.7", "Defs", "Mech.xml");
            var diskText = await File.ReadAllTextAsync(exportedXml);
            await File.WriteAllTextAsync(exportedXml, diskText.Replace(
                "</ThingDef>", "<!-- edited after export --></ThingDef>",
                StringComparison.Ordinal));
            var changed = await WorkspaceDiskService.ImportAsync(exportFolder);
            var changedAudit = GameExportAudit.Compare(source, changed);
            Assert.False(changedAudit.XmlMatches);
            Assert.Single(changedAudit.Differences);
            Assert.Equal("1.7/Defs/Mech.xml", changedAudit.Differences[0].RelativePath);
            Assert.Equal(before, GameLoadEvidence.ProjectFingerprint(source));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
