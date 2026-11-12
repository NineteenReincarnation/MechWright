namespace MechWright.App.ViewModels;

/// <summary>
/// One global navigation surface across the XML document and all visual tools.
/// The existing editors keep their own data models and undo transactions.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private int _workbenchArea = 1;

    /// <summary>
    /// 0=XML Inspector, 1=properties, 2=body, 3=graphics, 4=combat,
    /// 5=gestation, 6=assets, 7=review, 8=other definitions.
    /// </summary>
    public int WorkbenchArea
    {
        get => _isDocumentMode ? 0 : _workbenchArea;
        set => OpenWorkbenchArea(value);
    }

    public bool CanUseFullWorkbench => HasWorkspace && CurrentMechName != null;
    public string PrimarySaveLabel => HasWorkspace ? "导出整个 Mod" : "保存 XML";

    public void OpenWorkbenchArea(int area)
    {
        area = Math.Clamp(area, 0, 8);
        if (area == 0)
        {
            if (!_isDocumentMode) UseDocumentMode();
            else Notify(nameof(WorkbenchArea));
            return;
        }

        if (!CanUseFullWorkbench)
        {
            Status = "专项工作区需要具有关联定义的机械体工程；当前可继续在配置列表中自由编辑 XML。";
            Notify(nameof(WorkbenchArea)); // reset a rejected tab selection
            return;
        }

        if (_isDocumentMode)
        {
            if (!CommitDocumentInputs())
            {
                Notify(nameof(WorkbenchArea));
                return;
            }
            UseGuidedMode();
            if (_isDocumentMode)
            {
                Notify(nameof(WorkbenchArea));
                return;
            }
        }

        _workbenchArea = area;
        var targetStep = StepForArea(area);
        if (WorkflowStep != targetStep) WorkflowStep = targetStep;
        Notify(nameof(WorkbenchArea));
    }

    private static int StepForArea(int area) => area switch
    {
        1 => 0,           // basic
        2 or 3 => 1,      // body + graphics
        4 => 2,           // combat
        5 or 6 => 3,      // gestation + assets
        7 => 4,           // review
        8 => 5,           // custom
        _ => 0
    };

    private static int DefaultAreaForStep(int step) => step switch
    {
        0 => 1,
        1 => 2,
        2 => 4,
        3 => 5,
        4 => 7,
        5 => 8,
        _ => 1
    };

    private void SyncWorkbenchAreaWithStage()
    {
        if (_isDocumentMode) return;
        if (StepForArea(_workbenchArea) != WorkflowStep)
            _workbenchArea = DefaultAreaForStep(WorkflowStep);
        Notify(nameof(WorkbenchArea));
    }
}
