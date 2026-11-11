using System.Globalization;

namespace MechWright.RimWorld;

/// <summary>
/// Conservative rules for high-frequency *verified* RimWorld primitive fields.
/// Not a replacement for the versioned complete Def catalog. Unknown/Mod fields
/// deliberately remain editable without semantic assumptions.
/// </summary>
public static class StandardFieldRules
{
    public sealed record Rule(string Description, string Kind, double? Minimum = null,
        double? Maximum = null, string[]? Allowed = null)
    {
        public string Hint => Allowed is { Length: > 0 }
            ? $"{Description} · {string.Join(" / ", Allowed)}"
            : $"{Description} · {Kind}{(Minimum.HasValue ? $" · 最小 {Minimum.Value.ToString(CultureInfo.InvariantCulture)}" : "")}";
    }

    private static readonly IReadOnlyDictionary<string, Rule> Known =
        new Dictionary<string, Rule>(StringComparer.Ordinal)
        {
            ["MoveSpeed"] = Number("移动速度", 0),
            ["Mass"] = Number("质量（kg）", 0),
            ["MarketValue"] = Number("市场价值", 0),
            ["ArmorRating_Sharp"] = Number("锐器护甲", 0),
            ["ArmorRating_Blunt"] = Number("钝器护甲", 0),
            ["ArmorRating_Heat"] = Number("热能护甲", 0),
            ["baseBodySize"] = Number("身体大小", 0.001),
            ["baseHealthScale"] = Number("身体部件生命倍率", 0.001),
            ["combatPower"] = Number("战斗点数 CP", 0),
            ["BandwidthCost"] = Integer("控制带宽", 0),
            ["hitPoints"] = Integer("基础部件血量", 1),
            ["damageAmountBase"] = Number("基础弹丸伤害", 0),
            ["armorPenetrationBase"] = Number("基础护甲穿透", 0),
            ["stoppingPower"] = Number("冲击力", 0),
            ["range"] = Number("射程（格）", 0),
            ["warmupTime"] = Number("瞄准预热（秒）", 0),
            ["cooldownTime"] = Number("近战冷却（秒）", 0),
            ["RangedWeapon_Cooldown"] = Number("武器冷却（秒）", 0),
            ["power"] = Number("近战威力", 0),
            ["coverage"] = new Rule("父部件相对覆盖份额", "Number", 0, 1),
            ["burstShotCount"] = Integer("连发弹数", 1),
            ["ticksBetweenBurstShots"] = Integer("连发间隔（ticks）", 0),
            ["gestationCycles"] = Integer("培育周期", 1),
            ["maxPerGroup"] = Integer("单组最大数量", 1),
            ["mechWeightClass"] = Options("机械重量等级", "Light", "Medium", "Heavy", "UltraHeavy"),
            ["depth"] = Options("部件深度", "Outside", "Inside"),
            ["height"] = Options("部件高度", "Top", "Middle", "Bottom"),
            ["allowInMechClusters"] = Boolean("允许加入机械集群"),
            ["needsRest"] = Boolean("需要休息"),
            ["hasGenders"] = Boolean("具有性别"),
            ["canFlyInVacuum"] = Boolean("可在真空环境飞行")
        };

    private static Rule Number(string description, double min) => new(description, "Number", min);
    private static Rule Integer(string description, int min) => new(description, "Integer", min);
    private static Rule Boolean(string description) => new(description, "Boolean");
    private static Rule Options(string description, params string[] allowed) =>
        new(description, "Enum", Allowed: allowed);

    public static Rule? Describe(string fieldName) =>
        Known.GetValueOrDefault(fieldName);

    public static string? Validate(string fieldName, string raw)
    {
        if (!Known.TryGetValue(fieldName, out var rule)) return null;
        if (string.IsNullOrWhiteSpace(raw))
            return $"{fieldName} 不能留空：{rule.Description}";
        switch (rule.Kind)
        {
            case "Number":
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                    !double.IsFinite(value) ||
                    rule.Minimum.HasValue && value < rule.Minimum.Value ||
                    rule.Maximum.HasValue && value > rule.Maximum.Value)
                    return $"{fieldName} 必须是有效数字（使用小数点 .），范围 {FormatBounds(rule)}。";
                return null;
            case "Integer":
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ||
                    rule.Minimum.HasValue && integer < rule.Minimum.Value ||
                    rule.Maximum.HasValue && integer > rule.Maximum.Value)
                    return $"{fieldName} 必须是整数，范围 {FormatBounds(rule)}。";
                return null;
            case "Boolean":
                return bool.TryParse(raw, out _) ? null : $"{fieldName} 只能填写 true 或 false。";
            case "Enum":
                return rule.Allowed?.Contains(raw, StringComparer.OrdinalIgnoreCase) == true
                    ? null : $"{fieldName} 必须是 {string.Join(" / ", rule.Allowed ?? [])}。";
        }
        return null;
    }

    private static string FormatBounds(Rule rule)
    {
        var min = rule.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-∞";
        var max = rule.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "+∞";
        return $"{min} ~ {max}";
    }
}
