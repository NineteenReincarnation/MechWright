using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// File-first, schema-neutral operations for one RimWorld XML document.
/// A new file stays genuinely empty until the user adds a Def. No mechanoid
/// hierarchy, combat stats or game-specific template is silently injected.
/// </summary>
public static class DocumentWorksheetEditor
{
    public static DefEntry AppendDefinition(DefDocument document, string type,
        string defName, string label)
    {
        ArgumentNullException.ThrowIfNull(document);
        type = (type ?? "").Trim();
        defName = (defName ?? "").Trim();
        label = (label ?? "").Trim();
        if (!Regex.IsMatch(type, @"^[A-Za-z_][A-Za-z0-9_]*Def$",
                RegexOptions.CultureInvariant))
            throw new ArgumentException("选择一个有效的原生定义类别。");
        if (!Regex.IsMatch(defName, @"^MP_[A-Za-z_][A-Za-z0-9_]*$",
                RegexOptions.CultureInvariant) || defName.Length > 110)
            throw new ArgumentException("定义标识需要以 MP_ 开头，只能使用英文字母、数字和下划线。");
        if (label.Length > 200)
            throw new ArgumentException("显示名称不能超过 200 个字符。");
        if (document.Xml.Root?.Name.LocalName != "Defs")
            throw new InvalidOperationException("只能向以 Defs 为根节点的文件添加游戏定义。");
        if (document.Definitions().Any(x =>
                x.DefName.Equals(defName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("当前 XML 中已经存在这个定义标识。");
        document.Transform(xml =>
        {
            var node = new XElement(type, new XElement("defName", defName));
            if (label.Length > 0) node.Add(new XElement("label", label));
            xml.Root!.Add(node);
        });
        return document.Definitions().Last();
    }

     public static bool CanContainFields(FreeformDefinitionEditor.Node node) =>
        node.IsRoot || node.IsGroup ||
        (node.Value is null or "" && !node.IsIdentity &&
            node.Name is not ("label" or "description"));


    /// <summary>
    /// A user-facing field choice adds its required parents and the field
    /// together as one undoable change; nothing is written until confirmed.
    /// </summary>
    public static void AppendFieldPath(DefDocument document, DefEntry definition,
        string path, string value)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (definition.Element.Document != document.Xml)
            throw new InvalidOperationException("请重新选择当前文件中的定义。");
        var segments = (path ?? "").Split('/');
        if (segments.Length is < 1 or > 8 ||
            segments.Any(s => s.Length is < 1 or > 100))
            throw new ArgumentException("字段路径不正确。");
        foreach (var segment in segments) System.Xml.XmlConvert.VerifyNCName(segment);
        if (segments.Any(s => s == "li") || segments[0] == "defName")
            throw new InvalidOperationException("此类结构请通过添加列表项编辑。");
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096)
            throw new ArgumentException("请先填写该项的值。");
        var ruleError = StandardFieldRules.Validate(segments[^1], value);
        if (ruleError != null) throw new ArgumentException(ruleError);
        document.Transform(_ =>
        {
            var parent = definition.Element;
            foreach (var segment in segments[..^1])
            {
                var next = parent.Element(segment);
                if (next == null)
                {
                    next = new XElement(segment);
                    parent.Add(next);
                }
                else if (!next.HasElements && !string.IsNullOrWhiteSpace(next.Value))
                    throw new InvalidOperationException("不能在已有文字内容的字段中添加子项。");
                parent = next;
            }
            if (parent.Element(segments[^1]) != null)
                throw new InvalidOperationException("当前配置已存在，请直接修改列表里的值。");
            parent.Add(new XElement(segments[^1], value.Trim()));
        });
    }

    public static void SetDisplayLabel(DefDocument document, DefEntry definition, string label)
    {
        ArgumentNullException.ThrowIfNull(document);
        label = (label ?? "").Trim();
        if (label.Length is < 1 or > 200)
            throw new ArgumentException("请输入显示名称（最多 200 字）。");
        if (definition.Element.Document != document.Xml)
            throw new InvalidOperationException("请选择当前文件中的定义。");
        document.Transform(_ =>
        {
            var element = definition.Element.Element("label");
            if (element == null)
                definition.Element.Add(new XElement("label", label));
            else if (!element.HasElements)
                element.Value = label;
            else throw new InvalidOperationException("显示名称不是普通文本，请使用高级编辑。");
        });
    }

    public static string AddField(DefDocument document, DefEntry definition,
        string selectedPath, string name, string value, bool group)
    {
        // A selected value leaf means "add a sibling"; selecting a group
        // means "add inside". No XML path has to be typed by the end user.
        var rows = FreeformDefinitionEditor.Read(document, definition);
        var selected = rows.FirstOrDefault(x => x.Path == selectedPath)
            ?? rows.First();
        var parentPath = CanContainFields(selected)
            ? selected.Path
            : rows.FirstOrDefault(x => x.Path == ParentPath(selected.Path))?.Path
              ?? rows[0].Path;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("请先选择一个字段，或填写自定义字段名称。");
        // Ordinary scalar fields are unique within a container. Repeated
        // list nodes remain supported as native li siblings.
        if (!name.Equals("li", StringComparison.OrdinalIgnoreCase))
        {
            var parent = definition.Element.DescendantsAndSelf()
                .FirstOrDefault(x => DefDocument.GetPath(x) == parentPath);
            if (parent?.Elements(name).Any() == true)
                throw new InvalidOperationException("此处已有“" + name +
                    "”。请直接修改现有字段，或添加列表项。");
        }
        return FreeformDefinitionEditor.AddChild(document, definition, parentPath,
            name.Trim(), value ?? "", group);
    }

    public static string ParentPath(string path)
    {
        var index = path.LastIndexOf('/');
        return index <= 0 ? path : path[..index];
    }

    public static void UpdateValue(DefDocument document, DefEntry definition,
        string path, string value) =>
        FreeformDefinitionEditor.SetValue(document, definition, path, value);

    public static void RemoveField(DefDocument document, DefEntry definition,
        string path) =>
        FreeformDefinitionEditor.RemoveNode(document, definition, path);
}
