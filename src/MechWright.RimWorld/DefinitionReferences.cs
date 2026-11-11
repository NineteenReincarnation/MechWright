using System.Xml.Linq;
using MechWright.Core;

namespace MechWright.RimWorld;

public enum ReferenceLocation { CurrentDocument, GameCatalog, ExternalUnknown }

public sealed record DefinitionReference(
    string FromType, string FromDefName, string TargetType, string TargetDefName,
    string FieldPath, ReferenceLocation Location, string OriginPackage)
{
    public bool CanNavigateLocally => Location == ReferenceLocation.CurrentDocument;
}

/// <summary>
/// Conservative reference explorer: explicit RimWorld reference paths, no guessing
/// arbitrary string-valued XML elements are def references.
/// Unresolved means "not in this file or loaded catalog", not a game-load error.
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
                    foreach (var group in element.Element("tools")?.Elements("li") ?? [])
                        Add(group.Element("linkedBodyPartsGroup"), "BodyPartGroupDef");
                    foreach (var verb in element.Element("verbs")?.Elements("li") ?? [])
                        Add(verb.Element("defaultProjectile"), "ThingDef");
                    break;
                case "PawnKindDef":
                    Add(element.Element("race"), "ThingDef");
                    foreach (var ability in element.Element("abilities")?.Elements("li") ?? [])
                        Add(ability, "AbilityDef");
                    break;
                case "RecipeDef":
                    foreach (var product in element.Element("products")?.Elements() ?? [])
                        Add(product, "ThingDef", fromElementName: true);
                    break;
                case "BodyDef":
                    foreach (var part in document.BodyParts(entry))
                        Add(part.Xml.Element("def"), "BodyPartDef");
                    break;
            }

            void Add(XElement? field, string targetType, bool fromElementName = false)
            {
                if (field == null) return;
                var target = (fromElementName ? field.Name.LocalName : field.Value).Trim();
                if (string.IsNullOrWhiteSpace(target)) return;
                var key = (targetType, target);
                var place = locals.Contains(key) ? ReferenceLocation.CurrentDocument :
                            resolved.ContainsKey(key) ? ReferenceLocation.GameCatalog :
                            ReferenceLocation.ExternalUnknown;
                var package = place == ReferenceLocation.GameCatalog ? resolved[key] : "";
                result.Add(new DefinitionReference(entry.Type, entry.DefName,
                    targetType, target, DefDocument.GetPath(field), place, package));
            }
        }
        return result;
    }
}
