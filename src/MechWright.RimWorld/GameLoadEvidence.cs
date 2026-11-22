using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MechWright.RimWorld;

/// <summary>
/// Conservative, privacy-preserving local Player.log evidence extraction.
/// The report never embeds log text or machine-specific file paths, and
/// absence of observed errors never certifies that a Mod loaded successfully.
/// </summary>
public static class GameLoadEvidence
{
    public const int MaxLogCharacters = 12_000_000;
    public const int MaxIssueRows = 100;
    private static readonly Regex Tokens = new(
        @"[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));
    private static readonly Regex Signals = new(
        @"\b(error|exception|failed|failure|could not|unable to|missing|unresolved)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public sealed record LogIssue(int LineNumber, string Kind,
        IReadOnlyList<string> Definitions)
    {
        public string Display => $"第 {LineNumber} 行 · {Kind} · " +
            string.Join("、", Definitions);
    }

    public sealed record Result(
        string LogSha256, string ProjectSha256, int LineCount,
        int PossibleErrorLines, int RelevantErrorLines, int RelevantMentions,
        IReadOnlyList<LogIssue> Issues)
    {
        public string Summary => $"已读取游戏日志 {LineCount} 行 · " +
            $"含疑似错误标记 {PossibleErrorLines} 行 · " +
            $"关联工程 DefName 的疑似错误 {RelevantErrorLines} 行。" +
            (RelevantErrorLines > Issues.Count ? $" 仅展示前 {Issues.Count} 项。" : "") +
            " 日志分析不是游戏加载成功证明。";
    }

    /// <summary>
    /// SHA-256 of sorted relative file names and current XML bytes; use for
    /// identifying whether an imported log was reviewed against another
    /// editor state. It does NOT establish which version the game actually ran.
    /// </summary>
    public static string ProjectFingerprint(MechWorkspace? project,
        DefDocument? singleDocument = null)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var items = project == null
            ? singleDocument == null ? Array.Empty<(string Path, string Xml)>()
                : new[] { ("standalone.xml", singleDocument.Export()) }
            : project.Files.Select(x => (x.RelativePath, x.Document.Export())).ToArray();
        foreach (var (path, xml) in items.OrderBy(x => x.Item1,
                     StringComparer.OrdinalIgnoreCase))
        {
            sha.AppendData(Encoding.UTF8.GetBytes(path));
            sha.AppendData([0]);
            sha.AppendData(Encoding.UTF8.GetBytes(xml));
            sha.AppendData([0]);
        }
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    public static Result Analyze(string log,
        IReadOnlyCollection<string> projectDefNames, string projectFingerprint)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(projectDefNames);
        if (log.Length > MaxLogCharacters)
            throw new InvalidDataException("Player.log 超过 1200 万字符的安全读取限制。");
        var names = projectDefNames.Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(log)));
        var lines = 0;
        var signals = 0;
        var relevant = 0;
        var mentions = 0;
        var issues = new List<LogIssue>();
        using var reader = new StringReader(log);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines++;
            // Player.log can contain credentials or filesystem paths.
            // Scan in memory, but never keep even an excerpt in Result.
            var matching = Tokens.Matches(line).Select(x => x.Value)
                .Where(names.Contains).Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).Take(12).ToArray();
            if (matching.Length > 0) mentions++;
            if (!Signals.IsMatch(line)) continue;
            signals++;
            if (matching.Length == 0) continue;
            relevant++;
            if (issues.Count >= MaxIssueRows) continue;
            var kind = line.Contains("cross-reference", StringComparison.OrdinalIgnoreCase) ||
                       line.Contains("resolve", StringComparison.OrdinalIgnoreCase)
                ? "可能的引用解析问题"
                : line.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("def", StringComparison.OrdinalIgnoreCase)
                ? "可能的 XML / Def 问题"
                : line.Contains("exception", StringComparison.OrdinalIgnoreCase)
                ? "可能的异常"
                : "其他疑似错误";
            issues.Add(new LogIssue(lines, kind, matching));
        }
        return new Result(hash, projectFingerprint, lines, signals, relevant,
            mentions, issues);
    }
}

public sealed record GameVerificationChecklist(
    string GameVersion, string EnabledDlc, string Tester, string Notes,
    bool GameLoaded, bool Spawned, bool GraphicsChecked,
    bool CombatChecked, bool GestationChecked, bool ReimportChecked);

