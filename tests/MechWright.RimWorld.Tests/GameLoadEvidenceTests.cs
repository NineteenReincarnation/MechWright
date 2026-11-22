using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class GameLoadEvidenceTests
{
    [Fact]
    public void ExtractsRelevantPlayerLogErrorByLineWithoutRetainingLogTextOrPaths()
    {
        const string log = """
            RimWorld 1.7 started
            C:\Users\PrivateName\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios
            Error: Could not resolve cross-reference to Verse.ThingDef named MP_Raptor
            Exception: unrelated system component
            XML error in MP_RaptorPlus
            Everything else looks fine for MP_Raptor
            """;
        var found = GameLoadEvidence.Analyze(log, ["MP_Raptor"], "project-snapshot");
        Assert.Equal(6, found.LineCount);
        Assert.Equal(3, found.PossibleErrorLines);
        Assert.Equal(1, found.RelevantErrorLines);
        Assert.Equal(2, found.RelevantMentions);
        var row = Assert.Single(found.Issues);
        Assert.Equal(3, row.LineNumber);
        Assert.Equal("可能的引用解析问题", row.Kind);
        Assert.Equal("MP_Raptor", Assert.Single(row.Definitions));
        Assert.DoesNotContain("PrivateName", found.Summary);
        Assert.DoesNotContain("PrivateName", row.Display);
        Assert.DoesNotContain("AppData", GameVerificationReport.Generate(
            new GameVerificationChecklist("1.7", "Biotech", "", "", false,
                false, false, false, false, false),
            found, "project-snapshot", 1));
    }

    [Fact]
    public void AQuietLogNeverCertifiesGameLoadAndChangedXmlInvalidatesSnapshot()
    {
        var project = new MechWorkspace("Local", "");
        var file = project.AddXml("Defs/Mech.xml",
            "<Defs><ThingDef><defName>MP_Test</defName></ThingDef></Defs>");
        var before = GameLoadEvidence.ProjectFingerprint(project);
        var log = GameLoadEvidence.Analyze("RimWorld started\nloaded something",
            ["MP_Test"], before);
        Assert.Empty(log.Issues);
        Assert.Equal(0, log.RelevantErrorLines);
        var checklist = new GameVerificationChecklist("1.7.abc", "Biotech", "tester", "",
            true, false, true, false, false, false);
        var report = GameVerificationReport.Generate(checklist, log,
            GameLoadEvidence.ProjectFingerprint(project), 1);
        Assert.Contains("待人工确认", report);
        Assert.Contains("用户已勾选", report);
        Assert.Contains("SHA-256 一致", report);
        Assert.Contains(log.LogSha256, report);

        file.Document.AddDefinition("ThingDef", "MP_Another");
        var updated = GameVerificationReport.Generate(checklist, log,
            GameLoadEvidence.ProjectFingerprint(project), 2);
        Assert.Contains("不一致：工程在扫描后可能变更", updated);
        Assert.NotEqual(before, GameLoadEvidence.ProjectFingerprint(project));
    }

    [Fact]
    public void NoLogProducesExplicitlyUnverifiedReport()
    {
        var report = GameVerificationReport.Generate(
            new GameVerificationChecklist("", "", "", "untested", false, false,
                false, false, false, false), null, "FINGERPRINT", 0);
        Assert.Contains("未导入 Player.log", report);
        Assert.Contains("未确认", report);
        Assert.Contains("待人工确认", report);
    }

    [Fact]
    public void SourceLimitsAndCaseSensitiveDefNamesPreventUnsafeOrSpuriousMatches()
    {
        Assert.Throws<InvalidDataException>(() => GameLoadEvidence.Analyze(
            new string('x', GameLoadEvidence.MaxLogCharacters + 1),
            ["MP_X"], "snapshot"));
        var found = GameLoadEvidence.Analyze(
            "Error MP_X1\nERROR mp_x\nError MP_X", ["MP_X"], "snapshot");
        Assert.Equal(1, found.RelevantErrorLines);
        Assert.Equal(3, found.PossibleErrorLines);
        Assert.Equal(3, found.LineCount);
    }
}
