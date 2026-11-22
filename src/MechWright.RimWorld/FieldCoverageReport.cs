using System.Globalization;
using System.Text;

namespace MechWright.RimWorld;

/// <summary>
/// An auditable, read-only inventory derived from the user's own installed
/// RimWorld data. Observed XML and CLR candidates are deliberately not
/// described as validated or complete RimWorld serialization fields.
/// </summary>
public static class FieldCoverageReport
{
    private static readonly string[] AssemblyTypes =
    [
        "ThingDef", "PawnKindDef", "BodyDef", "BodyPartDef",
        "BodyPartGroupDef", "RecipeDef", "AbilityDef", "HediffDef",
        "DamageDef", "EffecterDef", "SoundDef", "StatDef"
    ];

    public static string Generate(DefCatalog.CatalogScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        if (!scan.IsGameInstallation)
            throw new InvalidOperationException("请先连接合法 RimWorld 游戏安装目录。");

        var observed = scan.NativeFields.All;
        var types = observed.Select(x => x.DefType).Concat(AssemblyTypes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var result = new StringBuilder();
        result.AppendLine("# MechWright · 本机 RimWorld 字段证据清单");
        result.AppendLine();
        result.AppendLine("> 来源：当前用户本机 Core 与 DLC 的 XML、可读取的 Assembly-CSharp.dll 元数据。");
        result.AppendLine("> **证据限制**：观察到的 XML 路径不等于完整 schema；程序集公开字段只是候选。")
            .AppendLine("本报告不代表游戏加载验证，也不计算未经证明的“字段覆盖率”。");
        result.AppendLine();

        result.AppendLine("## 游戏包扫描情况");
        result.AppendLine();
        result.AppendLine("| 包 | Def XML 目录 | 有 defName 的定义数 | 已观察的字段路径（去重） |");
        result.AppendLine("| --- | --- | ---: | ---: |");
        foreach (var p in scan.Packages)
        {
            var paths = observed.Count(x => IncludesPackage(x.SourcePackages, p.Name));
            result.Append("| ").Append(Escape(p.Name)).Append(" | ")
                .Append(p.Present ? "存在" : "未安装或未找到").Append(" | ")
                .Append(p.DefinitionCount.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(paths.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" |");
        }
        result.AppendLine();
        result.AppendLine("注意：定义数只统计有 defName 的定义；字段路径仅统计扫描算法可提取的非空文本叶节点，")
            .AppendLine("不包括所有复杂 XML 属性、空容器、Patch 操作及 C# 运行时行为。");
        result.AppendLine();

        result.AppendLine("## 类型统计（独立证据来源）");
        result.AppendLine();
        result.AppendLine("| Def 类型 | XML 已观察路径 | CLR 字段候选 |");
        result.AppendLine("| --- | ---: | ---: |");
        foreach (var type in types)
        {
            var count = observed.Count(x => x.DefType == type);
            var metadataCount = scan.GameFields.For(type).Count;
            if (count == 0 && metadataCount == 0) continue;
            result.Append("| ").Append(Escape(type)).Append(" | ")
                .Append(count.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(metadataCount.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" |");
        }
        result.AppendLine();

        result.AppendLine("## XML 中已观察的字段路径");
        result.AppendLine();
        result.AppendLine("| Def 类型 | 字段路径 | 观察类型 | 来源包 | 出现次数 | 编辑器插入策略 | CLR 证据 |");
        result.AppendLine("| --- | --- | --- | --- | ---: | --- | --- |");
        foreach (var f in observed.OrderBy(x => x.DefType, StringComparer.Ordinal)
                     .ThenBy(x => x.Path, StringComparer.Ordinal))
        {
            var metadata = scan.GameFields.For(f.DefType)
                .Any(x => string.Equals(x.Path, f.Path, StringComparison.Ordinal));
            result.Append("| ").Append(Escape(f.DefType)).Append(" | ")
                .Append(Escape(f.Path)).Append(" | ")
                .Append(Escape(f.ObservedType)).Append(" | ")
                .Append(Escape(f.SourcePackages)).Append(" | ")
                .Append(f.Observations.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(f.CanInsert ? "可尝试简单标量编辑（仍需验证）" : "禁止直接标量插入").Append(" | ")
                .Append(metadata ? "另见 CLR 候选" : "无匹配候选")
                .AppendLine(" |");
        }
        result.AppendLine();

        result.AppendLine("## 仅从程序集元数据推导的候选字段");
        result.AppendLine();
        result.AppendLine("| Def 类型 | 字段路径 | 候选类型 | CLR 类型 | XML 样本中出现 |");
        result.AppendLine("| --- | --- | --- | --- | --- |");
        var observedKeys = observed.Select(x => (x.DefType, x.Path)).ToHashSet();
        foreach (var type in AssemblyTypes)
            foreach (var f in scan.GameFields.For(type)
                         .OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                result.Append("| ").Append(Escape(type)).Append(" | ")
                    .Append(Escape(f.Path)).Append(" | ")
                    .Append(Escape(f.Kind)).Append(" | ")
                    .Append(Escape(f.TypeName)).Append(" | ")
                    .Append(observedKeys.Contains((type, f.Path)) ? "是" : "否")
                    .AppendLine(" |");
            }
        result.AppendLine();

        if (scan.Warnings.Count > 0)
        {
            result.AppendLine("## 扫描注意事项");
            result.AppendLine();
            // Never include local absolute paths or game content in exported
            // diagnostics: the report is shareable without identifying a user.
            result.AppendLine("扫描时发生读取异常或缺项，共 " +
                scan.Warnings.Count.ToString(CultureInfo.InvariantCulture) +
                " 条；请在程序内查看状态并复核本地安装。");
            result.AppendLine();
        }
        result.AppendLine("## 尚未验证");
        result.AppendLine();
        result.AppendLine("- 游戏实际加载、继承 / PatchOperation 的最终生效值、不同 DLC 组合合法性。");
        result.AppendLine("- 复杂集合、多态 Class、空节点、运行时 C# 逻辑与第三方 Mod 注入字段。");
        result.AppendLine("- 当前报告使用观察路径与程序集候选，不提供完整性百分比。");

        return result.ToString();
    }

    private static bool IncludesPackage(string sourcePackages, string package) =>
        sourcePackages.Split(" / ", StringSplitOptions.RemoveEmptyEntries)
            .Contains(package, StringComparer.Ordinal);

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
