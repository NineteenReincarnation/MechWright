using System.Xml;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Direct child <li> collections used by vanilla RimWorld definitions.
/// It edits complete list items as reversibly structured XML, without
/// flattening unknown CompProperties, patches, attributes or comments.
/// </summary>
public static class NativeListEditor
{
    public static readonly string[] CommonContainers =
    [
        "verbs", "tools", "comps", "modExtensions", "weaponTags",
        "apparelTags", "tradeTags", "costList", "recipes", "abilities"
    ];

    public sealed record ListInfo(string Container, int ItemCount, bool Exists)
    {
        public string Display => $"{Container}  ·  {ItemCount} 项" +
            (Exists ? "" : "（尚未创建）");
    }

    public sealed record ItemInfo(string XmlPath, int Index, string Summary, string Source)
    {
        public string Display => $"{Index + 1:00}  {Summary}";
    }

    public static IReadOnlyList<ListInfo> Lists(DefDocument document, DefEntry def)
    {
        RequireAttached(document, def);
        var existing = def.Element.Elements()
            .Where(x => x.Elements("li").Any() ||
                CommonContainers.Contains(x.Name.LocalName, StringComparer.Ordinal))
            .Select(x => x.Name.LocalName);
        return CommonContainers.Concat(existing).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select(name =>
            {
                var parent = def.Element.Element(name);
                return new ListInfo(name, parent?.Elements("li").Count() ?? 0, parent != null);
            }).ToArray();
    }

    public static IReadOnlyList<ItemInfo> Items(
        DefDocument document, DefEntry def, string container)
    {
        var parent = Container(document, def, container);
        return parent?.Elements("li").Select((item, index) =>
            new ItemInfo(DefDocument.GetPath(item), index,
                Summary(item), item.ToString(SaveOptions.DisableFormatting))).ToArray()
            ?? [];
    }

    public static string Add(
        DefDocument document, DefEntry def, string container, string fragment)
    {
        var parent = Container(document, def, container);
        var item = ParseItem(fragment);
        string addedPath = "";
        document.Transform(_ =>
        {
            parent ??= new XElement(container);
            if (parent.Parent == null) def.Element.Add(parent);
            var added = new XElement(item);
            parent.Add(added);
            addedPath = DefDocument.GetPath(added);
        });
        return addedPath;
    }

    public static string Replace(
        DefDocument document, DefEntry def, string container,
        string itemPath, string fragment)
    {
        var item = ResolveItem(document, def, container, itemPath);
        var replacement = ParseItem(fragment);
        string path = "";
        document.Transform(_ =>
        {
            item.ReplaceWith(new XElement(replacement));
            // Same ordinal position and parent; XPath remains stable.
            path = itemPath;
        });
        return path;
    }

    public static string Duplicate(
        DefDocument document, DefEntry def, string container, string itemPath)
    {
        var item = ResolveItem(document, def, container, itemPath);
        string path = "";
        document.Transform(_ =>
        {
            var clone = new XElement(item);
            item.AddAfterSelf(clone);
            path = DefDocument.GetPath(clone);
        });
        return path;
    }

    public static string Move(
        DefDocument document, DefEntry def, string container, string itemPath, int direction)
    {
        if (direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));
        var item = ResolveItem(document, def, container, itemPath);
        var sibling = direction < 0 ? item.ElementsBeforeSelf("li").LastOrDefault()
                                    : item.ElementsAfterSelf("li").FirstOrDefault();
        if (sibling == null) throw new InvalidOperationException("Item is already at the list edge.");
        string path = "";
        document.Transform(_ =>
        {
            item.Remove(); // Detach, do not clone.
            if (direction < 0) sibling.AddBeforeSelf(item);
            else sibling.AddAfterSelf(item);
            path = DefDocument.GetPath(item);
        });
        return path;
    }

    public static void Remove(
        DefDocument document, DefEntry def, string container, string itemPath)
    {
        var item = ResolveItem(document, def, container, itemPath);
        document.Transform(_ => item.Remove());
    }

    private static XElement? Container(DefDocument document, DefEntry def, string container)
    {
        RequireAttached(document, def);
        if (string.IsNullOrWhiteSpace(container) ||
            !System.Text.RegularExpressions.Regex.IsMatch(container,
                @"^[A-Za-z_][A-Za-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid XML list container.");
        var element = def.Element.Element(container);
        if (element != null && element.Elements().Any(x => x.Name.LocalName != "li"))
            throw new InvalidOperationException("Container has non-li XML children; use the raw XML editor.");
        return element;
    }

    private static XElement ResolveItem(
        DefDocument document, DefEntry def, string container, string path)
    {
        var parent = Container(document, def, container) ??
            throw new InvalidOperationException("The requested list does not exist.");
        var item = parent.Elements("li").FirstOrDefault(x => DefDocument.GetPath(x) == path);
        return item ?? throw new InvalidOperationException("Select an existing list item.");
    }

    private static void RequireAttached(DefDocument document, DefEntry def)
    {
        if (def.Element.Document != document.Xml ||
            !ReferenceEquals(def.Element.Parent, document.Xml.Root))
            throw new InvalidOperationException("Definition is not attached to this document.");
    }

    private static XElement ParseItem(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment) || fragment.Length > 65536)
            throw new ArgumentException("List entry must be nonempty XML under 64 KiB.");
        var parsed = DefDocument.Parse("<Defs>" + fragment + "</Defs>");
        var nodes = parsed.Xml.Root!.Nodes().Where(x => x is not XText text ||
            !string.IsNullOrWhiteSpace(text.Value)).ToArray();
        if (nodes.Length != 1 || nodes[0] is not XElement item || item.Name != "li")
            throw new XmlException("List entry must be exactly one <li> element.");
        return item;
    }

    private static string Summary(XElement item)
    {
        var label = item.Element("label")?.Value ??
            item.Element("verbClass")?.Value ??
            item.Element("def")?.Value ??
            item.Element("bodyPart")?.Value ??
            item.Element("thingDef")?.Value ??
            item.Element("compClass")?.Value ??
            (item.HasElements ? item.Elements().First().Name.LocalName : item.Value);
        return label.Length > 80 ? label[..80] + "…" : label;
    }
}
