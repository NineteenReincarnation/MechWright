using System.Xml.Linq;
using MechWright.Core;

namespace MechWright.RimWorld;

public enum ReferenceLocation { CurrentDocument, ProjectFile, GameCatalog, ExternalUnknown }

public sealed record DefinitionReference(
    string FromType, string FromDefName, string TargetType, string TargetDefName,
    string FieldPath, ReferenceLocation Location, string OriginPackage)
{
    public bool CanNavigateLocally => Location == ReferenceLocation.CurrentDocument;
    // An XML condition on this node or an ancestor can exclude the reference
    // from the active Def graph. A game scan proves file presence, not that
    // the required package is active in the user's mod load order.
    public bool IsConditional { get; init; }
    public string ConditionalPackages { get; init; } = "";
}

/// <summary>
/// Explicit native RimWorld Def-reference paths only. Tag strings (weaponTags,
/// tradeTags, class names), XML type names and arbitrary mod fields are not
/// inferred to be DefNames. References supplied by third-party mods remain
/// unresolved, not invalid, until the game can evaluate the full load order.
/// </summary>
public static class DefinitionReferences
{
    public static IReadOnlyList<DefinitionReference> Collect(
        DefDocument document, IReadOnlyCollection<CatalogEntry>? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var locals = document.Definitions()
            .Where(d => d.DefName != "(unnamed)")
            .Select(d => (d.Type, d.DefName))
            .ToHashSet();
        var resolved = (catalog ?? Array.Empty<CatalogEntry>())
            .GroupBy(x => (x.Identity.Type, x.Identity.DefName))
            .ToDictionary(g => g.Key, g => g.First().SourcePackage);
        var result = new List<DefinitionReference>();

        foreach (var entry in document.Definitions())
        {
            var element = entry.Element;
            switch (entry.Type)
            {
                case "ThingDef":
                    Add(element.Element("race")?.Element("body"), "BodyDef");
                    Add(element.Element("projectile")?.Element("damageDef"), "DamageDef");
                    Add(element.Element("recipeMaker")?.Element("researchPrerequisite"),
                        "ResearchProjectDef");
                    Add(element.Element("recipeMaker")?.Element("unfinishedThingDef"), "ThingDef");
                    AddList(element.Element("recipeMaker")?.Element("recipeUsers"), "ThingDef");
                    AddList(element.Element("researchPrerequisites"), "ResearchProjectDef");
                    AddList(element.Element("thingCategories"), "ThingCategoryDef");
                    AddList(element.Element("recipes"), "RecipeDef");
                    AddNamedProducts(element.Element("costList"), "ThingDef");
                    AddFilter(element.Element("recipeMaker")?.Element("defaultIngredientFilter"));
                    AddFilter(element.Element("recipeMaker")?.Element("fixedIngredientFilter"));
                    foreach (var tool in element.Element("tools")?.Elements("li") ?? [])
                    {
                        Add(tool.Element("linkedBodyPartsGroup"), "BodyPartGroupDef");
                        AddList(tool.Element("capacities"), "ToolCapacityDef");
                    }
                    foreach (var verb in element.Element("verbs")?.Elements("li") ?? [])
                    {
                        Add(verb.Element("defaultProjectile"), "ThingDef");
                        Add(verb.Element("soundCast"), "SoundDef");
                        Add(verb.Element("soundCastTail"), "SoundDef");
                    }
                    break;

                case "PawnKindDef":
                    Add(element.Element("race"), "ThingDef");
                    Add(element.Element("defaultFactionType"), "FactionDef");
                    AddList(element.Element("abilities"), "AbilityDef");
                    break;

                case "RecipeDef":
                    Add(element.Element("researchPrerequisite"), "ResearchProjectDef");
                    AddList(element.Element("researchPrerequisites"), "ResearchProjectDef");
                    AddList(element.Element("recipeUsers"), "ThingDef");
                    AddNamedProducts(element.Element("products"), "ThingDef");
                    Add(element.Element("soundWorking"), "SoundDef");
                    Add(element.Element("effectWorking"), "EffecterDef");
                    Add(element.Element("workSpeedStat"), "StatDef");
                    foreach (var ingredient in element.Element("ingredients")?.Elements("li") ?? [])
                        AddFilter(ingredient.Element("filter"));
                    AddFilter(element.Element("fixedIngredientFilter"));
                    AddFilter(element.Element("defaultIngredientFilter"));
                    break;

                case "BodyDef":
                    foreach (var part in document.BodyParts(entry))
                        Add(part.Xml.Element("def"), "BodyPartDef");
                    break;

                case "FactionDef":
                    Add(element.Element("basicMemberKind"), "PawnKindDef");
                    foreach (var maker in element.Element("pawnGroupMakers")?.Elements("li") ?? [])
                    {
                        Add(maker.Element("kindDef"), "PawnGroupKindDef");
                        foreach (var option in maker.Element("options")?.Elements("li") ?? [])
                            Add(option.Element("kind"), "PawnKindDef");
                        foreach (var option in maker.Element("traders")?.Elements("li") ?? [])
                            Add(option.Element("kind"), "PawnKindDef");
                    }
                    break;

                case "DamageDef":
                    Add(element.Element("hediff"), "HediffDef");
                    Add(element.Element("hediffSkin"), "HediffDef");
                    Add(element.Element("hediffSolid"), "HediffDef");
                    Add(element.Element("soundExplosion"), "SoundDef");
                    break;

                case "ResearchProjectDef":
                    AddList(element.Element("prerequisites"), "ResearchProjectDef");
                    break;
            }

            void AddList(XElement? group, string targetType)
            {
                if (group == null) return;
                foreach (var item in group.Elements("li")) Add(item, targetType);
            }

            void AddNamedProducts(XElement? group, string targetType)
            {
                if (group == null) return;
                foreach (var item in group.Elements())
                    if (item.Name.LocalName != "li")
                        Add(item, targetType, fromElementName: true);
            }

            void AddFilter(XElement? filter)
            {
                if (filter == null) return;
                AddList(filter.Element("thingDefs"), "ThingDef");
                AddList(filter.Element("disallowedThingDefs"), "ThingDef");
                AddList(filter.Element("categories"), "ThingCategoryDef");
                AddList(filter.Element("disallowedCategories"), "ThingCategoryDef");
            }

            void Add(XElement? field, string targetType, bool fromElementName = false)
            {
                if (field == null || field.HasElements) return;
                var target = (fromElementName ? field.Name.LocalName : field.Value).Trim();
                if (target.Length == 0) return;

                var key = (targetType, target);
                var place = locals.Contains(key) ? ReferenceLocation.CurrentDocument :
                            resolved.ContainsKey(key) ? ReferenceLocation.GameCatalog :
                            ReferenceLocation.ExternalUnknown;
                var package = place == ReferenceLocation.GameCatalog ? resolved[key] : "";
                var conditions = field.AncestorsAndSelf()
                    .SelectMany(e => new[] { e.Attribute("MayRequire")?.Value,
                                            e.Attribute("MayRequireAnyOf")?.Value })
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .SelectMany(value => value!.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                result.Add(new DefinitionReference(entry.Type, entry.DefName,
                    targetType, target, DefDocument.GetPath(field), place, package)
                {
                    IsConditional = conditions.Length > 0,
                    ConditionalPackages = string.Join(", ", conditions)
                });
            }
        }
        return result;
    }
}
