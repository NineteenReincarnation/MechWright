using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Blank native Defs are assembled sequentially using visual tree rows.
/// The imported or generated XML is the only source of truth.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public IReadOnlyList<FreeformDefTypeRow> FreeformDefTypes { get; } =
        FreeformDefinitionEditor.CommonTypes
            .Select(x=>new FreeformDefTypeRow(x.Type,x.Label)).ToArray();
    public ObservableCollection<FreeformDefinitionRow> FreeformDefinitions { get; }=new();
    public ObservableCollection<FreeformDefinitionNodeRow> FreeformDefinitionNodes { get; }=new();

    private FreeformDefTypeRow? _selectedFreeformDefType;
    private FreeformDefinitionRow? _selectedFreeformDefinition;
    private FreeformDefinitionNodeRow? _selectedFreeformDefinitionNode;
    private string _freeformDefType="ThingDef";
    private string _freeformNewDefName="MP_NewDefinition";
    private string _freeformNewDefLabel="新定义";
    private string _newDefinitionFieldName="";
    private string _newDefinitionFieldValue="";
    private bool _newDefinitionFieldIsGroup;
    private string _definitionNodeValue="";
    private string _definitionAttributeName="Class";
    private string _definitionAttributeValue="";
    private string _definitionBuilderStatus="先新建定义，再逐条增加内部字段。";

    public FreeformDefTypeRow? SelectedFreeformDefType
    {
        get=>_selectedFreeformDefType;
        set
        {
            _selectedFreeformDefType=value;Notify();
            if(value!=null)FreeformDefType=value.Type;
        }
    }
    public string FreeformDefType {get=>_freeformDefType;set{_freeformDefType=value??"";Notify();}}
    public string FreeformNewDefName {get=>_freeformNewDefName;set{_freeformNewDefName=value??"";Notify();}}
    public string FreeformNewDefLabel {get=>_freeformNewDefLabel;set{_freeformNewDefLabel=value??"";Notify();}}
    public FreeformDefinitionRow? SelectedFreeformDefinition
    {
        get=>_selectedFreeformDefinition;
        set
        {
            _selectedFreeformDefinition=value;Notify();
            Notify(nameof(HasFreeformDefinition));
            RefreshFreeformDefinitionNodes();
        }
    }
    public FreeformDefinitionNodeRow? SelectedFreeformDefinitionNode
    {
        get=>_selectedFreeformDefinitionNode;
        set
        {
            _selectedFreeformDefinitionNode=value;Notify();
            Notify(nameof(HasFreeformDefinitionNode));
            Notify(nameof(CanRemoveFreeformDefinitionNode));
            DefinitionNodeValue=value?.Node.Value??"";
        }
    }
    public bool HasFreeformDefinition=>SelectedFreeformDefinition!=null;
    public bool HasFreeformDefinitionNode=>SelectedFreeformDefinitionNode!=null;
    public bool CanRemoveFreeformDefinitionNode=>
        SelectedFreeformDefinitionNode is { Node.IsRoot:false, Node.IsIdentity:false };
    public string NewDefinitionFieldName
    {get=>_newDefinitionFieldName;set{_newDefinitionFieldName=value??"";Notify();}}
    public string NewDefinitionFieldValue
    {get=>_newDefinitionFieldValue;set{_newDefinitionFieldValue=value??"";Notify();}}
    public bool NewDefinitionFieldIsGroup
    {get=>_newDefinitionFieldIsGroup;set{_newDefinitionFieldIsGroup=value;Notify();}}
    public string DefinitionNodeValue
    {get=>_definitionNodeValue;set{_definitionNodeValue=value??"";Notify();}}
    public string DefinitionAttributeName
    {get=>_definitionAttributeName;set{_definitionAttributeName=value??"";Notify();}}
    public string DefinitionAttributeValue
    {get=>_definitionAttributeValue;set{_definitionAttributeValue=value??"";Notify();}}
    public string DefinitionBuilderStatus
    {get=>_definitionBuilderStatus;private set{_definitionBuilderStatus=value;Notify();}}

    private void InitializeFreeformDefinitions()
    {
        if(SelectedFreeformDefType==null)
            SelectedFreeformDefType=FreeformDefTypes.First();
        RefreshFreeformDefinitions();
    }

    private void RefreshFreeformDefinitions(string? select=null)
    {
        var selected=select??SelectedFreeformDefinition?.DefName;
        FreeformDefinitions.Clear();
        if(_workspace!=null)
            foreach(var item in FreeformDefinitionEditor.List(_workspace))
                FreeformDefinitions.Add(new FreeformDefinitionRow(item));
        SelectedFreeformDefinition=FreeformDefinitions.FirstOrDefault(x=>x.DefName==selected)
            ?? FreeformDefinitions.FirstOrDefault();
    }

    private (DefDocument Doc,DefEntry Def)? GetFreeformDefinition()
    {
        var selection=SelectedFreeformDefinition;
        if(selection==null || _workspace==null)return null;
        var file=_workspace.FindDefinitionFile(selection.Type,selection.DefName);
        var def=file?.Document.Definitions().FirstOrDefault(x=>
            x.Type==selection.Type && x.DefName==selection.DefName);
        return file!=null&&def!=null ? (file.Document,def):null;
    }

    private void RefreshFreeformDefinitionNodes(string? selected=null)
    {
        FreeformDefinitionNodes.Clear();
        var link=GetFreeformDefinition();
        if(link!=null)
            foreach(var node in FreeformDefinitionEditor.Read(link.Value.Doc,link.Value.Def))
                FreeformDefinitionNodes.Add(new FreeformDefinitionNodeRow(node));
        SelectedFreeformDefinitionNode=FreeformDefinitionNodes
            .FirstOrDefault(x=>x.Node.Path==selected)
            ?? FreeformDefinitionNodes.FirstOrDefault();
    }

    public void CreateFreeformDefinition()
    {
        if(_workspace==null){DefinitionBuilderStatus="请先新建工程。";return;}
        try
        {
            var name=FreeformNewDefName.Trim();
            FreeformDefinitionEditor.Create(_workspace,FreeformDefType.Trim(),name,
                FreeformNewDefLabel.Trim());
            var added=_workspace.FindDefinitionFile(FreeformDefType.Trim(),name)!;
            if(ProjectFiles.All(x=>!ReferenceEquals(x.File,added)))
                ProjectFiles.Add(new ProjectFileRow(added));
            RefreshFreeformDefinitions(name);
            RefreshAll(SelectedDefinition?.Entry.Index);
            DefinitionBuilderStatus="空白定义创建成功。继续在下方逐项添加字段。";
            Status=DefinitionBuilderStatus;
        }
        catch(Exception ex){DefinitionBuilderStatus=ex.Message;ReportError(ex);}
    }

    public void AddFreeformDefinitionField()
    {
        var link=GetFreeformDefinition();
        if(link==null)return;
        try
        {
            var parentPath=SelectedFreeformDefinitionNode?.Node.Path ??
                FreeformDefinitionEditor.Read(link.Value.Doc,link.Value.Def)[0].Path;
            var path=FreeformDefinitionEditor.AddChild(link.Value.Doc,link.Value.Def,
                parentPath,NewDefinitionFieldName.Trim(),NewDefinitionFieldValue,
                NewDefinitionFieldIsGroup);
            AfterDefinitionEdit("已添加字段“"+NewDefinitionFieldName+"”。",
                NewDefinitionFieldIsGroup?path:parentPath);
        }
        catch(Exception ex){DefinitionBuilderStatus=ex.Message;ReportError(ex);}
    }

    public void SaveFreeformDefinitionValue()
    {
        var link=GetFreeformDefinition();
        if(link==null||SelectedFreeformDefinitionNode==null)return;
        try
        {
            var path=SelectedFreeformDefinitionNode.Node.Path;
            FreeformDefinitionEditor.SetValue(link.Value.Doc,link.Value.Def,path,
                DefinitionNodeValue);
            AfterDefinitionEdit("字段值已保存。",path);
        }
        catch(Exception ex){DefinitionBuilderStatus=ex.Message;ReportError(ex);}
    }

    public void SaveFreeformDefinitionAttribute()
    {
        var link=GetFreeformDefinition();
        if(link==null||SelectedFreeformDefinitionNode==null)return;
        try
        {
            var path=SelectedFreeformDefinitionNode.Node.Path;
            FreeformDefinitionEditor.SetAttribute(link.Value.Doc,link.Value.Def,path,
                DefinitionAttributeName.Trim(),DefinitionAttributeValue);
            AfterDefinitionEdit("节点属性已保存。",path);
        }
        catch(Exception ex){DefinitionBuilderStatus=ex.Message;ReportError(ex);}
    }

    public void RemoveFreeformDefinitionField()
    {
        var link=GetFreeformDefinition();
        if(link==null||SelectedFreeformDefinitionNode==null)return;
        try
        {
            FreeformDefinitionEditor.RemoveNode(link.Value.Doc,link.Value.Def,
                SelectedFreeformDefinitionNode.Node.Path);
            AfterDefinitionEdit("选中字段已删除，可以撤销。");
        }
        catch(Exception ex){DefinitionBuilderStatus=ex.Message;ReportError(ex);}
    }

    private void AfterDefinitionEdit(string message,string? nodePath=null)
    {
        RefreshAll(SelectedDefinition?.Entry.Index);
        RefreshFreeformDefinitionNodes(nodePath);
        RefreshWorksheet();
        DefinitionBuilderStatus=message;
        Status=message;
    }
}

public sealed record FreeformDefTypeRow(string Type,string Label);
public sealed record FreeformDefinitionRow(FreeformDefinitionEditor.Definition Def)
{
    public string DefName=>Def.DefName;
    public string Type=>Def.DefType;
    public string Display=>Def.Display;
}
public sealed record FreeformDefinitionNodeRow(FreeformDefinitionEditor.Node Node)
{
    public string Display=>Node.Display;
}
