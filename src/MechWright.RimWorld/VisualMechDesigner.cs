using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Clickable, non-XML mechanoid configuration presets. The designer deliberately
/// uses only native, transparent Def fields; no custom combat logic is invented.
/// All written definitions stay editable in the existing XML document history.
/// </summary>
public static class VisualMechDesigner
{
    public sealed record ChassisPreset(
        string Key, string Title, string Description, string Symbol,
        double CombatPower, double BodySize, double HealthScale,
        double MoveSpeed, double Mass, double SharpArmor,
        double BluntArmor, double HeatArmor, int BandwidthCost,
        string WeightClass);

    public static IReadOnlyList<ChassisPreset> Chassis { get; } =
    [
        new("scout", "侦察机体", "轻装、高速，适合前出侦察", "◇",
            110, .75, .85, 5.8, 36, .25, .15, 2, 1, "Light"),
        new("general", "标准机体", "平衡装甲与机动性的通用机械体", "⬡",
            200, 1, 1, 4.7, 85, .4, .2, 2, 2, "Medium"),
        new("assault", "强攻机体", "强化前线持续作战能力", "◆",
            420, 1.65, 1.45, 3.9, 130, .6, .4, 2, 3, "Medium"),
        new("siege", "重装机体", "高生存能力，机动性较低", "▣",
            700, 2.4, 2.1, 2.9, 260, .85, .65, 2, 5, "Heavy")
    ];

    public sealed record BasicSettings(double CombatPower, double BodySize, double HealthScale,
        double MoveSpeed, double Mass, double SharpArmor, double BluntArmor,
        double HeatArmor, int BandwidthCost, string WeightClass)
    {
        public static BasicSettings From(ChassisPreset p) =>
            new(p.CombatPower, p.BodySize, p.HealthScale, p.MoveSpeed, p.Mass,
                p.SharpArmor, p.BluntArmor, p.HeatArmor, p.BandwidthCost, p.WeightClass);
    }

    public sealed record MeleePreset(
        string Key, string Title, string Description, string Symbol,
        string Capacity, double Power, double Cooldown);

    public static IReadOnlyList<MeleePreset> Melee { get; } =
    [
        new("blunt", "撞击", "以冲撞或钝击伤害近身敌人", "●", "Blunt", 8, 2.5),
        new("cut", "切割", "采用机械刃实施近战切割", "◢", "Cut", 12, 2.0)
    ];

    private static string Format(double value) => value.ToString("0.########",
        CultureInfo.InvariantCulture);

    public static void ApplyBasic(MechWorkspace project, string mechDefName, BasicSettings values)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(values);
        ValidateNumber("战斗点数", values.CombatPower, 0, 100000);
        ValidateNumber("身体大小", values.BodySize, .01, 100);
        ValidateNumber("生命倍率", values.HealthScale, .01, 100);
        ValidateNumber("移动速度", values.MoveSpeed, 0, 50);
        ValidateNumber("质量", values.Mass, 0, 100000);
        ValidateNumber("锐器护甲", values.SharpArmor, 0, 20);
        ValidateNumber("钝器护甲", values.BluntArmor, 0, 20);
        ValidateNumber("热能护甲", values.HeatArmor, 0, 20);
        if (values.BandwidthCost is < 0 or > 1000)
            throw new ArgumentException("控制带宽超出可用范围。");
        if (values.WeightClass is not ("Light" or "Medium" or "Heavy" or "UltraHeavy"))
            throw new ArgumentException("无效的机械体重量等级。");

        var thing = Find(project, "ThingDef", mechDefName);
        var kind = Find(project, "PawnKindDef", mechDefName);
        // Preflight before modifying either document.
        foreach (var (node, path) in new[]
        {
            (thing.Element, "statBases/MoveSpeed"),
            (thing.Element, "statBases/Mass"),
            (thing.Element, "statBases/ArmorRating_Sharp"),
            (thing.Element, "statBases/ArmorRating_Blunt"),
            (thing.Element, "statBases/ArmorRating_Heat"),
            (thing.Element, "statBases/BandwidthCost"),
            (thing.Element, "race/baseBodySize"),
            (thing.Element, "race/baseHealthScale"),
            (thing.Element, "race/mechWeightClass"),
            (kind.Element, "combatPower")
        }) EnsureEditable(node, path);

