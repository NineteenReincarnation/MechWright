using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Native gestation recipe builder. Uses vanilla RecipeDef, not a custom
/// mech gestation system. No external game content is copied.
/// </summary>
public static class VisualGestationEditor
{
    public sealed record Material(string DefName,string Label);
    public sealed record Ingredient(string DefName,int Amount);
    public sealed record Configuration(string ParentName,int Cycles,string? Research,
        IReadOnlyList<Ingredient> Ingredients);

    public static IReadOnlyList<Material> SuggestedMaterials { get; } =
    [
        new("Steel","钢"),
        new("Plasteel","玻璃钢"),
        new("ComponentIndustrial","零件"),
        new("ComponentSpacer","高级零件"),
        new("SubcoreBasic","低级亚核心"),
        new("SubcoreRegular","标准亚核心"),
        new("SubcoreHigh","高级亚核心"),
        new("Uranium","铀"),
        new("Chemfuel","化合燃料"),
        new("WoodLog","木材")
    ];

    public static IReadOnlyList<string> KnownResearch { get; } =
        ["无科技限制","BasicMechtech","HighMechtech","UltraMechtech"];

    public static string RecipeDefName(string mech) => mech+"_Gestate";

    public static IReadOnlyList<string> DiscoverRecipeParents(
        IReadOnlyList<VanillaMechanoidLibrary.SourceDef> vanilla)
    {
        return vanilla.Where(x => x.Type=="RecipeDef" &&
                x.TemplateName is { } name &&
                name.Contains("MechanoidRecipe",StringComparison.OrdinalIgnoreCase))
            .Select(x=>x.TemplateName!).Distinct(StringComparer.Ordinal)
            .OrderBy(x=>x,StringComparer.Ordinal).ToList();
    }

    public static string? ReadCurrentParent(MechWorkspace project,string mech)
    {
        var file=project.FindDefinitionFile("RecipeDef",RecipeDefName(mech));
        return file?.Document.Definitions()
            .FirstOrDefault(x=>x.Type=="RecipeDef" && x.DefName==RecipeDefName(mech))
            ?.Element.Attribute("ParentName")?.Value;
    }

    public static Configuration? Read(MechWorkspace project,string mech)
    {
        var file=project.FindDefinitionFile("RecipeDef",RecipeDefName(mech));
        var def=file?.Document.Definitions()
            .FirstOrDefault(x=>x.Type=="RecipeDef" && x.DefName==RecipeDefName(mech));
        if(def==null)return null;
        var node=def.Element;
        int.TryParse(node.Element("gestationCycles")?.Value,out var cycles);
        var items=node.Element("ingredients")?.Elements("li")
            .Select(x=>new Ingredient(
                x.Element("filter")?.Element("thingDefs")?.Elements("li")
                    .FirstOrDefault()?.Value ?? "",
                int.TryParse(x.Element("count")?.Value,out var number)?number:0))
            .Where(x=>x.DefName.Length>0 && x.Amount>0).ToArray() ?? [];
        return new Configuration(node.Attribute("ParentName")?.Value ??
            "MediumMechanoidRecipe",cycles,node.Element("researchPrerequisite")?.Value,items);
    }

    public static bool Exists(MechWorkspace project,string mech)=>
        project.FindDefinitionFile("RecipeDef",RecipeDefName(mech))!=null;

