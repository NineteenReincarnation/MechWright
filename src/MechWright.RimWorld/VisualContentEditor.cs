using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// One reversible, type-aware writer for native scalar options belonging to the
/// three linked standard mechanoid definitions. Never flatten XML lists, replace
/// nested structures, or silently interpret observed XML as the runtime schema.
/// </summary>
public static class VisualContentEditor
{
    public sealed record Option(
        string DefType, string Path, string Label, string Category,
        string Kind, string Suggestion, string Help, string Source,
        bool Verified, string[]? Choices = null)
    {
        public string FieldName => Path.Split('/')[^1];
    }

    public static IReadOnlyList<Option> Common { get; } =
    [
        new("ThingDef","label","显示名称","身份与描述","Text","新机械体","游戏中显示的名字","已验证原生字段",true),
        new("ThingDef","description","描述文字","身份与描述","Text","一种机械体。","游戏中的说明文字","已验证原生字段",true),
        new("ThingDef","statBases/MarketValue","市场价值","基础性能","Number","800","物品的市场价值；不等同于战斗点数","已验证原生字段",true),
        new("ThingDef","statBases/MoveSpeed","移动速度","基础性能","Number","4.7","普通移动速度，单位：格/秒","已验证原生字段",true),
        new("ThingDef","statBases/ArmorRating_Sharp","锐器护甲","基础性能","Number","0.40","0.40 对应 40% 护甲值","已验证原生字段",true),
        new("ThingDef","statBases/ArmorRating_Blunt","钝器护甲","基础性能","Number","0.20","防御钝击伤害","已验证原生字段",true),
        new("ThingDef","statBases/ArmorRating_Heat","热能护甲","基础性能","Number","2","热能护甲值","已验证原生字段",true),
        new("ThingDef","statBases/Mass","质量","基础性能","Number","85","单位：kg","已验证原生字段",true),
        new("ThingDef","statBases/BandwidthCost","控制带宽","机械师与生成","Integer","2","需要 Biotech 的机械师控制带宽","已验证原生字段",true),
        new("ThingDef","race/baseBodySize","身体大小","基础性能","Number","1","不等同于贴图显示大小","已验证原生字段",true),
        new("ThingDef","race/baseHealthScale","身体耐久倍率","基础性能","Number","1","对机体部件生命值的倍率","已验证原生字段",true),
        new("ThingDef","race/mechWeightClass","机体重量等级","基础性能","Enum","Medium","影响原版机械体类别","已验证原生字段",true,["Light","Medium","Heavy","UltraHeavy"]),
        new("PawnKindDef","combatPower","战斗点数 CP","机械师与生成","Number","200","机械体袭击预算的点数","已验证原生字段",true),
        new("PawnKindDef","maxPerGroup","单组数量上限","机械师与生成","Integer","3","机械体一次生成组内的上限","已验证原生字段",true),
        new("PawnKindDef","allowInMechClusters","加入机械集群","机械师与生成","Boolean","true","是否允许出现在标准机械集群中","已验证原生字段",true),
        new("PawnKindDef","controlGroupPortraitZoom","控制组头像缩放","机械师与生成","Number","1","机械师控制列表的头像缩放比例","已验证原生字段",true),
        new("ThingDef","race/needsRest","是否需要休息","机械师与生成","Boolean","false","仅当游戏定义实际支持时有相应作用","已验证原生字段",true),
        new("ThingDef","race/hasGenders","是否有性别","机械师与生成","Boolean","false","原版 RaceProperties 设置","已验证原生字段",true)
    ];

    private static readonly string[] Reserved = ["defName", "race/body", "race", "corePart"];

