namespace MechWright.RimWorld;

/// <summary>
/// Compares the editor's current XML against XML re-imported from an exported
/// Mod folder. No assets, About.xml, runtime patches, or game load results are
/// verified here. The comparison never mutates either workspace.
/// </summary>
public static class GameExportAudit
{
    public const int MaxShownDifferences = 100;

    public sealed record Difference(string RelativePath, string Category)
    {
        public string Display => $"{Category} · {RelativePath}";
    }

    public sealed record Result(
        string SourceXmlSha256, string ImportedXmlSha256,
        int SourceFileCount, int ImportedFileCount,
        int ExactMatches, int TotalDifferences,
        IReadOnlyList<Difference> Differences)
    {
        public bool XmlMatches => TotalDifferences == 0;
        public string Summary => $"导出目录 XML 对照：原工程 {SourceFileCount} 个、" +
            $"重导入 {ImportedFileCount} 个；逐字一致 {ExactMatches} 个，" +
            $"不一致 {TotalDifferences} 处。" +
            (TotalDifferences > Differences.Count ? $" 只显示前 {Differences.Count} 项。" : "") +
            " 仅核对 XML，不代表 Mod 已在游戏中加载，也不核对素材或 About.xml。";
    }

    public static Result Compare(MechWorkspace source, MechWorkspace reimported)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reimported);
        var expected = source.Files.ToDictionary(x => x.RelativePath,
            x => x.Document.Export(), StringComparer.OrdinalIgnoreCase);
        var imported = reimported.Files.ToDictionary(x => x.RelativePath,
            x => x.Document.Export(), StringComparer.OrdinalIgnoreCase);
        var all = expected.Keys.Concat(imported.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        var matches = 0;
        var count = 0;
        var differences = new List<Difference>();
        foreach (var file in all)
        {
            string? category = null;
            if (!expected.TryGetValue(file, out var before))
                category = "仅在重导入目录中出现";
            else if (!imported.TryGetValue(file, out var after))
                category = "导出目录缺失文件";
            else if (!string.Equals(before, after, StringComparison.Ordinal))
                category = "文件 XML 文本不同";
            else matches++;

            if (category == null) continue;
            count++;
            if (differences.Count < MaxShownDifferences)
                differences.Add(new Difference(file, category));
        }
        return new Result(GameLoadEvidence.ProjectFingerprint(source),
            GameLoadEvidence.ProjectFingerprint(reimported),
            expected.Count, imported.Count, matches, count, differences);
    }
}