    public static void Apply(MechWorkspace project,string mech, string label,
        Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!Regex.IsMatch(mech,@"^MP_[A-Za-z0-9_]+$",RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(configuration.ParentName,
                @"^[A-Za-z_][A-Za-z0-9_]*$",RegexOptions.CultureInvariant) ||
            !configuration.ParentName.Contains("MechanoidRecipe",StringComparison.Ordinal))
            throw new ArgumentException("请选择有效的机械体和游戏原生培育模板。");
        if(configuration.Cycles is <1 or >10000)throw new ArgumentException("培育周期为 1–10000。");
        if (!Exists(project,mech) &&
            project.FindDefinitionFile("ThingDef",mech)==null)
            throw new InvalidOperationException("请先创建当前机械体，再配置其培育配方。");
        if(configuration.Ingredients.Count is <1 or >32)
            throw new ArgumentException("请添加 1–32 种培育材料。");
        var ingredients=configuration.Ingredients.ToArray();
        if (ingredients.Any(x=>x.Amount is <1 or >1000000 ||
            !Regex.IsMatch(x.DefName,@"^[A-Za-z_][A-Za-z0-9_]*$",
                RegexOptions.CultureInvariant)))
            throw new ArgumentException("材料名称或数量无效。");
        if (ingredients.Select(x=>x.DefName)
            .Distinct(StringComparer.Ordinal).Count()!=ingredients.Length)
            throw new ArgumentException("同种材料只能添加一次，请调整该材料的数量。");
        var tech=configuration.Research;
        if(!string.IsNullOrWhiteSpace(tech) &&
            !Regex.IsMatch(tech,@"^[A-Za-z_][A-Za-z0-9_]*$",RegexOptions.CultureInvariant))
            throw new ArgumentException("请选择有效的科技项目。");
        var name=RecipeDefName(mech);
        var file=project.FindDefinitionFile("RecipeDef",name);
        var ingredientXml=new XElement("ingredients",
            new XAttribute("Inherit","False"),
            ingredients.Select(x=>new XElement("li",
                new XElement("filter",new XElement("thingDefs",
                    new XElement("li",x.DefName))),
                new XElement("count",x.Amount))));
        if(file==null)
        {
            var node=new XElement("RecipeDef",
                new XAttribute("ParentName",configuration.ParentName),
                new XAttribute("MayRequire","Ludeon.RimWorld.Biotech"),
                new XElement("defName",name),
                new XElement("label","培育"+label),
                new XElement("description","培育"+label+"。"),
                new XElement("gestationCycles",configuration.Cycles),
                new XElement("researchPrerequisite",tech??""),
                ingredientXml,
                new XElement("products",new XElement(mech,1)),
                new XElement("descriptionHyperlinks",new XElement("ThingDef",mech)));
            if(string.IsNullOrEmpty(tech))node.Element("researchPrerequisite")?.Remove();
            var xml=new XDocument(new XDeclaration("1.0","utf-8",null),
                new XElement("Defs",node)).ToString();
            project.AddXml($"1.7/Defs/RecipeDefs/{name}.xml",xml);
            return;
        }

        var doc=file.Document;
        var def=doc.Definitions().Single(x=>x.Type=="RecipeDef" && x.DefName==name);
        var output=def.Element.Element("products");
        if(output!=null && (output.Elements().Count()!=1 ||
            output.Elements().Single().Name.LocalName!=mech))
            throw new InvalidOperationException("自定义产物不能由通用表单覆盖。");
        // An imported custom recipe may contain additional logic or ingredient
        // filters. Refuse a destructive rewrite of these unknown structures.
        var present=def.Element.Element("ingredients");
        if(present!=null && present.Elements().Any(x=>x.Name.LocalName!="li" ||
            x.Elements().Any(y=>y.Name.LocalName is not ("filter" or "count")) ||
            x.Element("filter")?.Elements().Any(y=>y.Name.LocalName!="thingDefs")==true ||
            x.Element("filter")?.Element("thingDefs")?.Elements().Any(y=>y.Name.LocalName!="li")==true))
            throw new InvalidOperationException("原配方有特殊材料逻辑，普通表单不能覆盖；请使用高级编辑。");
        doc.Transform(_=>
        {
            def.Element.SetAttributeValue("ParentName",configuration.ParentName);
            def.Element.SetElementValue("gestationCycles",configuration.Cycles);
            if(string.IsNullOrEmpty(tech))
                def.Element.Element("researchPrerequisite")?.Remove();
            else def.Element.SetElementValue("researchPrerequisite",tech);
            if(present==null)def.Element.Add(ingredientXml);
            else present.ReplaceWith(ingredientXml);
            var products=def.Element.Element("products");
            if(products==null)def.Element.Add(new XElement("products",new XElement(mech,1)));
            else products.Elements().Single().Value="1";
        });
    }
}
