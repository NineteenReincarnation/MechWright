using System.Globalization;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>BodyDef controls share the same document as the raw XML editor and inspector.</summary>
public sealed partial class MainWindowViewModel
{
    private BodyRow? _selectedBodyPart;
    private string _partDefName = "";
    private string _partCoverage = "1";
    private string _partHeight = "Middle";
    private string _partDepth = "Outside";
    private string _partCustomLabel = "";
    private string _newPartDefName = "MechanicalArm";
    private string _newPartCoverage = "0.10";
    private decimal _visualNewPartCoverage = 0.10m;
    private string _newPartHeight = "Middle";
    private string _newPartDepth = "Outside";
    public decimal VisualNewPartCoverage
    {
        get=>_visualNewPartCoverage;
        set{_visualNewPartCoverage=value;Notify();}
    }
    public string NewPartHeight {get=>_newPartHeight;set{_newPartHeight=value;Notify();}}
    public string NewPartDepth {get=>_newPartDepth;set{_newPartDepth=value;Notify();}}


    public IReadOnlyList<string> Heights => BodyStructureEditor.Heights;
    public IReadOnlyList<string> Depths => BodyStructureEditor.Depths;
    public bool HasBodyPart => SelectedBodyPart != null;
    public bool CanRemoveBodyPart => SelectedBodyPart?.Part.Depth > 0;

    public BodyRow? SelectedBodyPart
    {
        get => _selectedBodyPart;
        set
        {
            if (ReferenceEquals(_selectedBodyPart, value)) return;
            _selectedBodyPart = value;
            Notify();
            Notify(nameof(HasBodyPart));
            Notify(nameof(CanRemoveBodyPart));
            PartDefName = value?.Part.PartDef ?? "";
            PartCoverage = value == null ? "1" :
                value.Part.Coverage.ToString("0.########", CultureInfo.InvariantCulture);
            VisualPartCoverage = value == null ? .10m :
                (decimal)Math.Clamp(value.Part.Coverage, .01, 1);
            PartHeight = value?.Part.Height ?? "Middle";
            PartDepth = value?.Part.DepthLayer ?? "Outside";
            PartCustomLabel = value?.Part.Xml.Element("customLabel")?.Value ?? "";
        }
    }

    public string PartDefName { get => _partDefName; set { _partDefName = value; Notify(); } }
    public string PartCoverage { get => _partCoverage; set { _partCoverage = value; Notify(); } }
    public string PartHeight { get => _partHeight; set { _partHeight = value; Notify(); } }
    public string PartDepth { get => _partDepth; set { _partDepth = value; Notify(); } }
    public string PartCustomLabel { get => _partCustomLabel; set { _partCustomLabel = value; Notify(); } }
    public string NewPartDefName { get => _newPartDefName; set { _newPartDefName = value; Notify(); } }
    public string NewPartCoverage { get => _newPartCoverage; set { _newPartCoverage = value; Notify(); } }

    public void ApplyBodyPart()
    {
        if (_document == null || SelectedBodyPart == null) return;
        var defIndex = SelectedDefinition!.Entry.Index;
        var path = DefDocument.GetPath(SelectedBodyPart.Part.Xml);
        try
        {
            BodyStructureEditor.Update(_document, SelectedBodyPart.Part, PartDefName,
                PartCoverage, PartHeight, PartDepth, PartCustomLabel);
            RefreshAll(defIndex, bodyPath: path);
            Status = "已修改所选 BodyDef 部件 · 撤销操作可恢复";
        }
        catch (Exception exception) { ReportError(exception); }
    }

    public void AddBodyPart()
    {
        if (_document == null || SelectedBodyPart == null)
        {
            Status = "请先选择 BodyDef 中的父部件"; return;
        }
        var defIndex = SelectedDefinition!.Entry.Index;
        try
        {
            var path = BodyStructureEditor.AddChild(_document, SelectedBodyPart.Part,
                NewPartDefName,VisualNewPartCoverage.ToString(
                    CultureInfo.InvariantCulture),NewPartHeight,NewPartDepth);
            RefreshAll(defIndex, bodyPath: path);
            Status = "子部件已插入 · 相对 coverage 可在部件属性中修改";
        }
        catch (Exception exception) { ReportError(exception); }
    }

    public void RemoveBodyPart()
    {
        if (_document == null || SelectedBodyPart == null) return;
        var index = SelectedDefinition!.Entry.Index;
        try
        {
            var parent = BodyStructureEditor.Remove(_document, SelectedBodyPart.Part);
            RefreshAll(index, bodyPath: parent);
            Status = "已删除该部件及其所有子部件 · Ctrl+Z 可撤销";
        }
        catch (Exception exception) { ReportError(exception); }
    }

    public void MoveBodyPart(int direction)
    {
        if (_document == null || SelectedBodyPart == null) return;
        var index = SelectedDefinition!.Entry.Index;
        try
        {
            var path = BodyStructureEditor.Move(_document, SelectedBodyPart.Part, direction);
            RefreshAll(index, bodyPath: path);
            Status = direction == -1 ? "部件已上移" : "部件已下移";
        }
        catch (Exception exception) { ReportError(exception); }
    }
}
