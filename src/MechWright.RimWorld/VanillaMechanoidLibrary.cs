using System.Globalization;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// A conservative read-only reference to mechanoids in locally installed
/// RimWorld Core + DLC XML. XML parent templates are consulted for scalar fields.
/// PatchOperations, C# defaults, load-order changes and game-side runtime stat
/// modifiers are intentionally NOT evaluated. No vanilla content is shipped.
/// </summary>
public sealed class VanillaMechanoidLibrary
{
    public sealed record SourceDef(
        string Type, string? Name, string? TemplateName, string? ParentName,
        string Package, string FilePath, XElement Xml)
    {
        public bool IsAbstract =>
            string.Equals(Xml.Attribute("Abstract")?.Value, "True",
                StringComparison.OrdinalIgnoreCase);
    }

    public sealed record Fact(
        string Label, string? Value, string XmlPath,
        string? SourceTemplate, string? SourcePackage)
    {
        public string Display => Value ?? "未在 XML 中明确找到";
        public string Provenance => SourceTemplate == null
            ? (SourcePackage == null ? "未解析" : "显式 · " + SourcePackage)
            : "模板继承 · " + SourceTemplate + " / " + SourcePackage;
    }

    public sealed record BodyPart(string Name, int Level, string Coverage,
        string Height, string Depth);

    public sealed record RelatedDef(
        string Label, string DefName, string Description, string Package,
        string SourceFile);

    public sealed record Profile(
        string DefName, string Label, string Description,
        string Package, string SourceFile, string ParentTemplate,
        IReadOnlyList<Fact> Fields, IReadOnlyList<RelatedDef> Kinds,
        IReadOnlyList<RelatedDef> WeaponCandidates,
        IReadOnlyList<RelatedDef> Recipes,
        IReadOnlyList<BodyPart> BodyParts,
        string BodyDefName, string WeightClass,
        string OriginalXml, IReadOnlyList<string> Notes)
    {
        public string Summary => Label + "  /  " + DefName;
        public string Origin => Package + " · " + SourceFile;
    }

    private static readonly (string Label, string Path, bool Kind)[] Fields =
    [
        ("战斗点数 CP", "combatPower", true),
        ("单组数量上限", "maxPerGroup", true),
        ("加入机械集群", "allowInMechClusters", true),
        ("武器生成标签", "weaponTags", true),
        ("移动速度", "statBases/MoveSpeed", false),
        ("锐器护甲", "statBases/ArmorRating_Sharp", false),
        ("钝器护甲", "statBases/ArmorRating_Blunt", false),
        ("热能护甲", "statBases/ArmorRating_Heat", false),
        ("质量", "statBases/Mass", false),
        ("市场价值", "statBases/MarketValue", false),
        ("机械控制带宽", "statBases/BandwidthCost", false),
        ("控制接管时间", "statBases/ControlTakingTime", false),
        ("身体大小", "race/baseBodySize", false),
        ("生命值倍率", "race/baseHealthScale", false),
        ("重量等级", "race/mechWeightClass", false),
        ("身体定义", "race/body", false),
        ("主要思维树", "race/thinkTreeMain", false),
        ("机体类别", "race/intelligence", false)
    ];

    private readonly IReadOnlyList<Profile> _entries;
    private VanillaMechanoidLibrary(IReadOnlyList<Profile> entries) => _entries = entries;
    public static VanillaMechanoidLibrary Empty { get; } = new([]);
    public IReadOnlyList<Profile> Entries => _entries;
    public int Count => _entries.Count;

