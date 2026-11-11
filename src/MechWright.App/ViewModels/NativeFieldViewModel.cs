using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Native XML field discovery is driven by observed installed game definitions;
/// examples are suggestions, and insertion always requires an explicit click.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private NativeFieldCatalog _nativeFields = NativeFieldCatalog.Empty;
    private NativeFieldRow? _selectedNativeField;
    private string _nativeFieldSearch = "";
    private string _nativeFieldValue = "";
    private bool _nativeOnlySimple = true;
    private string _nativeFieldStatus = "扫描本机 Core / DLC 目录后可以使用原生 XML 字段目录。";

    public ObservableCollection<NativeFieldRow> VisibleNativeFields { get; } = new();
    public string NativeFieldStatus
    {
        get => _nativeFieldStatus;
        private set { _nativeFieldStatus = value; Notify(); }
    }

    public string NativeFieldSearch
    {
        get => _nativeFieldSearch;
        set { _nativeFieldSearch = value; Notify(); FilterNativeFields(); }
    }

    public bool NativeOnlySimple
    {
        get => _nativeOnlySimple;
        set { _nativeOnlySimple = value; Notify(); FilterNativeFields(); }
    }

    public NativeFieldRow? SelectedNativeField
    {
        get => _selectedNativeField;
        set
        {
            _selectedNativeField = value;
            Notify();
            Notify(nameof(CanInsertNativeField));
            NativeFieldValue = value?.Field.Example ?? "";
            Notify(nameof(NativeFieldTip));
        }
    }

    public string NativeFieldValue
    {
        get => _nativeFieldValue;
        set { _nativeFieldValue = value; Notify(); }
    }

    public bool CanInsertNativeField => _document != null &&
        SelectedDefinition != null && SelectedNativeField?.Field.CanInsert == true;

    public string NativeFieldTip => SelectedNativeField == null
        ? "请选中一个字段；示例数据不会自动写入工程。"
        : SelectedNativeField.Field.Description +
          " · 示例：" + SelectedNativeField.Field.Example +
          "（观察样本，并非原版运行时默认值）";

    private void RefreshNativeFields()
    {
        FilterNativeFields();
    }

    private void UseNativeFieldCatalog(DefCatalog.CatalogScan scan)
    {
        _nativeFields = scan.NativeFields;
        NativeFieldStatus = scan.IsGameInstallation
            ? $"共观察到 {_nativeFields.All.Count} 种 XML 字段路径 · 来源：本机 Core / DLC · 非 C# 完整字段规范"
            : "选择 RimWorld 安装目录才能读取原版字段。Mod 文件夹不会被冒充为原版来源。";
        FilterNativeFields();
    }

    private void FilterNativeFields()
    {
        var previous = SelectedNativeField?.Field.Path;
        VisibleNativeFields.Clear();
        var type = SelectedDefinition?.Entry.Type;
        if (type == null)
        {
            SelectedNativeField = null;
            return;
        }
        foreach (var field in _nativeFields.For(type))
        {
            if (NativeOnlySimple && !field.CanInsert) continue;
            if (NativeFieldSearch.Length > 0 &&
                !field.Path.Contains(NativeFieldSearch, StringComparison.OrdinalIgnoreCase) &&
                !field.Example.Contains(NativeFieldSearch, StringComparison.OrdinalIgnoreCase))
                continue;
            if (VisibleNativeFields.Count >= 600) break;
            VisibleNativeFields.Add(new NativeFieldRow(field));
        }
        SelectedNativeField = VisibleNativeFields.FirstOrDefault(x => x.Field.Path == previous)
            ?? VisibleNativeFields.FirstOrDefault();
    }

    public void ApplyNativeField()
    {
        if (_document == null || SelectedDefinition == null || SelectedNativeField == null) return;
        var index = SelectedDefinition.Entry.Index;
        var field = SelectedNativeField.Field;
        var explicitValue = NativeFieldValue;
        try
        {
            NativeFieldCatalog.Apply(_document, SelectedDefinition.Entry, field, explicitValue);
            RefreshAll(index);
            SelectedNativeField = VisibleNativeFields.FirstOrDefault(x => x.Field.Path == field.Path);
            NativeFieldValue = explicitValue;
            Status = $"已明确写入 {field.Path} = {explicitValue} · 可撤销 · 未修改原版游戏数据";
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }
}

public sealed record NativeFieldRow(NativeFieldCatalog.Field Field)
{
    public string Label => Field.Display;
    public string Meta => Field.SourcePackages +
        (Field.CanInsert ? " · 可插入" : " · 复杂结构");
}
