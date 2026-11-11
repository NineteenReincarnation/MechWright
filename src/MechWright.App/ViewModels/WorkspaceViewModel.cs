using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MechWright.Core;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Multi-document navigation is a thin adapter over the single DefDocument
/// editor. Switching files never commits or discards edits in other files.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private MechWorkspace? _workspace;
    private ProjectFileRow? _selectedProjectFile;
    private string _newProjectXmlPath = "1.7/Defs/NewDefs.xml";

    public ObservableCollection<ProjectFileRow> ProjectFiles { get; } = new();
    public bool HasWorkspace => _workspace != null;
    public string WorkspaceTitle => _workspace == null
        ? "单文件模式" : $"{_workspace.Name} · {_workspace.Files.Count} XML 文件";
    public string NewProjectXmlPath
    {
        get => _newProjectXmlPath;
        set { _newProjectXmlPath = value; Notify(); }
    }

    public ProjectFileRow? SelectedProjectFile
    {
        get => _selectedProjectFile;
        set
        {
            if (ReferenceEquals(_selectedProjectFile, value)) return;
            _selectedProjectFile = value;
            Notify();
            if (_workspace != null && value != null)
            {
                _document = value.File.Document;
                SourceName = value.File.RelativePath;
                RefreshAll();
                Status = $"项目文件 · {value.File.RelativePath}（其他文件的修改已保留）";
            }
        }
    }

    public void CreateWorkspace()
    {
        var workspace = new MechWorkspace("NewMechanoidMod", "");
        workspace.AddXml("1.7/Defs/Mechanoids.xml", DefDocument.CreateEmpty().Export());
        OpenWorkspace(workspace);
        Status = "已建立空白机械体工程 · 添加定义或新的 XML 文件后，可统一导出";
    }

    public void OpenWorkspace(MechWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
        ProjectFiles.Clear();
        foreach (var file in workspace.Files) ProjectFiles.Add(new ProjectFileRow(file));
        _selectedProjectFile = null;
        Notify(nameof(HasWorkspace));
        Notify(nameof(WorkspaceTitle));
        SelectedProjectFile = ProjectFiles.FirstOrDefault();
        RefreshProjectAssets();
        RefreshWorkflowVisibility();
        StartDesign();
        Status = $"已打开工程 · {workspace.Name} · {workspace.DefinitionCount} 个定义";
    }

    public void GenerateMechanoid(MechanoidBlueprint blueprint)
    {
        if (_workspace == null)
        {
            Status = "请先新建或导入一个工程。";
            return;
        }
        try
        {
            var files = blueprint.CreateIn(_workspace);
            foreach (var file in files)
                ProjectFiles.Add(new ProjectFileRow(file));
            SearchQuery = "";
            var thing = files.First(x => x.Document.Definitions().Any(d => d.Type == "ThingDef"));
            SelectedProjectFile = ProjectFiles.First(x => ReferenceEquals(x.File, thing));
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Entry.Type == "ThingDef" &&
                x.Entry.DefName == blueprint.DefName);
            RefreshProjectAssets();
            StartDesign(blueprint.DefName);
            Status = $"已创建 {blueprint.DefName} · ThingDef / PawnKindDef / BodyDef 三个相互关联的 XML，可继续补充 Body、武器与培育配置。";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void AddProjectXml()
    {
        if (_workspace == null) { Status = "请先新建或导入多文件工程"; return; }
        try
        {
            var path = NewProjectXmlPath.Trim();
            var file = _workspace.AddXml(path, DefDocument.CreateEmpty().Export());
            var row = new ProjectFileRow(file);
            ProjectFiles.Add(row);
            SelectedProjectFile = row;
            Status = $"已新增项目 XML · {path}";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public MechWorkspace? Workspace => _workspace;

    private void RefreshWorkspaceIndicators()
    {
        if (_workspace == null) return;
        foreach (var file in ProjectFiles) file.Refresh();
        Notify(nameof(WorkspaceTitle));
        Notify(nameof(IsDirty));
        Notify(nameof(DirtyFlag));
    }

    public void StatusMessage(string message) => Status = message;

    public void WorkspaceExported(string path, bool includedAssets)
    {
        if (_workspace == null) return;
        _workspace.MarkAllSaved(includeAssets: includedAssets);
        RefreshAll(SelectedDefinition?.Entry.Index, SelectedField?.Field.Path);
        RefreshProjectAssets();
        Status = includedAssets
            ? $"已导出 XML 和项目自有资源至 {path}（源文件未修改）"
            : $"已导出 XML 至 {path}（项目资源未复制）";
    }
}

public sealed class ProjectFileRow(WorkspaceFile file) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public WorkspaceFile File { get; } = file;
    public string Display => (File.Document.IsDirty || File.IsNew ? "● " : "○ ") + File.RelativePath;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
}