    public static VanillaMechanoidLibrary Build(
        IReadOnlyList<SourceDef> sources, CancellationToken token = default)
    {
        var templates = sources.Where(s => !string.IsNullOrWhiteSpace(s.TemplateName))
            .GroupBy(s => (s.Type, s.TemplateName!))
            .ToDictionary(g => g.Key, g => g.Last());
        var defs = sources.Where(s => !string.IsNullOrWhiteSpace(s.Name) && !s.IsAbstract)
            .GroupBy(s => (s.Type, s.Name!))
            .ToDictionary(g => g.Key, g => g.Last());

        IEnumerable<SourceDef> Parents(SourceDef def)
        {
            var visited = new HashSet<(string, string)>();
            var current = def;
            for (var i = 0; i < 48; i++)
            {
                yield return current;
                if (string.IsNullOrWhiteSpace(current.ParentName)) yield break;
                var key = (current.Type, current.ParentName);
                if (!visited.Add(key) || !templates.TryGetValue(key, out current!)) yield break;
            }
        }

        Fact Read(SourceDef? source, string label, string path)
        {
            if (source == null)
                return new Fact(label, null, path, null, null);
            foreach (var node in Parents(source))
            {
                var location = node.Xml;
                var segments = path.Split('/');
                var blocked = false;
                for (int index = 0; index < segments.Length; index++)
                {
                    var container = location.Element(segments[index]);
                    if (container == null) { location = null!; break; }
                    if (index != segments.Length - 1 && string.Equals(
                            container.Attribute("Inherit")?.Value, "False",
                            StringComparison.OrdinalIgnoreCase))
                        blocked = true;
                    location = container;
                }
                if (location != null)
                {
                    var content = location.HasElements
                        ? string.Join(", ", location.Elements().Select(x => x.Value.Trim())
                            .Where(x => x.Length != 0))
                        : location.Value.Trim();
                    if (!string.IsNullOrEmpty(content))
                        return new Fact(label, content, path,
                            ReferenceEquals(node, source) ? null :
                                node.TemplateName ?? node.Name ?? node.ParentName,
                            node.Package);
                    if (location.HasElements && !string.IsNullOrWhiteSpace(location.Value))
                        return new Fact(label, location.Value.Trim(), path,
                            node.TemplateName, node.Package);
                }
                if (blocked) break;
            }
            return new Fact(label, null, path, null, null);
        }

        IReadOnlyList<string> Tags(SourceDef source)
        {
            var accumulated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in Parents(source))
            {
                var list = node.Xml.Element("weaponTags");
                if (list == null) continue;
                foreach (var tag in list.Elements("li"))
                    if (!string.IsNullOrWhiteSpace(tag.Value)) accumulated.Add(tag.Value.Trim());
                if (string.Equals(list.Attribute("Inherit")?.Value,
                    "False", StringComparison.OrdinalIgnoreCase)) break;
            }
            return accumulated.ToArray();
        }