        var thingDoc = thing.Element.Document is { } td
            ? project.FindDefinitionFile("ThingDef", mechDefName)!.Document
            : throw new InvalidOperationException("ThingDef no longer attached.");
        var kindDoc = project.FindDefinitionFile("PawnKindDef", mechDefName)!.Document;
        thingDoc.Transform(_ =>
        {
            Set(thing.Element, "statBases/MoveSpeed", Format(values.MoveSpeed));
            Set(thing.Element, "statBases/Mass", Format(values.Mass));
            Set(thing.Element, "statBases/ArmorRating_Sharp", Format(values.SharpArmor));
            Set(thing.Element, "statBases/ArmorRating_Blunt", Format(values.BluntArmor));
            Set(thing.Element, "statBases/ArmorRating_Heat", Format(values.HeatArmor));
            Set(thing.Element, "statBases/BandwidthCost", values.BandwidthCost.ToString(CultureInfo.InvariantCulture));
            Set(thing.Element, "race/baseBodySize", Format(values.BodySize));
            Set(thing.Element, "race/baseHealthScale", Format(values.HealthScale));
            Set(thing.Element, "race/mechWeightClass", values.WeightClass);
        });
        kindDoc.Transform(_ =>
            Set(kind.Element, "combatPower", Format(values.CombatPower)));
    }

    public static void AddMelee(DefDocument document, DefEntry definition,
        MeleePreset preset, double damage, double cooldown)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!Melee.Contains(preset)) throw new ArgumentException("未知近战预设。");
        ValidateNumber("近战伤害", damage, .1, 10000);
        ValidateNumber("攻击冷却", cooldown, .1, 3600);
        if (definition.Type != "ThingDef")
            throw new ArgumentException("近战工具必须写入机械体 ThingDef。");
        var tool = new XElement("li",
            new XElement("label", preset.Title),
            new XElement("capacities", new XElement("li", preset.Capacity)),
            new XElement("power", Format(damage)),
            new XElement("cooldownTime", Format(cooldown)));
        NativeListEditor.Add(document, definition, "tools",
            tool.ToString(SaveOptions.DisableFormatting));
    }

    public static void ApplyBodyTemplate(DefDocument document, DefEntry destination,
        XElement sourceBodyDef)
    {
        ArgumentNullException.ThrowIfNull(sourceBodyDef);
        if (destination.Type != "BodyDef" || sourceBodyDef.Name != "BodyDef")
            throw new ArgumentException("源和目标必须是 BodyDef。");
        var core = sourceBodyDef.Element("corePart")
            ?? throw new ArgumentException("原版 BodyDef 没有 corePart。");
        if (core.Descendants().Count() > 4000)
            throw new ArgumentException("身体结构过大，不能使用可视化克隆。");
        if (core.Element("def") == null)
            throw new ArgumentException("源身体结构的根部件缺少 DefName。");
        if (!ReferenceEquals(destination.Element.Document, document.Xml))
            throw new InvalidOperationException("目标 BodyDef 已失效。");
        var clone = new XElement(core);
        document.Transform(_ =>
        {
            var existing = destination.Element.Element("corePart");
            if (existing == null) destination.Element.Add(clone);
            else existing.ReplaceWith(clone);
        });
    }

    public static void SetPawnGraphic(DefDocument document, DefEntry kind,
        string texturePath, double drawSize)
    {
        if (kind.Type != "PawnKindDef" || kind.Element.Document != document.Xml)
            throw new ArgumentException("需要选择当前工程的机械体 PawnKindDef。");
        if (string.IsNullOrWhiteSpace(texturePath) ||
            !Regex.IsMatch(texturePath, @"^[A-Za-z0-9_/-]+$", RegexOptions.CultureInvariant)
            || texturePath.StartsWith('/') || texturePath.Contains(".."))
            throw new ArgumentException("贴图路径只能包含 Mod 内部的相对素材路径。");
        ValidateNumber("显示大小", drawSize, .1, 20);
        document.Transform(_ =>
        {
            var lifeStages = kind.Element.Element("lifeStages");
            if (lifeStages == null)
            {
                lifeStages = new XElement("lifeStages");
                kind.Element.Add(lifeStages);
            }
            var stage = lifeStages.Elements("li").FirstOrDefault();
            if (stage == null) { stage = new XElement("li"); lifeStages.Add(stage); }
            var graphic = stage.Element("bodyGraphicData");
            if (graphic == null) { graphic = new XElement("bodyGraphicData"); stage.Add(graphic); }
            graphic.SetElementValue("texPath", texturePath);
            graphic.SetElementValue("graphicClass", "Graphic_Multi");
            graphic.SetElementValue("drawSize", Format(drawSize));
        });
    }

    public static string? CurrentString(MechWorkspace project, string type,
        string name, string path)
    {
        var file = project.FindDefinitionFile(type, name);
        if (file == null) return null;
        var def = file.Document.Definitions().FirstOrDefault(x => x.Type == type && x.DefName == name);
        XElement? node = def?.Element;
        foreach (var part in path.Split('/')) node = node?.Element(part);
        return node?.Value.Trim();
    }

    private static DefEntry Find(MechWorkspace project, string type, string name)
    {
        var file = project.FindDefinitionFile(type, name) ??
            throw new InvalidOperationException($"工程中没有 {type}：{name}。");
        return file.Document.Definitions().Single(x => x.Type == type && x.DefName == name);
    }

    private static void ValidateNumber(string label, double number, double min, double max)
    {
        if (!double.IsFinite(number) || number < min || number > max)
            throw new ArgumentException($"{label} 必须在 {min} 到 {max} 之间。");
    }

    private static void EnsureEditable(XElement element, string path)
    {
        XElement? current = element;
        foreach (var segment in path.Split('/')) current = current?.Element(segment);
        if (current != null && (current.HasElements ||
            current.Nodes().Any(x => x is not XText and not XCData)))
            throw new InvalidOperationException("字段有复杂 XML 内容，不能通过预设覆盖：" + path);
    }

    private static void Set(XElement element, string path, string value)
    {
        var current = element;
        foreach (var part in path.Split('/'))
        {
            var next = current.Element(part);
            if (next == null) { next = new XElement(part); current.Add(next); }
            current = next;
        }
        current.Value = value;
    }
}
