using System.Xml.Linq;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class FreeformEditorTests
{
    private static MechWorkspace Project()
    {
        var project = new MechWorkspace("Freeform Test","");
        new MechanoidBlueprint("MP_Test","自由机械体").CreateIn(project);
        return project;
    }

    [Fact]
    public void UserCanBuildAsManyNestedAttackFieldsAsNeededWithoutTemplates()
    {
        var project=Project();
        var file=project.FindDefinitionFile("ThingDef","MP_Test")!;
        var doc=file.Document;
        var def=doc.Definitions().Single();
        var path=FreeformModuleEditor.AddItem(doc,def,"tools");
        var root=Assert.Single(FreeformModuleEditor.Read(doc,def,"tools",path));
        FreeformModuleEditor.AddChild(doc,def,"tools",path,root.Path,
            "label","custom cutter",false);
        var power=FreeformModuleEditor.AddChild(doc,def,"tools",path,root.Path,
            "power","8",false);
        var group=FreeformModuleEditor.AddChild(doc,def,"tools",path,root.Path,
            "capacities","",true);
        FreeformModuleEditor.AddChild(doc,def,"tools",path,group,"li","Cut",false);
        FreeformModuleEditor.AddChild(doc,def,"tools",path,root.Path,
            "cooldownTime","2.5",false);
        FreeformModuleEditor.SetValue(doc,def,"tools",path,power,"12");
        var item=doc.Xml.Descendants("tools").Elements("li").Single();
        Assert.Equal("custom cutter",item.Element("label")?.Value);
        Assert.Equal("12",item.Element("power")?.Value);
        Assert.Equal("Cut",item.Element("capacities")?.Element("li")?.Value);
        Assert.Equal("2.5",item.Element("cooldownTime")?.Value);
        Assert.True(doc.Undo());
        Assert.Equal("8",doc.Xml.Descendants("tools").Elements("li").Single()
            .Element("power")?.Value);
    }

    [Fact]
    public void UserCanAddCustomComponentClassAttributesAndRemoveChildWithoutFlattening()
    {
        var project=Project();
        var file=project.FindDefinitionFile("ThingDef","MP_Test")!;
        var def=file.Document.Definitions().Single();
        var path=FreeformModuleEditor.AddItem(file.Document,def,"comps");
        var root=FreeformModuleEditor.Read(file.Document,def,"comps",path).Single();
        FreeformModuleEditor.SetAttribute(file.Document,def,"comps",path,
            root.Path,"Class","CompProperties_ModCustom");
        var first=FreeformModuleEditor.AddChild(file.Document,def,"comps",path,
            root.Path,"power","20",false);
        FreeformModuleEditor.AddChild(file.Document,def,"comps",path,
            root.Path,"unknownField","preserved",false);
        FreeformModuleEditor.RemoveNode(file.Document,def,"comps",path,first);
        Assert.Contains("Class=\"CompProperties_ModCustom\"",file.Document.Export());
        Assert.Contains("<unknownField>preserved</unknownField>",file.Document.Export());
        Assert.DoesNotContain("<power>20</power>",file.Document.Export());
        Assert.Throws<InvalidOperationException>(()=>
            FreeformModuleEditor.RemoveNode(file.Document,def,"comps",path,root.Path));
    }

    [Fact]
    public void BlankDefCanBecomeCompleteCustomBodyPartUsingOnlyFieldRows()
    {
        var project=Project();
        var path=FreeformDefinitionEditor.Create(project,"BodyPartDef",
            "MP_CustomWing","自定义机翼");
        Assert.StartsWith("1.7/Defs/Custom/BodyPartDef/",path);
        var file=project.FindDefinitionFile("BodyPartDef","MP_CustomWing");
        Assert.NotNull(file);
        var doc=file!.Document;
        var def=doc.Definitions().Single();
        var root=FreeformDefinitionEditor.Read(doc,def).Single(x=>x.IsRoot);
        var hitpoints=FreeformDefinitionEditor.AddChild(doc,def,root.Path,
            "hitPoints","18",false);
        var tags=FreeformDefinitionEditor.AddChild(doc,def,root.Path,"tags","",true);
        FreeformDefinitionEditor.AddChild(doc,def,tags,"li","MovingLimbCore",false);
        FreeformDefinitionEditor.SetValue(doc,def,hitpoints,"22");
        FreeformDefinitionEditor.SetAttribute(doc,def,root.Path,
            "MayRequire","Ludeon.RimWorld.Biotech");
        Assert.Contains("<hitPoints>22</hitPoints>",doc.Export());
        Assert.Contains("<li>MovingLimbCore</li>",doc.Export());
        Assert.Contains("MayRequire=\"Ludeon.RimWorld.Biotech\"",doc.Export());
        Assert.True(doc.Undo());
        Assert.DoesNotContain("MayRequire=",doc.Export());
    }

    [Fact]
    public void FreeformDefEditorPreservesUnrelatedFieldsAndRejectsIdentityDestruction()
    {
        var project=Project();
        FreeformDefinitionEditor.Create(project,"AbilityDef","MP_NewSkill","新能力");
        var file=project.FindDefinitionFile("AbilityDef","MP_NewSkill")!;
        var doc=file.Document;
        var def=doc.Definitions().Single();
        var roots=FreeformDefinitionEditor.Read(doc,def);
        var identity=roots.Single(x=>x.IsIdentity);
        Assert.Throws<InvalidOperationException>(()=>
            FreeformDefinitionEditor.RemoveNode(doc,def,identity.Path));
        Assert.Throws<InvalidOperationException>(()=>
            FreeformDefinitionEditor.SetValue(doc,def,identity.Path,"MP_Hijack"));
        Assert.Throws<InvalidOperationException>(()=>
            FreeformDefinitionEditor.Create(project,"AbilityDef","MP_NewSkill","duplicate"));
        var root=roots.Single(x=>x.IsRoot);
        var arbitrary=FreeformDefinitionEditor.AddChild(doc,def,root.Path,
            "customEffect","on",false);
        Assert.Contains("<customEffect>on</customEffect>",doc.Export());
        FreeformDefinitionEditor.RemoveNode(doc,def,arbitrary);
        Assert.DoesNotContain("<customEffect>",doc.Export());
        Assert.True(doc.Undo());
        Assert.Contains("<customEffect>on</customEffect>",doc.Export());
    }
}