        var kinds = defs.Values.Where(s => s.Type == "PawnKindDef").ToList();
        var kindsByRace = kinds
            .Select(k => (Kind: k, Race: Read(k, "race", "race").Value))
            .Where(x => !string.IsNullOrWhiteSpace(x.Race))
            .GroupBy(x => x.Race!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Kind).ToArray(),
                StringComparer.Ordinal);
        var recipesByProduct = new Dictionary<string, List<SourceDef>>(StringComparer.Ordinal);
        foreach (var recipe in defs.Values.Where(s => s.Type == "RecipeDef"))
        {
            token.ThrowIfCancellationRequested();
            foreach (var product in recipe.Xml.Element("products")?.Elements() ?? [])
            {
                var name = product.Name.LocalName;
                if (!recipesByProduct.TryGetValue(name, out var productRecipes))
                    recipesByProduct[name] = productRecipes = [];
                productRecipes.Add(recipe);
            }
        }

        var actualThings = defs.Values.Where(s => s.Type == "ThingDef").ToList();
        var weaponTags = new Dictionary<string, List<SourceDef>>(StringComparer.Ordinal);
        foreach (var thing in actualThings)
        {
            token.ThrowIfCancellationRequested();
            foreach (var tag in Tags(thing))
            {
                if (!weaponTags.TryGetValue(tag, out var matches))
                    weaponTags[tag] = matches = [];
                matches.Add(thing);
            }
        }

        string RecipeDescription(SourceDef recipe)
        {
            var ingredients = recipe.Xml.Element("ingredients")?.Elements("li")
                .Select(e =>
                {
                    var count = e.Element("count")?.Value.Trim() ?? "?";
                    var types = e.Element("filter")?.Element("thingDefs")?.Elements("li")
                        .Select(x => x.Value.Trim()).Where(x => x.Length != 0).ToArray() ?? [];
                    return (types.Length > 0 ? string.Join("/", types) : "混合材料") + " ×" + count;
                }).ToArray() ?? [];
            var cycles = Read(recipe, "", "gestationCycles").Value;
            var tech = Read(recipe, "", "researchPrerequisite").Value;
            return string.Join(" · ", new[]
            {
                cycles == null ? null : "培育周期 " + cycles,
                tech == null ? null : "科技 " + tech,
                ingredients.Length == 0 ? null : string.Join(" + ", ingredients)
            }.Where(x => x != null));
        }

        string WeaponDescription(SourceDef weapon, string matchingTag)
        {
            var verb = Parents(weapon)
                .Select(x => x.Xml.Element("verbs")?.Elements("li").FirstOrDefault())
                .FirstOrDefault(x => x != null);
            var info = new List<string> { "标签 " + matchingTag };
            if (verb?.Element("range")?.Value is { Length: > 0 } range)
                info.Add("射程 " + range);
            if (verb?.Element("burstShotCount")?.Value is { Length: > 0 } burst)
                info.Add("连发 " + burst);
            if (verb?.Element("defaultProjectile")?.Value is { Length: > 0 } projectile)
            {
                info.Add("弹丸 " + projectile);
                if (defs.TryGetValue(("ThingDef", projectile), out var bullet))
                {
                    var damage = Read(bullet, "", "projectile/damageAmountBase").Value;
                    var ap = Read(bullet, "", "projectile/armorPenetrationBase").Value;
                    if (damage != null) info.Add("基础伤害 " + damage);
                    if (ap != null) info.Add("穿甲 " + ap);
                }
            }
            return string.Join(" · ", info);
        }

        RelatedDef Related(SourceDef source, string? description = null) =>
            new(source.Xml.Element("label")?.Value.Trim() ?? source.Name ?? "",
                source.Name ?? "", description ??
                source.Xml.Element("description")?.Value.Trim() ?? "", source.Package,
                source.FilePath);

        var profiles = new List<Profile>();
        foreach (var thing in actualThings)
        {
            token.ThrowIfCancellationRequested();
            if (thing.IsAbstract || string.IsNullOrWhiteSpace(thing.Name)) continue;
            var ancestors = Parents(thing).ToList();
            var isMech = ancestors.Any(x =>
                x.TemplateName?.Contains("Mechanoid", StringComparison.OrdinalIgnoreCase) == true)
                || Read(thing, "", "race/thinkTreeMain").Value == "Mechanoid";
            var linkedKinds = kindsByRace.GetValueOrDefault(thing.Name)
                ?? Array.Empty<SourceDef>();
            if (!isMech)
                isMech = linkedKinds.Any(k => Parents(k).Any(a =>
                    a.TemplateName?.Contains("MechanoidKind", StringComparison.OrdinalIgnoreCase) == true));
            if (!isMech) continue;

            var representativeKind = linkedKinds.FirstOrDefault();
            var collected = Fields
                .Select(f => Read(f.Kind ? representativeKind : thing, f.Label, f.Path))
                .ToArray();
            var body = collected.First(f => f.XmlPath == "race/body").Value ?? "";
            var weight = collected.First(f => f.XmlPath == "race/mechWeightClass").Value ?? "未在 XML 中定义";

            var recipes = (recipesByProduct.GetValueOrDefault(thing.Name) ?? [])
                .Select(s => Related(s, RecipeDescription(s))).ToList();
            var weapons = new Dictionary<string, RelatedDef>(StringComparer.Ordinal);
            foreach (var kind in linkedKinds)
            foreach (var tag in Tags(kind))
            {
                if (!weaponTags.TryGetValue(tag, out var matches)) continue;
                foreach (var weapon in matches)
                {
                    if (weapon.Name is not { } weaponName) continue;
                    weapons.TryAdd(weaponName, Related(weapon, WeaponDescription(weapon, tag)));
                }
            }

            var parts = new List<BodyPart>();
            if (!string.IsNullOrWhiteSpace(body) &&
                defs.TryGetValue(("BodyDef", body), out var bodyDef))
            {
                void Append(XElement part, int depth)
                {
                    if (depth > 24 || parts.Count > 1000) return;
                    parts.Add(new BodyPart(part.Element("customLabel")?.Value ??
                            part.Element("def")?.Value ?? "(未知部件)",
                        depth, part.Element("coverage")?.Value ?? "（未显式设置）",
                        part.Element("height")?.Value ?? "（未显式设置）",
                        part.Element("depth")?.Value ?? "（未显式设置）"));
                    foreach (var sub in part.Element("parts")?.Elements("li") ?? [])
                        Append(sub, depth + 1);
                }
                if (bodyDef.Xml.Element("corePart") is { } core) Append(core, 0);
            }

            var notes = new List<string>
            {
                "从本地原版 XML 读取；仅追溯普通 ParentName 模板和显式字段，不执行 PatchOperation。",
                "武器列表是匹配 PawnKind weaponTags 的候选集合，不代表游戏必然装备全部武器。",
                "运行时 C# 默认值、特效及数值修正须在 RimWorld 内另行验证。"
            };
            if (linkedKinds.Length == 0)
                notes.Add("未找到直接关联的 PawnKindDef；相关 CP 与武器信息可能缺失。");
            if (parts.Count == 0)
                notes.Add("未在已扫描的 BodyDef 中找到完整部件树；可能来自未加载资源或模板。");

            profiles.Add(new Profile(
                thing.Name, Read(thing, "标签", "label").Value ?? thing.Name,
                Read(thing, "描述", "description").Value ?? "",
                thing.Package, thing.FilePath, thing.ParentName ?? "",
                collected,
                linkedKinds.Select(k => Related(k, "PawnKind / CP " +
                    (Read(k, "", "combatPower").Value ?? "未解析"))).ToList(),
                weapons.Values.OrderBy(w => w.DefName).ToList(),
                recipes,
                parts, body, weight,
                thing.Xml.ToString(SaveOptions.DisableFormatting), notes));
        }
        return new VanillaMechanoidLibrary(profiles
            .OrderBy(x => x.Package).ThenBy(x => x.Label).ToList());
    }
}
