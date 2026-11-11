using System.Xml.Linq;
using MechWright.RimWorld;
using Xunit;

namespace MechWright.RimWorld.Tests;

public sealed class VisualContentEditorTests
{
    private static MechWorkspace Project()
    {
        var ws=new MechWorkspace("Visual Tests","");
        new MechanoidBlueprint("MP_Example","测试机械体").CreateIn(ws);
        return ws;
    }

    [Fact]
    public void CommonVisualOptionWritesToCorrectLinkedDefAndSupportsRemovalAndUndo()
    {
        var ws=Project();
        var option=VisualContentEditor.Common.Single(x=>x.Path=="maxPerGroup");
        var file=ws.FindDefinitionFile("PawnKindDef","MP_Example")!;
        var original=file.Document.Export();
        VisualContentEditor.Apply(ws,"MP_Example",option,"3");
        Assert.Equal("3",VisualContentEditor.Read(ws,"MP_Example",option));
        Assert.Contains("<maxPerGroup>3</maxPerGroup>",file.Document.Export());
        Assert.DoesNotContain("maxPerGroup",
            ws.FindDefinitionFile("ThingDef","MP_Example")!.Document.Export());
        VisualContentEditor.Remove(ws,"MP_Example",option);
        Assert.Null(VisualContentEditor.Read(ws,"MP_Example",option));
        Assert.True(file.Document.Undo());
        Assert.Equal("3",VisualContentEditor.Read(ws,"MP_Example",option));
        Assert.True(file.Document.Undo());
        Assert.Equal(original,file.Document.Export());
    }

    [Fact]
    public void UsesTypedValidationAndNeverRewritesUnrelatedDocument()
    {
        var ws=Project();
        var name=ws.FindDefinitionFile("ThingDef","MP_Example")!.Document;
        var original=name.Export();
        var allow=VisualContentEditor.Common.Single(x=>x.Path=="allowInMechClusters");
        Assert.Throws<ArgumentException>(()=>
            VisualContentEditor.Apply(ws,"MP_Example",allow,"maybe"));
        var weight=VisualContentEditor.Common.Single(x=>x.Path=="race/mechWeightClass");
        Assert.Throws<ArgumentException>(()=>
            VisualContentEditor.Apply(ws,"MP_Example",weight,"Gigantic"));
        var unnamed=new VisualContentEditor.Option(
            "ThingDef","defName","unsafe","身份与描述","Text","MP_X","","",true);
        Assert.Throws<ArgumentException>(()=>
            VisualContentEditor.Apply(ws,"MP_Example",unnamed,"MP_Hijack"));
        Assert.Equal(original,name.Export());
    }

    [Fact]
    public void SafeNativeObservedFieldsAreDiscoveredWithoutPretendingToKnowGameDefaults()
    {
        var src=new VanillaMechanoidLibrary.SourceDef(
            "ThingDef","SampleMech",null,null,"Biotech","FromGame.xml",
            XElement.Parse("<ThingDef><defName>SampleMech</defName><statBases><CustomNumber>2.4</CustomNumber></statBases></ThingDef>"));
        var native=NativeFieldCatalog.Build([src]);
        var option=VisualContentEditor.All(native).Single(x=>x.Path=="statBases/CustomNumber");
        Assert.False(option.Verified);
        Assert.Equal("2.4",option.Suggestion);
        var ws=Project();
        VisualContentEditor.Apply(ws,"MP_Example",option,"3.5");
        Assert.Equal("3.5",VisualContentEditor.Read(ws,"MP_Example",option));
    }

    [Fact]
    public void VanillaModuleCatalogCopiesRealItemsAndKeepsClassAndUnknownNodes()
    {
        var src=new VanillaMechanoidLibrary.SourceDef(
            "ThingDef","SampleMech",null,null,"Biotech","FromGame.xml",
            XElement.Parse("""
              <ThingDef>
                <defName>SampleMech</defName>
                <comps><li Class="CompProperties_Example"><custom><ignored>preserve</ignored></custom><enabled>true</enabled></li></comps>
                <tools><li><label>cut</label><power>8</power><cooldownTime>2.0</cooldownTime><originalExtra>keep</originalExtra></li></tools>
              </ThingDef>
              """));
        var catalog=NativeListTemplateCatalog.Build([src]);
        var ws=Project();
        var template=catalog.All.Single(x=>x.Container=="tools");
        NativeListTemplateCatalog.Install(ws,"MP_Example",template);
        var file=ws.FindDefinitionFile("ThingDef","MP_Example")!;
        var def=file.Document.Definitions().Single();
        var item=NativeListEditor.Items(file.Document,def,"tools").Single();
        Assert.Contains("<originalExtra>keep</originalExtra>",item.Source);
        var parameters=VisualContentEditor.Parameters(item.Source);
        Assert.Contains(parameters,x=>x.Path=="power" && x.Value=="8");
        VisualContentEditor.UpdateModuleParameter(file.Document,def,"tools",
            item.XmlPath,"power","14");
        var after=NativeListEditor.Items(file.Document,def,"tools").Single();
        Assert.Contains("<power>14</power>",after.Source);
        Assert.Contains("<originalExtra>keep</originalExtra>",after.Source);
        Assert.True(file.Document.Undo());
        Assert.Contains("<power>8</power>",file.Document.Export());

        var comp=catalog.All.Single(x=>x.Container=="comps");
        NativeListTemplateCatalog.Install(ws,"MP_Example",comp);
        Assert.Contains("Class=\"CompProperties_Example\"",file.Document.Export());
        Assert.Contains("<ignored>preserve</ignored>",file.Document.Export());
    }

    [Fact]
    public void ListParameterEditorRejectsPathsThatAreNotSelectableNativeLeaves()
    {
        var ws=Project();
        var f=ws.FindDefinitionFile("ThingDef","MP_Example")!;
        var def=f.Document.Definitions().Single();
        var source="""
        <li Class="CompProperties_Test"><nested><li>first</li><li>second</li></nested><setting>safe</setting></li>
        """;
        var itemPath=NativeListEditor.Add(f.Document,def,"comps",source);
        var fragment=NativeListEditor.Items(f.Document,def,"comps").Single().Source;
        Assert.Contains(VisualContentEditor.Parameters(fragment),x=>x.Path=="setting");
        Assert.DoesNotContain(VisualContentEditor.Parameters(fragment),x=>x.Path.Contains("nested"));
        Assert.Throws<InvalidOperationException>(()=>
            VisualContentEditor.UpdateModuleParameter(f.Document,def,
                "comps",itemPath,"nested/li","modified"));
        Assert.Contains("first",f.Document.Export());
    }
}
