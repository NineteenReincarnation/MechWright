using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<VanillaComparisonRow> VanillaComparisons { get; } = new();
    private string _vanillaComparisonStatus =
        "先在编辑区选择自定义 ThingDef，然后点击下方按钮与当前原版机械体比较。";

    public string VanillaComparisonStatus
    {
        get => _vanillaComparisonStatus;
        private set { _vanillaComparisonStatus = value; Notify(); }
    }

    public void CompareVanillaWithCurrent()
    {
        VanillaComparisons.Clear();
        if (_document == null || SelectedDefinition?.Entry.Type != "ThingDef" ||
            SelectedVanillaMech == null)
        {
            VanillaComparisonStatus = "请先打开 / 选择工程中的 ThingDef，再选一台原版机械体。";
            return;
        }
        try
        {
            var result = VanillaMechComparison.Compare(SelectedVanillaMech.Profile,
                _document, SelectedDefinition.Entry, _workspace);
            foreach (var row in result.Rows)
                VanillaComparisons.Add(new VanillaComparisonRow(row));
            VanillaComparisonStatus = $"{result.UserDefName} ↔ {result.VanillaDefName} · " +
                result.Notice;
        }
        catch (Exception ex)
        {
            VanillaComparisonStatus = "无法对照：" + ex.Message;
        }
    }

    private void InvalidateVanillaComparison()
    {
        if (VanillaComparisons.Count == 0) return;
        VanillaComparisons.Clear();
        VanillaComparisonStatus = "当前机械体或原版资料已变化；重新点击对照刷新结果。";
    }
}

public sealed record VanillaComparisonRow(VanillaMechComparison.ComparisonRow Row)
{
    public string Label => Row.Label;
    public string VanillaValue => Row.VanillaValue;
    public string UserValue => Row.UserExplicitValue;
    public string Delta => Row.Delta;
    public string Provenance => Row.VanillaOrigin;
}
