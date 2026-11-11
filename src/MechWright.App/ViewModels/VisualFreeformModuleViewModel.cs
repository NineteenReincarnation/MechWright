using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Free-build editor for arbitrary native list items. No stock weapon is
/// mandatory; users can add any number of nested fields and attributes.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<FreeformTargetRow> FreeformTargets { get; } =
        new([new("ThingDef","机械体本体"),new("PawnKindDef","生成与种类")]);
    public IReadOnlyList<string> FreeformContainers { get; } =
        FreeformModuleEditor.CommonContainers;
    public ObservableCollection<FreeformModuleRow> FreeformModules { get; } = new();
    public ObservableCollection<FreeformNodeRow> FreeformNodes { get; } = new();
    private FreeformTargetRow? _selectedFreeformTarget;
    private string _freeformContainer = "tools";
    private FreeformModuleRow? _selectedFreeformModule;
    private FreeformNodeRow? _selectedFreeformNode;
    private string _freeformName = "";
    private string _freeformValue = "";
    private string _freeformNodeValue = "";
    private string _freeformAttrName = "Class";
    private string _freeformAttrValue = "";
    private bool _freeformMakeGroup;
    private string _freeformStatus = "选择一份定义和一种列表，再添加所需数量的配置项。";

    public FreeformTargetRow? SelectedFreeformTarget
    {
        get=>_selectedFreeformTarget;
        set{_selectedFreeformTarget=value;Notify();ReloadFreeformModules();}
    }
    public string FreeformContainer
    {
        get=>_freeformContainer;
        set{_freeformContainer=value??"tools";Notify();ReloadFreeformModules();}
    }
    public FreeformModuleRow? SelectedFreeformModule
    {
        get=>_selectedFreeformModule;
        set
        {
            _selectedFreeformModule=value;
            Notify();
            Notify(nameof(HasFreeformModule));
            RefreshFreeformNodes();
        }
    }
    public FreeformNodeRow? SelectedFreeformNode
    {
        get=>_selectedFreeformNode;
        set
        {
            _selectedFreeformNode=value;
            Notify();
            Notify(nameof(HasFreeformNode));
            Notify(nameof(CanRemoveFreeformNode));
            FreeformNodeValue=value?.Node.Value ?? "";
        }
    }
    public bool HasFreeformModule=>SelectedFreeformModule!=null;
    public bool HasFreeformNode=>SelectedFreeformNode!=null;
    public bool CanRemoveFreeformNode=>SelectedFreeformNode is { Node.IsRoot:false };
    public string FreeformName {get=>_freeformName;set{_freeformName=value??"";Notify();}}
    public string FreeformValue {get=>_freeformValue;set{_freeformValue=value??"";Notify();}}
    public string FreeformNodeValue {get=>_freeformNodeValue;set{_freeformNodeValue=value??"";Notify();}}
    public string FreeformAttrName {get=>_freeformAttrName;set{_freeformAttrName=value??"";Notify();}}
    public string FreeformAttrValue {get=>_freeformAttrValue;set{_freeformAttrValue=value??"";Notify();}}
    public bool FreeformMakeGroup {get=>_freeformMakeGroup;set{_freeformMakeGroup=value;Notify();}}
    public string FreeformStatus
    {
        get=>_freeformStatus;
        private set{_freeformStatus=value;Notify();}
    }

    private void InitializeFreeformEditor()
    {
        if (SelectedFreeformTarget == null)
            SelectedFreeformTarget=FreeformTargets[0];
        else ReloadFreeformModules();
    }

    private (DefDocument Doc,DefEntry Def)? FreeformDocument()
    {
        var name=CurrentMechName;
        var type=SelectedFreeformTarget?.DefType;
        if(name==null || type==null || _workspace==null)return null;
        var file=_workspace.FindDefinitionFile(type,name);
        var def=file?.Document.Definitions().FirstOrDefault(x=>
            x.Type==type && x.DefName==name);
        return file!=null && def!=null ? (file.Document,def) : null;
    }

    public void ReloadFreeformModules(string? preferPath=null)
    {
        var old=preferPath??SelectedFreeformModule?.Path;
        FreeformModules.Clear();
        var link=FreeformDocument();
        if(link!=null)
        {
            try
            {
                foreach(var item in NativeListEditor.Items(link.Value.Doc,
                         link.Value.Def, FreeformContainer))
                    FreeformModules.Add(new FreeformModuleRow(item.XmlPath,
                        item.Summary,item.Index+1));
            }
            catch(Exception ex){FreeformStatus=ex.Message;}
        }
        SelectedFreeformModule=FreeformModules.FirstOrDefault(x=>x.Path==old)
            ?? FreeformModules.FirstOrDefault();
    }

    private void RefreshFreeformNodes(string? preferred=null)
    {
        FreeformNodes.Clear();
        var link=FreeformDocument();
        if (link!=null && SelectedFreeformModule!=null)
        {
            try
            {
                foreach(var node in FreeformModuleEditor.Read(link.Value.Doc,link.Value.Def,
                    FreeformContainer, SelectedFreeformModule.Path))
                    FreeformNodes.Add(new FreeformNodeRow(node));
            }
            catch(Exception ex){FreeformStatus=ex.Message;}
        }
        SelectedFreeformNode=FreeformNodes.FirstOrDefault(x=>x.Node.Path==preferred)
            ?? FreeformNodes.FirstOrDefault();
    }

    public void AddFreeformModule()
    {
        var link=FreeformDocument();
        if(link==null){FreeformStatus="请先建立机械体工程。";return;}
        try
        {
            var path=FreeformModuleEditor.AddItem(link.Value.Doc,link.Value.Def,FreeformContainer);
            AfterFreeformEdit("已创建空白配置项。接下来逐条添加字段。",path);
        }
        catch(Exception ex){FreeformStatus=ex.Message;ReportError(ex);}
    }

    public void RemoveFreeformModule()
    {
        var link=FreeformDocument();
        if(link==null || SelectedFreeformModule==null)return;
        try
        {
            NativeListEditor.Remove(link.Value.Doc,link.Value.Def,FreeformContainer,
                SelectedFreeformModule.Path);
            AfterFreeformEdit("已移除整个配置项，可撤销。");
        }
        catch(Exception ex){FreeformStatus=ex.Message;ReportError(ex);}
    }

    public void AddFreeformField()
    {
        var link=FreeformDocument();
        if(link==null || SelectedFreeformModule==null)return;
        try
        {
            var node=SelectedFreeformNode?.Node.Path??SelectedFreeformModule.Path;
            var path=FreeformModuleEditor.AddChild(link.Value.Doc,link.Value.Def,
                FreeformContainer, SelectedFreeformModule.Path,node,
                FreeformName.Trim(),FreeformValue,FreeformMakeGroup);
            AfterFreeformEdit("已增加字段“"+FreeformName+"”，其他字段不变。",
                SelectedFreeformModule.Path,path);
        }
        catch(Exception ex){FreeformStatus=ex.Message;ReportError(ex);}
    }

    public void SaveFreeformNode()
    {
        var link=FreeformDocument();
        if(link==null || SelectedFreeformModule==null || SelectedFreeformNode==null)return;
        try
        {
            var node=SelectedFreeformNode.Node.Path;
            FreeformModuleEditor.SetValue(link.Value.Doc,link.Value.Def,
                FreeformContainer,SelectedFreeformModule.Path,node,FreeformNodeValue);
            AfterFreeformEdit("字段值已保存，可撤销。",SelectedFreeformModule.Path,node);
        }
        catch(Exception ex){FreeformStatus=ex.Message;ReportError(ex);}
    }

    public void SaveFreeformAttribute()
    {
        var link=FreeformDocument();
        if(link==null || SelectedFreeformModule==null || SelectedFreeformNode==null)return;
        try
        {
            var node=SelectedFreeformNode.Node.Path;
            FreeformModuleEditor.SetAttribute(link.Value.Doc,link.Value.Def,
                FreeformContainer,SelectedFreeformModule.Path,node,
                FreeformAttrName.Trim(),FreeformAttrValue);
            AfterFreeformEdit("属性已写入当前节点，可撤销。",SelectedFreeformModule.Path,node);
        }
        catch(Exception ex){FreeformStatus=ex.Message;ReportError(ex);}
    }

    public void RemoveFreeformNode()
    {
        var link=FreeformDocument();
        if(link==null || SelectedFreeformModule==null || SelectedFreeformNode==null)return;
        try
        {
            var path=SelectedFreeformNode.Node.Path;
            FreeformModuleEditor.RemoveNode(link.Value.Doc,link.Value.Def,
                FreeformContainer,SelectedFreeformModule.Path,path);
            AfterFreeformEdit("已删除选中字段，可撤销。",SelectedFreeformModule.Path);
        }
        catch(Exception ex){FreeformStatus=ex.Message;ReportError(ex);}
    }

    private void AfterFreeformEdit(string message,string? modulePath=null,
        string? nodePath=null)
    {
        var index=SelectedDefinition?.Entry.Index;
        RefreshAll(index);
        ReloadFreeformModules(modulePath);
        if(nodePath!=null)RefreshFreeformNodes(nodePath);
        RefreshInstalledContent();
        RefreshVisualReview();
        FreeformStatus=message;
        Status=message;
    }
}

public sealed record FreeformTargetRow(string DefType,string Label);
public sealed record FreeformModuleRow(string Path,string Title,int Index)
{
    public string Display => Index.ToString("00")+" · "+Title;
}
public sealed record FreeformNodeRow(FreeformModuleEditor.Node Node)
{
    public string Display => Node.IndentedLabel +
        (Node.Value is { Length:>0 } value ? "  =  "+value : "") +
        (Node.Attributes.Length>0 ? "  ["+Node.Attributes+"]" : "");
}
