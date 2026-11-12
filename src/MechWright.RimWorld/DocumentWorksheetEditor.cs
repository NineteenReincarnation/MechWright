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

    public static string AddField(DefDocument document, DefEntry definition,
        string selectedPath, string name, string value, bool group)
    {
        // A selected value leaf means "add a sibling"; selecting a group
        // means "add inside". No XML path has to be typed by the end user.
        var rows = FreeformDefinitionEditor.Read(document, definition);
        var selected = rows.FirstOrDefault(x => x.Path == selectedPath)
            ?? rows.First();
        var parentPath = selected.IsRoot || selected.IsGroup
            ? selected.Path
            : rows.FirstOrDefault(x => x.Path == ParentPath(selected.Path))?.Path
              ?? rows[0].Path;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("请先选择一个字段，或填写自定义字段名称。");
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