    public static IReadOnlyList<Option> All(NativeFieldCatalog catalog)
    {
        var results = new List<Option>(Common);
        var seen = Common.Select(x => (x.DefType,x.Path)).ToHashSet();
        foreach (var kind in new[] { "ThingDef", "PawnKindDef" })
        foreach (var field in catalog.For(kind))
        {
            if (!field.CanInsert || field.Path.Contains("li/", StringComparison.Ordinal) ||
                field.Path.StartsWith("li/", StringComparison.Ordinal) ||
                field.Path.Split('/').Length > 5 ||
                field.Path.Length > 140 || field.FieldName == "defName" ||
                field.Path == "race/body" ||
                field.Path == "race" || field.Path == "corePart" ||
                !seen.Add((kind, field.Path))) continue;
            var known = StandardFieldRules.Describe(field.FieldName);
            var observedType = known?.Kind ?? field.ObservedType;
            var kindLabel = observedType switch
            {
                "Boolean" => "Boolean", "Number" => "Number",
                "Integer" => "Integer", "Enum" => "Enum", _ => "Text"
            };
            var category = CategoryOf(kind, field.Path);
            var label = known?.Description ?? FriendlyLabel(field.FieldName);
            results.Add(new Option(kind, field.Path,label,category,kindLabel,
                field.Example.Length > 256 ? field.Example[..256] : field.Example,
                (known?.Hint ?? "此字段从已安装的原版 XML 中发现。") +
                " · 示例不是默认值。", field.SourcePackages,
                known != null, known?.Allowed));
        }
        return results;
    }

