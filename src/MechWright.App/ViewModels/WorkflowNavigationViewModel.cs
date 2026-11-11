namespace MechWright.App.ViewModels;

/// <summary>
/// Single guided editing path; advanced XML tools stay accessible without
/// competing with the five regular design stages.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private int _workflowStep;
    private string? _workflowMechName;

    public bool ShowStartPage => !HasWorkspace && !HasDocument;
    public bool ShowWorkspacePage => !ShowStartPage;
    public bool CanExportProject => HasWorkspace;

    public int WorkflowStep
    {
        get => _workflowStep;
        set
        {
            var step = Math.Clamp(value, 0, 5);
            if (_workflowStep == step) return;
            _workflowStep = step;
            Notify();
            Notify(nameof(WorkflowTitle));
            Notify(nameof(WorkflowDescription));
            Notify(nameof(IsBasicStage));
            Notify(nameof(IsBodyStage));
            Notify(nameof(IsWeaponStage));
            Notify(nameof(IsAssetStage));
            Notify(nameof(IsReviewStage));
            Notify(nameof(IsAdvancedStage));
            Notify(nameof(CanGoBackStage));
            Notify(nameof(CanGoForwardStage));
            SelectStageDocument();
        }
    }

    public string WorkflowTitle => WorkflowStep switch
    {
        0 => "基础配置",
        1 => "身体结构与外观",
        2 => "武器与战斗",
        3 => "工程资源",
        4 => "检查与导出",
        _ => "高级 XML 工具"
    };
    public string WorkflowDescription => WorkflowStep switch
    {
        0 => "确定机体身份、重量、身体大小与常规属性。",
        1 => "编辑 BodyDef 的父子部件与覆盖率，并检查各方向贴图。",
        2 => "查看普通武器的连发与伤害，并编辑原生武器/Comp 列表。",
        3 => "将贴图、音频和程序集登记到 Mod 工程中。",
        4 => "查看 XML 诊断、引用关系与源文件，然后导出完整 Mod。",
        _ => "自定义原生字段、XML 属性和未收录的扩展内容。"
    };

    public bool IsBasicStage => WorkflowStep == 0;
    public bool IsBodyStage => WorkflowStep == 1;
    public bool IsWeaponStage => WorkflowStep == 2;
    public bool IsAssetStage => WorkflowStep == 3;
    public bool IsReviewStage => WorkflowStep == 4;
    public bool IsAdvancedStage => WorkflowStep == 5;
    public bool CanGoBackStage => WorkflowStep is > 0 and <= 4;
    public bool CanGoForwardStage => WorkflowStep < 4;

    public void PreviousStage()
    {
        if (CanGoBackStage) WorkflowStep--;
    }

    public void NextStage()
    {
        if (CanGoForwardStage) WorkflowStep++;
    }

    public void StartDesign(string? mechDefName = null)
    {
        if (!string.IsNullOrWhiteSpace(mechDefName)) _workflowMechName = mechDefName;
        WorkflowStep = 0;
        RefreshWorkflowVisibility();
        SelectStageDocument();
    }

    private void RefreshWorkflowVisibility()
    {
        Notify(nameof(ShowStartPage));
        Notify(nameof(ShowWorkspacePage));
        Notify(nameof(CanExportProject));
    }

    private void SelectStageDocument()
    {
        if (_workspace == null) return;
        var wantedType = WorkflowStep switch
        {
            0 => "ThingDef",
            1 => "BodyDef",
            2 => "ThingDef",
            _ => null
        };
        if (wantedType == null) return;

        var wantedName = WorkflowStep == 1 && _workflowMechName != null
            ? _workflowMechName + "_Body" : _workflowMechName;
        var currentIsMatch = SelectedDefinition?.Entry.Type == wantedType &&
            (wantedName == null || SelectedDefinition.Entry.DefName == wantedName);
        if (currentIsMatch) return;

        var candidates = _workspace.Files
            .SelectMany(file => file.Document.Definitions()
                .Where(def => def.Type == wantedType)
                .Select(def => (File: file, Def: def)))
            .ToArray();
        if (candidates.Length == 0) return;
        var target = candidates.FirstOrDefault(x => x.Def.DefName == wantedName);
        if (target.File == null) target = candidates[0];
        var row = ProjectFiles.FirstOrDefault(x => ReferenceEquals(x.File, target.File));
        if (row == null) return;
        if (!ReferenceEquals(SelectedProjectFile, row)) SelectedProjectFile = row;
        SelectedDefinition = Definitions.FirstOrDefault(x =>
            x.Entry.Type == wantedType && x.Entry.DefName == target.Def.DefName);
    }
}
