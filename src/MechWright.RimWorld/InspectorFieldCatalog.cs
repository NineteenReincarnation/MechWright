using System.Globalization;
using System.Xml;

namespace MechWright.RimWorld;

/// <summary>
/// Native field metadata for the visual Inspector. Combines carefully reviewed
/// starter fields with observed Core/DLC XML fields without executing any DLL.
/// The observed XML is evidence of usage, not a complete runtime schema.
/// This type is independent of Avalonia and never writes to the game installation.
/// </summary>
public static class InspectorFieldCatalog
{
    public sealed record Field(
        string DefType, string Path, string Label, string Category,
        string Kind, string Hint, string Source, bool Verified,
        IReadOnlyList<string> Choices, bool IsStarter)
    {
        public string Name => Path.Split('/')[^1];
        public bool IsObserved => !IsStarter;
        public string SourceLabel => IsObserved
            ? "原版 XML 样本 · " + Source
            : "已整理字段";
    }

    public static IReadOnlyList<string> Categories { get; } =
        ["全部", "常用", "基础属性", "机体", "武器与战斗", "外观与声音", "其他"];

    private sealed record Starter(
        string Type, string Path, string Label, string Category,
        string Kind, string Hint, string[]? Choices = null);

    // A starter is metadata for one legal Def type, NOT a template inserted
    // into XML. No value is filled automatically and no gameplay defaults are
    // claimed. Every new path is committed only on explicit user action.
    private static readonly Starter[] Starters =
    [
        new("ThingDef", "description", "描述文字", "常用", "Text", "在游戏中显示的说明"),
        new("ThingDef", "statBases/MoveSpeed", "移动速度", "基础属性", "Number", "格/秒"),
        new("ThingDef", "statBases/Mass", "质量", "基础属性", "Number", "单位 kg"),
        new("ThingDef", "statBases/MarketValue", "市场价值", "基础属性", "Number", "交易价值，不是战斗点数"),
        new("ThingDef", "statBases/ArmorRating_Sharp", "锐器护甲", "基础属性", "Number", "0.4 表示 40%"),
        new("ThingDef", "statBases/ArmorRating_Blunt", "钝器护甲", "基础属性", "Number", "0.2 表示 20%"),
        new("ThingDef", "statBases/ArmorRating_Heat", "热能护甲", "基础属性", "Number", "热能护甲"),
        new("ThingDef", "statBases/BandwidthCost", "控制带宽", "机体", "Integer", "机械师控制带宽"),
        new("ThingDef", "race/baseBodySize", "身体大小", "机体", "Number", "身体大小，不等于贴图尺寸"),
        new("ThingDef", "race/baseHealthScale", "生命倍率", "机体", "Number", "身体部件生命倍率"),
        new("ThingDef", "race/mechWeightClass", "重量等级", "机体", "Enum", "原生机械体重量等级",
            ["Light", "Medium", "Heavy", "UltraHeavy"]),
        new("ThingDef", "race/body", "身体结构", "机体", "Text", "现有 BodyDef 的标识"),
        new("ThingDef", "race/needsRest", "需要休息", "机体", "Boolean", "是否需要休息"),
        new("ThingDef", "race/hasGenders", "具有性别", "机体", "Boolean", "是否区分性别"),
        new("ThingDef", "graphicData/texPath", "贴图路径", "外观与声音", "Text", "游戏内的相对贴图路径"),
        new("ThingDef", "graphicData/drawSize", "绘制大小", "外观与声音", "Number", "游戏中显示的尺寸"),
        new("PawnKindDef", "description", "描述文字", "常用", "Text", "种类的说明"),
        new("PawnKindDef", "combatPower", "战斗点数 CP", "武器与战斗", "Number", "用于袭击生成预算"),
        new("PawnKindDef", "maxPerGroup", "单组数量上限", "武器与战斗", "Integer", "生成组内的数量上限"),
        new("PawnKindDef", "allowInMechClusters", "加入机械集群", "武器与战斗", "Boolean", "是否允许出现于机械集群"),
        new("PawnKindDef", "controlGroupPortraitZoom", "控制组头像比例", "机体", "Number", "头像在控制组的显示比例"),
        new("BodyPartDef", "hitPoints", "基础部件血量", "基础属性", "Integer", "身体部件生命值"),
        new("BodyPartDef", "label", "显示名称", "常用", "Text", "在游戏中显示的名称"),
        new("BodyDef", "label", "显示名称", "常用", "Text", "在游戏中显示的名称"),
        new("RecipeDef", "description", "描述文字", "常用", "Text", "配方说明"),
        new("AbilityDef", "description", "描述文字", "常用", "Text", "能力说明")
    ];

