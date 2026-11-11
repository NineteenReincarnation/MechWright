using System.Globalization;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Observed native XML fields from the user's own installed Core/DLC files.
/// These are examples, NOT a complete authoritative C# type schema.
/// Unknown entries and list structures remain accessible in raw XML editing.
/// </summary>
public sealed class NativeFieldCatalog
{
    public sealed record Field(
        string DefType, string Path, string Example,
        string ObservedType, string SourcePackages,
        int Observations, bool CanInsert)
    {
        public string FieldName => Path.Split('/').Last();
        public string Display => Path + " · " + ObservedType;
        public string Description => "在 " + SourcePackages + " 的 XML 中观察到 " +
            Observations.ToString(CultureInfo.InvariantCulture) + " 次 · " +
            (CanInsert ? "可在当前 Def 显式插入" : "复杂列表字段，需要专用编辑器或源码编辑");
    }

    private sealed class Counter
    {
        public string First = "";
        public HashSet<string> Packages = new(StringComparer.Ordinal);
        public int Observations;
        public int Integer;
        public int Number;
        public int Boolean;
        public bool ListMember;

        public void Add(string value, string package, bool list)
        {
            if (Observations == 0) First = value;
            Packages.Add(package);
            Observations++;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                Integer++;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && double.IsFinite(number))
                Number++;
            if (bool.TryParse(value, out _)) Boolean++;
            ListMember |= list;
        }

        public Field ToField(string kind, string path)
        {
            var type = Observations == Boolean ? "Boolean" :
                Observations == Integer ? "Integer" :
                Observations == Number ? "Number" : "Text";
            return new Field(kind, path, First, type,
                string.Join(" / ", Packages.OrderBy(x => x, StringComparer.Ordinal)),
                Observations, !ListMember);
        }
    }

    private readonly IReadOnlyList<Field> _fields;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Field>> _byType;

    private NativeFieldCatalog(IReadOnlyList<Field> fields)
    {
        _fields = fields;
        _byType = fields.GroupBy(x => x.DefType, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Field>)g.ToArray(),
                StringComparer.Ordinal);
    }

    public static NativeFieldCatalog Empty { get; } = new([]);
    public IReadOnlyList<Field> All => _fields;
    public IReadOnlyList<Field> For(string defType) =>
        _byType.TryGetValue(defType, out var fields) ? fields : [];

    public static NativeFieldCatalog Build(
        IReadOnlyList<VanillaMechanoidLibrary.SourceDef> sources,
        CancellationToken token = default)
    {
        var data = new Dictionary<(string Type, string Path), Counter>();
        foreach (var source in sources)
        {
            token.ThrowIfCancellationRequested();
            foreach (var element in source.Xml.Descendants())
            {
                // Fields with child elements represent nested containers, never flat scalars.
                if (element.HasElements || element.Nodes().Any(n => n is not XText and not XCData))
                    continue;
                var value = element.Value.Trim();
                if (value.Length == 0) continue;
                var segments = element.AncestorsAndSelf()
                    .TakeWhile(e => !ReferenceEquals(e, source.Xml))
                    .Reverse().Select(e => e.Name.LocalName).ToArray();
                if (segments.Length == 0) continue;
                var path = string.Join('/', segments);
                var key = (source.Type, path);
                if (!data.TryGetValue(key, out var counter))
                    data[key] = counter = new Counter();
                counter.Add(value, source.Package, segments.Contains("li"));
            }
        }
        return new NativeFieldCatalog(data.Select(x => x.Value.ToField(x.Key.Type, x.Key.Path))
            .OrderBy(x => x.DefType, StringComparer.Ordinal)
            .ThenBy(x => x.Path, StringComparer.Ordinal).ToArray());
    }

    public static void Apply(
        DefDocument document, DefEntry def, Field field, string input)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(field);
        if (def.Element.Document != document.Xml)
            throw new InvalidOperationException("The selected Def comes from an old document snapshot.");
        if (!string.Equals(def.Type, field.DefType, StringComparison.Ordinal))
            throw new ArgumentException("The chosen field belongs to another Def type.");
        if (!field.CanInsert || field.Path.Split('/').Contains("li"))
            throw new InvalidOperationException("List/complex XML fields must be edited in the specialist or raw XML view.");
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("A field needs an explicit value. Empty values must be edited in XML.");
        var standardIssue = StandardFieldRules.Validate(field.FieldName, input);
        if (standardIssue != null)
            throw new ArgumentException(standardIssue);

        document.Transform(_ =>
        {
            var node = def.Element;
            foreach (var segment in field.Path.Split('/'))
            {
                var child = node.Element(segment);
                if (child == null)
                {
                    child = new XElement(segment);
                    node.Add(child);
                }
                node = child;
            }
            if (node.HasElements || node.Nodes().Any(n => n is not XText and not XCData))
                throw new InvalidOperationException("The destination contains complex XML: refusing a destructive overwrite.");
            node.Value = input;
        });
    }
}
