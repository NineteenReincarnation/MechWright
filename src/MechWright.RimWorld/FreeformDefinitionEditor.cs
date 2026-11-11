using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Blank native Def construction and recursive visual node editing.
/// No template, assumed C# worker, or fake gameplay logic is generated.
/// Raw unfamiliar fields are retained, and edits are undoable XML transforms.
/// </summary>
public static class FreeformDefinitionEditor
{
    public sealed record DefType(string Type,string Label,string Category);
    public static IReadOnlyList<DefType> CommonTypes { get; } =
    [
        new("ThingDef","物品／种族／武器／弹丸","常用定义"),
        new("BodyPartDef","身体部件定义","身体"),
        new("BodyPartGroupDef","身体部件组","身体"),
        new("BodyDef","身体结构","身体"),
        new("PawnKindDef","角色生成与种类","生成"),
        new("RecipeDef","机械培育／制造配方","配方"),
        new("AbilityDef","能力","能力"),
        new("HediffDef","健康状态","能力"),
        new("DamageDef","伤害类型","战斗"),
        new("EffecterDef","特效","外观与音效"),
        new("SoundDef","音效","外观与音效"),
        new("JobDef","工作","其他"),
        new("StatDef","属性定义","其他")
    ];

    public sealed record Node(string Path,int Depth,string Name,string? Value,
        string Attributes,bool IsRoot,bool IsIdentity,bool IsGroup)
    {
        public string Display => new string(' ',Math.Min(Depth,18)*3)+Name+
            (Value is {Length:>0} v ? "  =  "+v : "") +
            (Attributes.Length>0?"  ["+Attributes+"]":"");
    }
    public sealed record Definition(string DefType,string DefName,string Label,
        string FilePath)
    {
        public string Display => Label+" · "+DefType;
    }

    public static IReadOnlyList<Definition> List(MechWorkspace workspace)
    {
        return workspace.Files.SelectMany(f => f.Document.Definitions()
            .Where(d=>d.DefName.StartsWith("MP_",StringComparison.Ordinal))
            .Select(d=>new Definition(d.Type,d.DefName,d.Label,f.RelativePath)))
            .OrderBy(x=>x.DefType,StringComparer.Ordinal).ThenBy(x=>x.DefName,
                StringComparer.Ordinal).ToArray();
    }

    public static string Create(MechWorkspace project,string defType,string defName,
        string label)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!Regex.IsMatch(defType,@"^[A-Za-z_][A-Za-z0-9_]*Def$",
            RegexOptions.CultureInvariant))
            throw new ArgumentException("定义类型必须是受游戏识别的 Def 类型名。");
        if (!Regex.IsMatch(defName,@"^MP_[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.CultureInvariant))
            throw new ArgumentException("新定义名称必须以 MP_ 开头，并只包含英文字母、数字、下划线。");
        if (string.IsNullOrWhiteSpace(label) || label.Length > 200)
            throw new ArgumentException("请填写一个有效显示名称。");
        if (project.Files.Any(f=>f.Document.Definitions().Any(x=>x.DefName==defName)))
            throw new InvalidOperationException("工程中已经存在这个 DefName。");
        var relative=$"1.7/Defs/Custom/{defType}/{defName}.xml";
        var xml=new XDocument(new XDeclaration("1.0","utf-8",null),
            new XElement("Defs",new XElement(defType,
                new XElement("defName",defName),new XElement("label",label))));
        project.AddXml(relative,xml.ToString());
        return relative;
    }

    public static IReadOnlyList<Node> Read(DefDocument doc,DefEntry def)
    {
        Ensure(doc,def);
        var rows=new List<Node>();
        Walk(def.Element,0,rows);
        return rows;
    }

    private static void Walk(XElement node,int depth,List<Node> result)
    {
        if(result.Count>=500)return;
        var attrs=string.Join(" · ",node.Attributes().Where(x=>!x.IsNamespaceDeclaration)
            .Select(x=>x.Name.LocalName+"="+x.Value));
        var value=node.HasElements?null:node.Value.Trim();
        result.Add(new Node(DefDocument.GetPath(node),depth,node.Name.LocalName,
            value,attrs,depth==0,node.Name.LocalName=="defName",node.HasElements));
        foreach(var child in node.Elements())Walk(child,depth+1,result);
    }

    public static string AddChild(DefDocument doc,DefEntry def,string parentPath,
        string name,string value,bool group)
    {
        Ensure(doc,def);
        XmlConvert.VerifyNCName(name);
        if(name.Length>100 || value.Length>4096 ||
           (name=="defName" && def.Element.Element("defName")!=null))
            throw new ArgumentException("字段名称或内容无效。");
        var parent=Find(doc,def,parentPath);
        if(parent!=def.Element && !parent.HasElements &&
            !string.IsNullOrWhiteSpace(parent.Value))
            throw new InvalidOperationException("当前字段已有内容，请添加容器后在容器内创建子字段。");
        var child=group?new XElement(name):new XElement(name,value);
        var path="";
        doc.Transform(_=>
        {
            parent.Add(child);
            path=DefDocument.GetPath(child);
        });
        return path;
    }

    public static void SetValue(DefDocument doc,DefEntry def,string nodePath,string value)
    {
        Ensure(doc,def);
        if(value.Length>4096)throw new ArgumentException("字段内容过长。");
        var node=Find(doc,def,nodePath);
        if(node==def.Element || node.Name.LocalName=="defName")
            throw new InvalidOperationException("定义类型和 DefName 不能从这里修改，以免破坏工程引用。");
        if(node.HasElements)throw new InvalidOperationException("容器不能直接修改为文本。");
        var err=StandardFieldRules.Validate(node.Name.LocalName,value);
        if(err!=null)throw new ArgumentException(err);
        doc.Transform(_=>node.Value=value);
    }

    public static void SetAttribute(DefDocument doc,DefEntry def,string nodePath,
        string key,string value)
    {
        Ensure(doc,def);
        XmlConvert.VerifyNCName(key);
        if(key.Length>100 || value.Length>2048)
            throw new ArgumentException("属性名称或内容过长。");
        var node=Find(doc,def,nodePath);
        if(node==def.Element && key=="Abstract")
            throw new InvalidOperationException("普通定义不能直接设为抽象父模板。");
        doc.Transform(_=>node.SetAttributeValue(key,value));
    }

    public static void RemoveNode(DefDocument doc,DefEntry def,string path)
    {
        Ensure(doc,def);
        var node=Find(doc,def,path);
        if(node==def.Element || node.Name.LocalName is "defName" or "label")
            throw new InvalidOperationException("不能删除定义根节点或必要身份字段。");
        doc.Transform(_=>node.Remove());
    }

    private static void Ensure(DefDocument doc,DefEntry def)
    {
        if(def.Element.Document!=doc.Xml || def.Element.Parent!=doc.Xml.Root)
            throw new InvalidOperationException("请选择此工程中有效的定义。");
    }

    private static XElement Find(DefDocument doc,DefEntry def,string path)
    {
        if(path.Length>1000)throw new ArgumentException("节点路径过长。");
        return def.Element.DescendantsAndSelf()
            .FirstOrDefault(x=>DefDocument.GetPath(x)==path)
            ?? throw new InvalidOperationException("该字段已移动或删除，请重新选择。");
    }
}
