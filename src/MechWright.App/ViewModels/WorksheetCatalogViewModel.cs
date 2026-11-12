using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Inline, searchable field catalog for the primary vertical worksheet.
/// Adding a field pins an editable draft; XML changes only when Save is pressed.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private readonly Dictionary<(string DefType, string Path), VisualContentEditor.Option>
        _worksheetDraftOptions = new();
    // Edited values must survive filtering and ordinary editor refreshes without
    // prematurely entering the source XML.
    private readonly Dictionary<(string DefType, string Path), WorksheetFieldRow>
        _worksheetEditedRows = new();
    private MechWorkspace? _worksheetDraftWorkspace;
    private string? _worksheetDraftMech;

    public ObservableCollection<WorksheetCatalogRow> WorksheetCatalogRows { get; } = new();
    public IReadOnlyList<string> WorksheetCatalogCategories { get; } =
    [
        "全部", "身份与描述", "基础性能", "机械师与生成",
        "武器与战斗", "外观与音效", "组件与能力", "其他原生属性"
    ];

    private bool _worksheetCatalogOpen;
    private string _worksheetCatalogSearch = "";
    private string _worksheetCatalogCategory = "全部";
    private string _worksheetCatalogCount = "";

    public bool WorksheetCatalogOpen
    {
        get => _worksheetCatalogOpen;
        private set { _worksheetCatalogOpen = value; Notify(); }
    }

    public string WorksheetCatalogSearch
    {
        get => _worksheetCatalogSearch;
        set
        {
            if (_worksheetCatalogSearch == value) return;
            _worksheetCatalogSearch = value ?? "";
            Notify();
            RefreshWorksheetCatalog();
        }
    }

    public string WorksheetCatalogCategory
    {
        get => _worksheetCatalogCategory;
        set
        {
            if (_worksheetCatalogCategory == value) return;
            _worksheetCatalogCategory = value ?? "全部";
            Notify();
            RefreshWorksheetCatalog();
        }
    }

    public string WorksheetCatalogCount
    {
        get => _worksheetCatalogCount;
        private set { _worksheetCatalogCount = value; Notify(); }
    }

    public void ToggleWorksheetCatalog()
    {
        if (SelectedContentCustomTarget == null && FreeformTargets.Count > 0)
            SelectedContentCustomTarget = FreeformTargets[0];
        WorksheetCatalogOpen = !WorksheetCatalogOpen;
        RefreshWorksheetCatalog();
    }

    private void EnsureWorksheetDraftOwner()
    {
        var name = CurrentMechName;
        if (ReferenceEquals(_worksheetDraftWorkspace, _workspace) &&
            _worksheetDraftMech == name) return;
        _worksheetDraftWorkspace = _workspace;
        _worksheetDraftMech = name;
        _worksheetDraftOptions.Clear();
        _worksheetEditedRows.Clear();
        WorksheetFields.Clear();
    }

    private void RefreshWorksheetCatalog()
    {
        WorksheetCatalogRows.Clear();
        if (!WorksheetCatalogOpen || CurrentMechName == null || _workspace == null)
        {
            WorksheetCatalogCount = CurrentMechName == null
                ? "先新建或选择一台机械体" : "";
            return;
        }

        EnsureWorksheetDraftOwner();
        var existing = WorksheetFields
            .Select(x => (x.Spec.DefType, x.Spec.Path)).ToHashSet();
        var catalog = _contentOptions
            .Concat(VisualContentEditor.ExistingOptions(_workspace, CurrentMechName))
            .DistinctBy(x => (x.DefType, x.Path));
        var query = WorksheetCatalogSearch.Trim();
        var matching = catalog
            .Where(x => !existing.Contains((x.DefType, x.Path)) &&
                _workspace.FindDefinitionFile(x.DefType, CurrentMechName) != null &&
                (WorksheetCatalogCategory == "全部" || x.Category == WorksheetCatalogCategory) &&
                (query.Length == 0 ||
                 x.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 x.Path.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 x.Help.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.Verified)
            .ThenBy(x => x.Category, StringComparer.Ordinal)
            .ThenBy(x => x.Label, StringComparer.Ordinal)
            .ToArray();

        foreach (var option in matching.Take(160))
            WorksheetCatalogRows.Add(new WorksheetCatalogRow(option));
        WorksheetCatalogCount = matching.Length == 0
            ? "没有匹配的字段 · 可以读取游戏目录扩充字段库"
            : matching.Length > 160
                ? "匹配 " + matching.Length + " 项，当前显示前 160 项 · 输入关键词进一步筛选"
                : "可添加 " + matching.Length + " 项";
    }

    public void AddWorksheetCatalogField(WorksheetCatalogRow? row)
    {
        if (row == null || _workspace == null || CurrentMechName == null) return;
        EnsureWorksheetDraftOwner();
        var key = (row.Option.DefType, row.Option.Path);
        if (WorksheetFields.Any(x => (x.Spec.DefType, x.Spec.Path) == key))
        {
            WorksheetMessage = "该字段已经在配置清单中。";
            return;
        }

        _worksheetDraftOptions[key] = row.Option;
        WorksheetCatalogOpen = false;
        WorksheetSearch = "";
        RefreshWorksheet();
        WorksheetMessage = "“" + row.Option.Label +
            "”已添加到清单顶部。填写数值并点击保存后才会写入工程。";
    }

    public void DiscardWorksheetDraft(WorksheetFieldRow? row)
    {
        if (row == null || !row.IsPinned) return;
        EnsureWorksheetDraftOwner();
        var key = (row.Spec.DefType, row.Spec.Path);
        _worksheetDraftOptions.Remove(key);
        _worksheetEditedRows.Remove(key);
        WorksheetFields.Remove(row);
        RefreshWorksheet();
        WorksheetMessage = "已从清单中移除未保存的字段，没有修改工程。";
    }
}

public sealed record WorksheetCatalogRow(VisualContentEditor.Option Option)
{
    public string Title => Option.Label;
    public string Detail => Option.Help;
    public string Category => Option.Category;
    public string Source => Option.Verified ? Option.Source : Option.Source + " · 未验证游戏语义";
    public string Path => Option.Path;
}
