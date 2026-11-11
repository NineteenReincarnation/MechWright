using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using MechWright.Core;

namespace MechWright.RimWorld;

/// <summary>One original XML file and a single editable document tree. Unknown nodes, attributes and comments remain in the tree.</summary>
public sealed class DefDocument
{
    private readonly Stack<Change> _undo = new();
    private readonly Stack<Change> _redo = new();
    private string _original;
    public XDocument Xml { get; private set; }
    public string SourceName { get; }
    public bool IsDirty => _undo.Count != 0;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public const int MaxXmlCharacters = 8_000_000;
    private abstract record Change;
    private sealed record Edit(XElement Element, string OldValue, string NewValue) : Change;
    private sealed record Insert(XElement Element) : Change;
    private sealed record ReplaceDocument(XDocument Before, XDocument After) : Change;

    private DefDocument(XDocument xml, string raw, string sourceName)
    {
        Xml = xml;
        _original = raw;
        SourceName = sourceName;
    }

    public static DefDocument Parse(string source, string sourceName = "Untitled.xml")
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > MaxXmlCharacters)
            throw new InvalidDataException("XML exceeds the maximum allowed document size.");
        using var reader = XmlReader.Create(new StringReader(source),
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxXmlCharacters,
                MaxCharactersFromEntities = 0,
                IgnoreComments = false,
                IgnoreWhitespace = false
            });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        if (document.Root == null)
            throw new XmlException("The XML document does not have a root element.");
        return new DefDocument(document, source, sourceName);
    }

    public static DefDocument CreateEmpty() =>
        Parse("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Defs>\n</Defs>\n");

    public IReadOnlyList<DefEntry> Definitions()
    {
        var root = Xml.Root;
        if (root == null) return Array.Empty<DefEntry>();
        return root.Elements()
            .Where(e => e.Name.LocalName.EndsWith("Def", StringComparison.Ordinal)
                         || e.Name.LocalName == "Patch")
            .Select((e, index) => new DefEntry(e, index))
            .ToList();
    }

    public IReadOnlyList<DefField> Fields(DefEntry definition)
    {
        return definition.Element.DescendantsAndSelf()
            .Where(e => !e.HasElements && e.Nodes().All(n => n is XText or XCData))
            .Select(e => new DefField(e, GetPath(e)))
            .ToList();
    }

    public IReadOnlyList<BodyPartEntry> BodyParts(DefEntry definition)
    {
        if (definition.Type != "BodyDef") return Array.Empty<BodyPartEntry>();
        var core = definition.Element.Element("corePart");
        var entries = new List<BodyPartEntry>();
        if (core != null) AppendParts(core, 0, 1d, entries);
        return entries;
    }

    private static void AppendParts(XElement part, int depth, double cumulative, List<BodyPartEntry> result)
    {
        var relative = ParseCoverage(part);
        var effective = depth == 0 ? 1d : cumulative * relative;
        var name = part.Element("customLabel")?.Value
                   ?? part.Element("def")?.Value ?? "(unnamed)";
        result.Add(new BodyPartEntry(depth, name, relative, effective,
            part.Element("depth")?.Value ?? "Outside",
            part.Element("height")?.Value ?? "Middle",
            part.Element("def")?.Value ?? "", part));
        foreach (var child in part.Element("parts")?.Elements("li") ?? Enumerable.Empty<XElement>())
            AppendParts(child, depth + 1, effective, result);
    }

    private static double ParseCoverage(XElement part)
    {
        var field = part.Element("coverage");
        return field == null ? 1d :
            double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN;
    }

    public void SetField(DefField field, string value)
    {
        if (field.Element.HasElements || !field.Element.Nodes().All(n => n is XText or XCData))
            throw new InvalidOperationException("Only text-only leaf XML fields may be edited directly.");
        if (field.Element.Document != Xml)
            throw new InvalidOperationException("The field is not part of the current document.");
        var before = field.Element.Value;
        if (before == value) return;
        field.Element.Value = value;
        _undo.Push(new Edit(field.Element, before, value));
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var change = _undo.Pop();
        switch (change)
        {
            case Edit field:
                field.Element.Value = field.OldValue;
                break;
            case Insert addition:
                addition.Element.Remove();
                break;
            case ReplaceDocument replacement:
                Xml = replacement.Before;
                break;
        }
        _redo.Push(change);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var change = _redo.Pop();
        switch (change)
        {
            case Edit field:
                field.Element.Value = field.NewValue;
                break;
            case Insert addition:
                Xml.Root!.Add(addition.Element);
                break;
            case ReplaceDocument replacement:
                Xml = replacement.After;
                break;
        }
        _undo.Push(change);
        return true;
    }

    /// <summary>Replace the whole XML tree as one undoable edit, preserving raw and unknown fields.</summary>
    public void ReplaceSource(string source)
    {
        var newDocument = Parse(source, SourceName).Xml; // validates XML and rejects DTD first
        _undo.Push(new ReplaceDocument(Xml, newDocument));
        Xml = newDocument;
        _redo.Clear();
    }

    public void MarkSaved()
    {
        _original = Export();
        _undo.Clear();
        _redo.Clear();
    }

    public string Export()
    {
        if (!IsDirty) return _original;
        var content = Xml.ToString(SaveOptions.DisableFormatting);
        if (Xml.Declaration == null) return content;
        var decl = Xml.Declaration;
        var encoding = decl.Encoding ?? "utf-8";
        var standalone = decl.Standalone == null ? "" : $" standalone=\"{decl.Standalone}\"";
        return $"<?xml version=\"{decl.Version}\" encoding=\"{encoding}\"{standalone}?>" +
               (content.StartsWith("\n", StringComparison.Ordinal) ? "" : "\n") + content;
    }

    public static string GetPath(XElement element)
    {
        var names = new Stack<string>();
        for (var cursor = element; cursor != null; cursor = cursor.Parent)
        {
            var index = cursor.Parent?.Elements(cursor.Name).TakeWhile(x => x != cursor).Count() + 1 ?? 1;
            names.Push($"{cursor.Name.LocalName}[{index}]");
        }
        return "/" + string.Join("/", names);
    }

    public DefEntry AddDefinition(string type, string defName)
    {
        if (Xml.Root?.Name.LocalName != "Defs")
            throw new InvalidOperationException("New definitions can only be added under a <Defs> root.");
        if (type is not ("ThingDef" or "PawnKindDef" or "BodyDef" or "BodyPartDef" or "RecipeDef"))
            throw new ArgumentException("Unsupported starter definition type.", nameof(type));
        if (!Regex.IsMatch(defName, @"^MP_[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
            throw new ArgumentException("The starter template requires a unique MP_-prefixed defName.");
        if (Definitions().Any(x => x.Type == type && x.DefName == defName))
            throw new InvalidOperationException("This definition already exists in the XML document.");

        var e = new XElement(type, new XElement("defName", defName),
            new XElement("label", defName["MP_".Length..].Replace('_', ' ').ToLowerInvariant()));
        if (type == "ThingDef")
        {
            e.SetAttributeValue("ParentName", "BaseMechanoidWalker");
            e.SetAttributeValue("MayRequire", "Ludeon.RimWorld.Biotech");
            e.Add(new XElement("race", new XElement("body", "Pikeman"),
                new XElement("baseBodySize", "1"), new XElement("baseHealthScale", "1"),
                new XElement("mechWeightClass", "Medium")));
        }
        if (type == "PawnKindDef")
        {
            e.SetAttributeValue("ParentName", "BaseMechanoidKind");
            e.SetAttributeValue("MayRequire", "Ludeon.RimWorld.Biotech");
            e.Add(new XElement("race", defName), new XElement("combatPower", "80"));
        }
        if (type == "BodyDef")
            e.Add(new XElement("corePart", new XElement("def", "MechanicalThorax"),
                new XElement("height", "Middle"), new XElement("depth", "Outside")));
        Xml.Root.Add(e);
        _undo.Push(new Insert(e));
        _redo.Clear();
        return Definitions().Last();
    }

    public IReadOnlyList<Diagnostic> Validate() => DefValidation.Validate(this);
}

public sealed class DefEntry(XElement element, int index)
{
    internal XElement Element { get; } = element;
    public int Index { get; } = index;
    public string Type => Element.Name.LocalName;
    public string DefName => Element.Element("defName")?.Value ?? "(unnamed)";
    public string Label => Element.Element("label")?.Value ?? DefName;
    public bool Abstract => string.Equals(Element.Attribute("Abstract")?.Value, "True", StringComparison.OrdinalIgnoreCase);
    public string DisplayName => $"{Type}  ·  {DefName}";
}

public sealed class DefField(XElement element, string path)
{
    internal XElement Element { get; } = element;
    public string Path { get; } = path;
    public string Name => Element.Name.LocalName;
    public string Value => Element.Value;
    public bool HasAttributes => Element.HasAttributes;
    public string DisplayName => $"{Name}  =  {Value}";
}

public sealed record BodyPartEntry(int Depth, string Name, double Coverage,
    double CumulativeCoverage, string DepthLayer, string Height, string PartDef, XElement Xml)
{
    public string IndentedName => new string(' ', Depth * 3) + (Depth == 0 ? "◆ " : "└ ") + Name;
    public string CoverageText => double.IsNaN(Coverage) ? "?" : $"{Coverage:P1}";
    public string EffectiveText => double.IsNaN(CumulativeCoverage) ? "?" : $"{CumulativeCoverage:P2}";
}