public static class GameVerificationReport
{
    public static string Generate(
        GameVerificationChecklist checklist,
        GameLoadEvidence.Result? log,
        string currentProjectSha256,
        int definitionCount)
    {
        static string Safe(string value, int length = 600)
        {
            if (string.IsNullOrWhiteSpace(value)) return "未记录";
            var cleaned = value.Trim().Replace("\r", " ").Replace("\n", " ")
                .Replace("|", "／");
            return cleaned.Length > length ? cleaned[..length] : cleaned;
        }
        static string Checked(bool value) => value ? "用户已勾选" : "未确认";
        var same = log != null && log.ProjectSha256 == currentProjectSha256;
        var lines = new List<string>
        {
            "# MechWright · RimWorld 实机验收记录",
            "",
            "> 手动核验记录及日志线索；本软件无法代替运行游戏。即使未找到异常，也不代表加载成功。",
            "",
            "## 验收环境（用户填写）",
            "",
            "| 项目 | 记录 |",
            "| --- | --- |",
            $"| RimWorld 版本 | {Safe(checklist.GameVersion, 100)} |",
            $"| 启用 DLC / Mod | {Safe(checklist.EnabledDlc, 300)} |",
            $"| 验收执行者 | {Safe(checklist.Tester, 100)} |",
            $"| 当前工程定义数 | {definitionCount} |",
            $"| 当前编辑器 XML 快照 SHA-256 | `{currentProjectSha256}` |",
            "",
            "## 手动验收步骤",
            "",
            $"| 游戏启动与加载此 Mod | {Checked(checklist.GameLoaded)} |",
            $"| 生成 / 调试生成机械体 | {Checked(checklist.Spawned)} |",
            $"| N/S/E/W 贴图及身体部件观察 | {Checked(checklist.GraphicsChecked)} |",
            $"| 攻击 / 技能与实际战斗 | {Checked(checklist.CombatChecked)} |",
            $"| 机械培育或配方（如适用） | {Checked(checklist.GestationChecked)} |",
            $"| 导出后重新导入 XML 对照 | {Checked(checklist.ReimportChecked)} |",
            "",
            "## Player.log 证据",
            ""
        };
        if (log == null)
        {
            lines.Add("未导入 Player.log。以上勾选均为用户手动声明，无法凭此认定游戏加载通过。");
        }
        else
        {
            lines.Add($"- 解码后日志文本 SHA-256：`{log.LogSha256}`");
            lines.Add($"- 日志总行数：{log.LineCount}");
            lines.Add($"- 疑似错误标记的日志行：{log.PossibleErrorLines}");
            lines.Add($"- 同行提及工程 DefName 的疑似错误行：{log.RelevantErrorLines}");
            lines.Add($"- 提及工程 DefName 的日志行：{log.RelevantMentions}");
            lines.Add("- 日志导入时与当前编辑器工程快照：" +
                (same ? "SHA-256 一致（仍不能证明游戏实际加载了该快照）" :
                    "不一致：工程在扫描后可能变更，需重新验收"));
            lines.Add("");
            lines.Add("| 日志行号 | 信号类别 | 工程 DefName |");
            lines.Add("| --- | --- | --- |");
            foreach (var issue in log.Issues)
                lines.Add($"| {issue.LineNumber} | {issue.Kind} | " +
                    $"{string.Join("、", issue.Definitions)} |");
            if (log.Issues.Count == 0) lines.Add("| — | 没有找到同行匹配的疑似错误 | — |");
            lines.Add("");
            lines.Add("原始日志、错误原文与本机文件路径没有收录；请在本地日志中按行号人工复核。" +
                " 其他 Mod、跨行堆栈或不同格式的错误可能无法关联。");
        }
        lines.Add("");
        lines.Add("## 补充观察");
        lines.Add("");
        lines.Add(Safe(checklist.Notes, 1500));
        lines.Add("");
        lines.Add("## 验收结论");
        lines.Add("");
        lines.Add("**待人工确认。** 此记录仅显示用户勾选的检查项、工程指纹与日志信号；" +
            "无论检查项是否全部完成，均不自动颁发 RimWorld 兼容性认证。");
        lines.Add("");
        return string.Join("\n", lines);
    }
}
