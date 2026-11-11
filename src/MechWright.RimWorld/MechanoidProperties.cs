using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Verified common fields for the standard mechanoid design form.
/// Suggested values are NOT vanilla inherited values and are never written
/// without an explicit apply action. Other XML fields remain available in
/// the raw document editor, even if not described here.
/// </summary>
public static class MechanoidProperties
{
    public sealed record Spec(string DefType, string Path, string Label, string Suggestion, string Help)
    {
        public string FieldName => Path.Split('/').Last();
    }

    private static readonly IReadOnlyList<Spec> Properties =
    [
        new("ThingDef", "statBases/MoveSpeed", "移动速度", "4.7", "普通行进速度；飞行或特殊移动机制另行实现"),
        new("ThingDef", "statBases/Mass", "质量（kg）", "85", "物体质量，与 baseBodySize 不同"),
        new("ThingDef", "statBases/ArmorRating_Sharp", "锐器护甲", "0.40", "0.40 = 40% 护甲值"),
        new("ThingDef", "statBases/ArmorRating_Blunt", "钝器护甲", "0.20", "普通钝击抗性"),
        new("ThingDef", "statBases/ArmorRating_Heat", "热能护甲", "2.00", "机械体常见高热抗性"),
        new("ThingDef", "statBases/BandwidthCost", "控制带宽", "2", "需要 Biotech；不决定 combatPower"),
        new("ThingDef", "race/body", "身体 BodyDef", "Pikeman", "引用已存在的原版或自定义 BodyDef"),
        new("ThingDef", "race/baseBodySize", "身体大小", "1", "影响身体尺寸判定，不是贴图 drawSize"),
        new("ThingDef", "race/baseHealthScale", "生命倍率", "1", "对身体部件的生命值缩放"),
        new("ThingDef", "race/mechWeightClass", "重量等级", "Medium", "Light / Medium / Heavy / UltraHeavy"),
        new("PawnKindDef", "race", "种族 ThingDef", "MP_NewMech", "应指向同一机械体的 ThingDef"),
        new("PawnKindDef", "combatPower", "战斗 CP", "200", "用于威胁预算，不等于市场价格"),
        new("PawnKindDef", "maxPerGroup", "单组上限", "3", "限制同一生成组的单位数量"),
        new("PawnKindDef", "allowInMechClusters", "机械集群生成", "true", "控制是否允许在机械集群中出现"),
        new("PawnKindDef", "controlGroupPortraitZoom", "控制组头像缩放", "1", "Biotech 机械师控制组头像表现")
    ];

    public static IReadOnlyList<Spec> For(string defType) =>
        Properties.Where(s => s.DefType == defType).ToList();

    public static string? Read(DefEntry definition, Spec field)
    {
        EnsureType(definition, field);
        XElement? cursor = definition.Element;
        foreach (var segment in field.Path.Split('/'))
        {
            cursor = cursor?.Element(segment);
            if (cursor == null) return null;
        }
        return cursor.HasElements ? null : cursor.Value;
    }

    public static void Apply(DefDocument document, DefEntry definition, Spec field, string value)
    {
        EnsureAttached(document, definition, field);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("不能应用空值。请使用“恢复继承”移除该配置。");
        var message = StandardFieldRules.Validate(field.FieldName, value);
        if (message != null) throw new ArgumentException(message);

        document.Transform(_ =>
        {
            XElement cursor = definition.Element;
            var parts = field.Path.Split('/');
            foreach (var name in parts)
            {
                var next = cursor.Element(name);
                if (next == null)
                {
                    next = new XElement(name);
                    cursor.Add(next);
                }
                cursor = next;
            }
            if (cursor.HasElements || cursor.Nodes().Any(n => n is not XText and not XCData))
                throw new InvalidOperationException("目标节点包含 XML 子结构或注释，请使用源码编辑器以避免丢失。");
            cursor.Value = value;
        });
    }

    public static void Reset(DefDocument document, DefEntry definition, Spec field)
    {
        EnsureAttached(document, definition, field);
        document.Transform(_ =>
        {
            XElement? cursor = definition.Element;
            foreach (var segment in field.Path.Split('/'))
                cursor = cursor?.Element(segment);
            cursor?.Remove(); // Leave parent containers, metadata and comments intact.
        });
    }

    private static void EnsureType(DefEntry definition, Spec field)
    {
        if (definition.Type != field.DefType || !Properties.Contains(field))
            throw new ArgumentException("This field is not defined for the selected Def type.");
    }

    private static void EnsureAttached(DefDocument document, DefEntry definition, Spec field)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureType(definition, field);
        if (definition.Element.Document != document.Xml)
            throw new InvalidOperationException("Selected definition belongs to an old document snapshot.");
    }
}
