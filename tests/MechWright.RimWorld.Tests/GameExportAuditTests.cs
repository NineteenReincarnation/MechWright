using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class GameExportAuditTests
{
    [Fact]
    public void IdenticalReimportPreservesExactXmlAndLeavesSourceUntouched()
    {
        const string first = """
            <?xml version="1.0" encoding="utf-8"?>
            <Defs>
              <!-- preserve author comment -->
              <ThingDef Name="MP_Base" Abstract="True">
                <defName>MP_Base</defName>
                <custom Class="AnotherMod.Custom"><li Inherit="False">value</li></custom>
              </ThingDef>
            </Defs>
            """;
        const string second = "<Defs><PawnKindDef><defName>MP_BeastKind</defName>" +
            "<race>MP_Beast</race></PawnKindDef></Defs>";
        var workspace = new MechWorkspace("Editor", "");
        workspace.AddXml("1.7/Defs/Thing.xml", first, fromDisk: true);
        workspace.AddXml("1.7/Defs/Kinds.xml", second, fromDisk: true);
        var imported = new MechWorkspace("Export", "");
        imported.AddXml("1.7/Defs/Kinds.xml", second, fromDisk: true);
        imported.AddXml("1.7/Defs/Thing.xml", first, fromDisk: true);

        var sourceFingerprint = GameLoadEvidence.ProjectFingerprint(workspace);
        var result = GameExportAudit.Compare(workspace, imported);
        Assert.True(result.XmlMatches);
        Assert.Equal(2, result.ExactMatches);
        Assert.Equal(0, result.TotalDifferences);
        Assert.Empty(result.Differences);
        Assert.Equal(sourceFingerprint, result.ImportedXmlSha256);
        Assert.False(workspace.IsDirty);
        Assert.False(imported.IsDirty);
        Assert.Equal(first, workspace.FindFile("1.7/Defs/Thing.xml")!.Document.Export());
    }

    [Fact]
    public void ReportsChangedMissingAndExtraFilesWithoutNormalizingUnknownContent()
    {
        var editor = new MechWorkspace("Editor", "");
        editor.AddXml("Defs/Same.xml", "<Defs><ThingDef><defName>MP_A</defName></ThingDef></Defs>", true);
        editor.AddXml("Defs/Changed.xml", "<Defs><ThingDef><defName>MP_B</defName></ThingDef></Defs>", true);
        editor.AddXml("Defs/Missing.xml", "<Defs><ThingDef><defName>MP_C</defName></ThingDef></Defs>", true);

        var exported = new MechWorkspace("Export", "");
        exported.AddXml("Defs/Same.xml", "<Defs><ThingDef><defName>MP_A</defName></ThingDef></Defs>", true);
        exported.AddXml("Defs/Changed.xml",
            "<Defs><ThingDef><!-- extra --><defName>MP_B</defName></ThingDef></Defs>", true);
        exported.AddXml("Defs/Extra.xml", "<Defs><ThingDef><defName>MP_D</defName></ThingDef></Defs>", true);

        var audit = GameExportAudit.Compare(editor, exported);
        Assert.False(audit.XmlMatches);
        Assert.Equal(1, audit.ExactMatches);
        Assert.Equal(3, audit.TotalDifferences);
        Assert.Contains(audit.Differences, x =>
            x.RelativePath == "Defs/Changed.xml" && x.Category.Contains("文本不同"));
        Assert.Contains(audit.Differences, x =>
            x.RelativePath == "Defs/Missing.xml" && x.Category.Contains("缺失"));
        Assert.Contains(audit.Differences, x =>
            x.RelativePath == "Defs/Extra.xml" && x.Category.Contains("仅在"));
        Assert.NotEqual(audit.SourceXmlSha256, audit.ImportedXmlSha256);
    }

    [Fact]
    public void ExportEvidenceReceiptIncludesAuditFingerprintAndStaleEditorWarning()
    {
        var project = new MechWorkspace("Check", "");
        project.AddXml("Defs/One.xml", "<Defs><ThingDef><defName>MP_Test</defName></ThingDef></Defs>");
        var reimported = new MechWorkspace("Export", "");
        reimported.AddXml("Defs/One.xml", "<Defs><ThingDef><defName>MP_Test</defName></ThingDef></Defs>");
        var audit = GameExportAudit.Compare(project, reimported);
        var checklist = new GameVerificationChecklist("1.7", "Biotech", "", "",
            true, false, false, false, false, false);

        var report = GameVerificationReport.Generate(checklist, null,
            GameLoadEvidence.ProjectFingerprint(project), 1, audit);
        Assert.Contains(audit.SourceXmlSha256, report);
        Assert.Contains("导出目录 XML 重导入核对", report);
        Assert.Contains("待人工确认", report);
        Assert.Contains("不代表实际游戏版本", report);

        var file = project.Files.Single();
        file.Document.AddDefinition("ThingDef", "MP_New");
        var modifiedReport = GameVerificationReport.Generate(checklist, null,
            GameLoadEvidence.ProjectFingerprint(project), 2, audit);
        Assert.Contains("编辑器 XML 已改变，应重新核对", modifiedReport);
    }

    [Fact]
    public void GameLogLocatedDefPathsAreOnlyRelativeProjectNamesInReceipt()
    {
        var checklist = new GameVerificationChecklist("1.7", "", "", "",
            false, false, false, false, false, false);
        var report = GameVerificationReport.Generate(checklist, null, "ABC", 1, null,
        [
            new GameLogDefinitionLocation(28, "MP_Beast", "ThingDef",
                "1.7/Defs/Mechanoids.xml")
        ]);
        Assert.Contains("MP_Beast", report);
        Assert.Contains("1.7/Defs/Mechanoids.xml", report);
        Assert.Contains("Player.log 行号", report);
        Assert.Contains("待人工确认", report);
    }
}