    private static bool ValidPath(string path)
    {
        var segments = path.Split('/');
        if (segments.Length is < 1 or > 8 || path.Length > 180 ||
            segments.Any(s => s.Length is < 1 or > 100 || s == "li" || s == "defName"))
            return false;
        try
        {
            foreach (var s in segments) XmlConvert.VerifyNCName(s);
            return true;
        }
        catch (XmlException) { return false; }
    }

    public static IReadOnlyList<Field> For(string defType, NativeFieldCatalog observed)
    {
        ArgumentNullException.ThrowIfNull(observed);
        var fields = new List<Field>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var starter in Starters.Where(s => s.Type == defType))
        {
            if (!ValidPath(starter.Path) || !used.Add(starter.Path)) continue;
            var rule = StandardFieldRules.Describe(starter.Path.Split('/')[^1]);
            fields.Add(new Field(starter.Type, starter.Path, starter.Label,
                starter.Category, rule?.Kind ?? starter.Kind, starter.Hint,
                "内置字段说明", true,
                rule?.Allowed ?? starter.Choices ?? [], true));
        }

        // Samples are restricted to THIS definition type: the UI must never
        // offer a PawnKindDef field on a ThingDef or vice versa.
        foreach (var sample in observed.For(defType))
        {
            if (!sample.CanInsert || !ValidPath(sample.Path) ||
                sample.Path is "label" or "description" or "race/body" ||
                !used.Add(sample.Path)) continue;
            var rule = StandardFieldRules.Describe(sample.FieldName);
            var kind = rule?.Kind ?? sample.ObservedType;
            if (kind is not ("Text" or "Boolean" or "Number" or "Integer" or "Enum"))
                kind = "Text";
            var label = rule?.Description ?? sample.FieldName;
            fields.Add(new Field(defType, sample.Path, label,
                CategoryFor(defType, sample.Path), kind,
                (rule?.Hint ?? "从已安装的原版 XML 观察到的字段") +
                "；示例不是默认值，不能代替运行时类型验证。",
                sample.SourcePackages, rule != null, rule?.Allowed ?? [], false));
        }

        return fields.OrderBy(x => CategoryOrder(x.Category))
            .ThenBy(x => x.IsStarter ? 0 : 1)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<Field> Filter(
        IReadOnlyList<Field> fields, string category, string search,
        ISet<string>? occupied = null, int limit = 300)
    {
        search = (search ?? "").Trim();
        return fields.Where(x => occupied?.Contains(x.Path) != true)
            .Where(x => category == "全部" ||
                category == "常用" && x.IsStarter ||
                x.Category == category)
            .Where(x => search.Length == 0 ||
                x.Label.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Path.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Hint.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(limit, 1, 1000)).ToArray();
    }

    public static string CategoryFor(string type, string path)
    {
        if (path.StartsWith("statBases/", StringComparison.Ordinal))
            return "基础属性";
        if (path.StartsWith("race/", StringComparison.Ordinal) ||
            path.Contains("Bandwidth", StringComparison.OrdinalIgnoreCase))
            return "机体";
        if (type == "PawnKindDef" ||
            path.Contains("verb", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("combat", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("attack", StringComparison.OrdinalIgnoreCase))
            return "武器与战斗";
        if (path.Contains("graphic", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("sound", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("draw", StringComparison.OrdinalIgnoreCase))
            return "外观与声音";
        return "其他";
    }

    private static int CategoryOrder(string category)
    {
        var i = Categories.IndexOf(category);
        return i < 0 ? 100 : i;
    }
}