    public static string CategoryOf(string type, string path)
    {
        if (path is "label" or "description") return "身份与描述";
        if (path.StartsWith("statBases/", StringComparison.Ordinal) ||
            path.StartsWith("race/base", StringComparison.Ordinal) ||
            path == "race/mechWeightClass") return "基础性能";
        if (type == "PawnKindDef" ||
            path.Contains("Bandwidth", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("rest", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("genders", StringComparison.OrdinalIgnoreCase))
            return "机械师与生成";
        if (path.Contains("graphic", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("sound", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("draw", StringComparison.OrdinalIgnoreCase)) return "外观与音效";
        if (path.Contains("weapon", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("attack", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("verb", StringComparison.OrdinalIgnoreCase))
            return "武器与战斗";
        return "其他原生属性";
    }

    private static string FriendlyLabel(string name) =>
        name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name;

    public static string? Read(MechWorkspace project, string mechDefName, Option field)
    {
        var file = project.FindDefinitionFile(field.DefType, mechDefName);
        var def = file?.Document.Definitions()
            .FirstOrDefault(x => x.Type == field.DefType && x.DefName == mechDefName);
        XElement? cursor = def?.Element;
        foreach (var step in field.Path.Split('/')) cursor = cursor?.Element(step);
        return cursor?.HasElements == false ? cursor.Value : null;
    }

    public static void Apply(MechWorkspace project, string mechDefName, Option field, string value)
    {
        var (doc, def) = Locate(project, mechDefName, field);
        Validate(field,value);
        doc.Transform(_ =>
        {
            var cursor = def.Element;
            foreach (var segment in field.Path.Split('/'))
            {
                var child = cursor.Element(segment);
                if (child == null) { child = new XElement(segment); cursor.Add(child); }
                cursor = child;
            }
            if (cursor.HasElements || cursor.Nodes().Any(x => x is not XText and not XCData))
                throw new InvalidOperationException("该位置已有复杂原生结构，不能用普通字段覆盖。");
            cursor.Value = value.Trim();
        });
    }

    public static void Remove(MechWorkspace project, string mechDefName, Option field)
    {
        var (doc, def) = Locate(project, mechDefName,field);
        var current = def.Element;
        foreach (var step in field.Path.Split('/'))
        {
            var next = current.Element(step);
            if (next == null) return;
            current = next;
        }
        if (current.HasElements || current.Nodes().Any(x => x is not XText and not XCData))
            throw new InvalidOperationException("此字段包含复杂结构，不能通过普通输入删除。");
        doc.Transform(_ => current.Remove());
    }

    private static (DefDocument Doc,DefEntry Def) Locate(MechWorkspace project,
        string mechDefName, Option field)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(field);
        if (field.DefType is not ("ThingDef" or "PawnKindDef") ||
            Reserved.Contains(field.Path) ||
            field.Path.Contains("li", StringComparison.Ordinal) ||
            field.Path.Split('/').Any(x => !Regex.IsMatch(x,
                @"^[a-zA-Z_][a-zA-Z0-9_]*$",RegexOptions.CultureInvariant)))
            throw new ArgumentException("该字段不能通过简单属性编辑器修改。");
        var file = project.FindDefinitionFile(field.DefType,mechDefName)
            ?? throw new InvalidOperationException("找不到当前机械体的相关配置。");
        var def = file.Document.Definitions().Single(x => x.Type == field.DefType &&
            x.DefName == mechDefName);
        return (file.Document,def);
    }

    public static void Validate(Option field,string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096)
            throw new ArgumentException("请填写一个有效值（不超过 4096 字符）。");
        var standard = StandardFieldRules.Validate(field.FieldName,value);
        if (standard != null) throw new ArgumentException(standard);
        if (field.Kind is "Number" or "Integer")
        {
            if (field.Kind == "Integer" ?
                !int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out _) :
                !double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n) ||
                    !double.IsFinite(n))
                throw new ArgumentException("请输入有效的数字。");
        }
        if (field.Kind == "Boolean" && !bool.TryParse(value,out _))
            throw new ArgumentException("请选择“是”或“否”。");
        if (field.Kind == "Enum" && field.Choices is { Length: >0 } choices &&
            !choices.Contains(value,StringComparer.Ordinal))
            throw new ArgumentException("请选择列表中允许的选项。");
    }
}

/// <summary>
/// Read-only, finite catalog of real native list entries observed in installed
/// Core + DLC. Captures exact XML fragments only, not assets or game code.
/// </summary>
public sealed class NativeListTemplateCatalog
{
    public sealed record Template(string DefType,string Container,string Label,
        string FromUnit,string SourcePackage,string XmlFragment)
    {
        public string Detail => FromUnit + " · " + SourcePackage;
    }
    private readonly IReadOnlyList<Template> _all;
    private NativeListTemplateCatalog(IReadOnlyList<Template> items) => _all=items;
    public static NativeListTemplateCatalog Empty { get; } = new([]);
    public IReadOnlyList<Template> All => _all;

    private static readonly HashSet<string> Allowed =
    [
        "verbs", "tools", "comps", "modExtensions", "abilities",
        "weaponTags", "apparelTags", "tradeTags", "costList", "recipes"
    ];
    public static NativeListTemplateCatalog Build(
        IReadOnlyList<VanillaMechanoidLibrary.SourceDef> defs)
    {
        var result = new List<Template>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<(string,string),int>();
        foreach (var source in defs)
        {
            if (source.IsAbstract || source.Name == null ||
                source.Type is not ("ThingDef" or "PawnKindDef")) continue;
            foreach (var group in source.Xml.Elements().Where(x => Allowed.Contains(x.Name.LocalName)))
            foreach (var item in group.Elements("li"))
            {
                var key=(source.Type,group.Name.LocalName);
                if (counts.GetValueOrDefault(key)>=90) continue;
                var fragment=item.ToString(SaveOptions.DisableFormatting);
                if (fragment.Length is < 1 or > 8192 ||
                    !seen.Add(source.Type+"|"+group.Name+"|"+fragment)) continue;
                var label=item.Element("label")?.Value.Trim() ??
                    item.Element("def")?.Value.Trim() ??
                    item.Element("verbClass")?.Value.Trim() ??
                    item.Attribute("Class")?.Value.Trim() ??
                    (item.HasElements ? item.Elements().First().Name.LocalName : item.Value.Trim());
                if (label.Length>90)label=label[..90]+"…";
                result.Add(new Template(source.Type,group.Name.LocalName,label,
                    source.Xml.Element("label")?.Value.Trim()??source.Name,
                    source.Package,fragment));
                counts[key]=counts.GetValueOrDefault(key)+1;
            }
        }
        return new NativeListTemplateCatalog(result);
    }

    public static void Install(MechWorkspace project,string mechDefName,Template sample)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sample);
        if (!Allowed.Contains(sample.Container) ||
            sample.DefType is not ("ThingDef" or "PawnKindDef"))
            throw new InvalidOperationException("此模板不是受支持的原生列表内容。");
        var file=project.FindDefinitionFile(sample.DefType,mechDefName)
            ??throw new InvalidOperationException("当前机械体没有这个原生定义。");
        var def=file.Document.Definitions().Single(x=>x.Type==sample.DefType &&
            x.DefName==mechDefName);
        NativeListEditor.Add(file.Document,def,sample.Container,sample.XmlFragment);
    }
}
