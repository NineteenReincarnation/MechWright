using System.Xml;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// A structural, no-source-code editor for native repeated XML configurations.
/// Supports assembling a list entry one field or nested group at a time,
/// without assuming that a game mechanic exists for arbitrary Class/Def names.
/// </summary>
public static class FreeformModuleEditor
{
    public static readonly string[] CommonContainers =
        ["tools", "verbs", "comps", "abilities", "modExtensions",
         "weaponTags", "costList", "tradeTags", "apparelTags", "recipes"];

    public sealed record Node(string Path, string Label, int Depth,
        string Name, string? Value, bool HasChildren, bool IsRoot, string Attributes)
    {
        public string IndentedLabel => new string(' ', Math.Min(Depth, 12) * 3) + Label;
    }

    public static string AddItem(DefDocument doc, DefEntry def, string container) =>
        NativeListEditor.Add(doc, def, container, "<li />");

    public static IReadOnlyList<Node> Read(DefDocument doc, DefEntry def,
        string container, string itemPath)
    {
        var root = ResolveItem(doc, def, container, itemPath);
        var rows = new List<Node>();
        Append(root, 0, rows);
        return rows;
    }

    private static void Append(XElement element, int depth, List<Node> rows)
    {
        if (rows.Count >= 350) return;
        var label = element.Name.LocalName;
        var value = element.HasElements ? null : element.Value.Trim();
        var attrs = string.Join(" · ", element.Attributes()
            .Where(x => !x.IsNamespaceDeclaration)
            .Select(x => x.Name.LocalName + "=" + x.Value));
        rows.Add(new Node(DefDocument.GetPath(element), label, depth,
            element.Name.LocalName, value, element.HasElements, depth == 0, attrs));
        foreach (var child in element.Elements()) Append(child, depth + 1, rows);
    }

    public static string AddChild(DefDocument doc, DefEntry def,
        string container, string itemPath, string selectedPath,
        string elementName, string value, bool asGroup)
    {
        XmlConvert.VerifyNCName(elementName);
        if (elementName.Length > 100 || elementName == "defName")
            throw new ArgumentException("字段名称不允许使用这个标识。");
        if (value.Length > 4096) throw new ArgumentException("字段内容过长。");
        var root = ResolveItem(doc, def, container, itemPath);
        var parent = root.DescendantsAndSelf().FirstOrDefault(x =>
            DefDocument.GetPath(x) == selectedPath)
            ?? throw new InvalidOperationException("请在配置树中选择一个父节点。");
        if (parent != root && !parent.HasElements &&
            !string.IsNullOrWhiteSpace(parent.Value))
            throw new InvalidOperationException("这个字段已有文字值，不能直接挂载子字段。请先新建一个容器。");
        var node = asGroup ? new XElement(elementName) : new XElement(elementName, value);
        var result = "";
        doc.Transform(_ =>
        {
            parent.Add(node);
            result = DefDocument.GetPath(node);
        });
        return result;
    }

    public static void SetValue(DefDocument doc, DefEntry def,
        string container, string itemPath, string nodePath, string value)
    {
        if (value.Length > 4096) throw new ArgumentException("字段内容过长。");
        var node = ResolveNode(doc, def, container, itemPath, nodePath);
        if (node.HasElements) throw new InvalidOperationException("容器不能直接填写文字，请修改内部字段。");
        var issue = StandardFieldRules.Validate(node.Name.LocalName, value);
        if (issue != null) throw new ArgumentException(issue);
        doc.Transform(_ => node.Value = value);
    }

    public static void SetAttribute(DefDocument doc, DefEntry def,
        string container, string itemPath, string nodePath,
        string attrName, string value)
    {
        XmlConvert.VerifyNCName(attrName);
        if (attrName.Length > 100 || value.Length > 2048)
            throw new ArgumentException("属性名或属性值过长。");
        var node = ResolveNode(doc, def, container, itemPath, nodePath);
        doc.Transform(_ => node.SetAttributeValue(attrName, value));
    }

    public static void RemoveNode(DefDocument doc, DefEntry def,
        string container, string itemPath, string nodePath)
    {
        var node = ResolveNode(doc, def, container, itemPath, nodePath);
        if (node.Name.LocalName == "li" && DefDocument.GetPath(node) == itemPath)
            throw new InvalidOperationException("请用“删除配置项”移除整项。");
        doc.Transform(_ => node.Remove());
    }

    private static XElement ResolveNode(DefDocument doc, DefEntry def,
        string container, string itemPath, string nodePath) =>
        ResolveItem(doc, def, container, itemPath)
            .DescendantsAndSelf().FirstOrDefault(x =>
                DefDocument.GetPath(x) == nodePath)
        ?? throw new InvalidOperationException("节点已改变，请重新选择。");

    private static XElement ResolveItem(DefDocument doc, DefEntry def,
        string container, string itemPath)
    {
        if (def.Element.Document != doc.Xml || def.Type is not ("ThingDef" or "PawnKindDef"))
            throw new InvalidOperationException("只能编辑当前工程的机械体定义。");
        if (!CommonContainers.Contains(container, StringComparer.Ordinal))
            throw new ArgumentException("此列表需要进入高级 XML 模式。");
        var list = def.Element.Element(container)
            ?? throw new InvalidOperationException("列表不存在。");
        return list.Elements("li").FirstOrDefault(x =>
                   DefDocument.GetPath(x) == itemPath)
               ?? throw new InvalidOperationException("配置项不存在或已被移动。");
    }
}
