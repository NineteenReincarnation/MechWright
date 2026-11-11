using System.Globalization;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Compare original XML facts against *explicit* values in a user ThingDef.
/// Does not guess inherited values or run RimWorld game logic.
/// </summary>
public static class VanillaMechComparison
{
    public sealed record ComparisonRow(
        string Label, string XmlPath,
        string VanillaValue, string UserExplicitValue,
        string Delta, string VanillaOrigin);

    public sealed record ComparisonResult(
        string UserDefName, string VanillaDefName,
        IReadOnlyList<ComparisonRow> Rows, string Notice);

    public static ComparisonResult Compare(
        VanillaMechanoidLibrary.Profile original,
        DefDocument current, DefEntry userMech, MechWorkspace? project = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(current);
        if (userMech.Type != "ThingDef" || userMech.Element.Document != current.Xml)
            throw new ArgumentException("Select the currently edited ThingDef to compare.");

        var defName = userMech.DefName;
        DefEntry? pawnKind = null;
        if (project != null)
        {
            pawnKind = project.Files
                .SelectMany(f => f.Document.Definitions())
                .FirstOrDefault(x => x.Type == "PawnKindDef" &&
                    string.Equals(x.Element.Element("race")?.Value, defName,
                        StringComparison.Ordinal));
        }
        else
        {
            pawnKind = current.Definitions().FirstOrDefault(x => x.Type == "PawnKindDef" &&
                string.Equals(x.Element.Element("race")?.Value, defName,
                    StringComparison.Ordinal));
        }

        var rows = new List<ComparisonRow>();
        foreach (var fact in original.Fields)
        {
            var source = fact.XmlPath is "combatPower" or "maxPerGroup" or
                "allowInMechClusters" or "weaponTags" ? pawnKind : userMech;
            var explicitValue = Read(source, fact.XmlPath);
            var difference = "—";
            if (explicitValue != null && fact.Value != null &&
                double.TryParse(explicitValue, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var userNumber) &&
                double.TryParse(fact.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var vanillaNumber) &&
                double.IsFinite(userNumber) && double.IsFinite(vanillaNumber))
            {
                difference = (userNumber - vanillaNumber)
                    .ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
            }
            rows.Add(new ComparisonRow(fact.Label, fact.XmlPath,
                fact.Display, explicitValue ?? "未显式配置", difference, fact.Provenance));
        }
        return new ComparisonResult(defName, original.DefName, rows,
            pawnKind == null
                ? "未关联当前工程中的 PawnKindDef：CP、生成及武器标签按未显式配置显示。"
                : "仅对比当前 ThingDef/PawnKindDef 的显式 XML 值；不计算自定义继承、C# 默认值或游戏运行时修正。");
    }

    private static string? Read(DefEntry? definition, string path)
    {
        if (definition == null) return null;
        XElement? field = definition.Element;
        foreach (var segment in path.Split('/'))
        {
            field = field?.Element(segment);
            if (field == null) return null;
        }
        if (field.HasElements)
        {
            var items = field.Elements().Select(x => x.Value.Trim())
                .Where(x => x.Length > 0).ToArray();
            return items.Length == 0 ? null : string.Join(", ", items);
        }
        return string.IsNullOrWhiteSpace(field.Value) ? null : field.Value.Trim();
    }
}
