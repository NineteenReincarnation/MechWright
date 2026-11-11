using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MechWright.Core;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private DefDocument? _document;
    private string _sourceName = "未打开文档";
    private string _status = "就绪 · 新建 XML 或打开已有 Def 文件";
    private string _searchQuery = "";
    private string _catalogQuery = "";
    private string _xmlPreview = "";
    private string _editingValue = "";
    private string _newDefName = "MP_NewMech";
    private string _newDefType = "ThingDef";
    private bool _isBusy;
    private DefinitionRow? _selectedDefinition;
    private FieldRow? _selectedField;
    private CatalogRow? _selectedCatalog;

    public ObservableCollection<DefinitionRow> Definitions { get; } = new();
    public ObservableCollection<DefinitionRow> VisibleDefinitions { get; } = new();
    public ObservableCollection<FieldRow> Fields { get; } = new();
    public ObservableCollection<BodyRow> BodyParts { get; } = new();
    public ObservableCollection<DiagnosticRow> Diagnostics { get; } = new();
    public ObservableCollection<CatalogRow> VisibleCatalog { get; } = new();
    public string[] SupportedKinds { get; } = ["ThingDef", "PawnKindDef", "BodyDef", "BodyPartDef", "RecipeDef"];
    private IReadOnlyList<CatalogEntry> _catalog = Array.Empty<CatalogEntry>();

    public bool HasDocument => _document != null;
    public bool IsDirty => _document?.IsDirty ?? false;
    public bool CanUndo => _document?.CanUndo ?? false;
    public bool CanRedo => _document?.CanRedo ?? false;
    public string SourceName { get => _sourceName; private set { _sourceName = value; Notify(); } }
    public string Status { get => _status; private set { _status = value; Notify(); } }
    public string SearchQuery
    {
        get => _searchQuery;
        set { _searchQuery = value; Notify(); FilterDefinitions(); }
    }
    public string CatalogQuery
    {
        get => _catalogQuery;
        set { _catalogQuery = value; Notify(); FilterCatalog(); }
    }
    public string XmlPreview { get => _xmlPreview; private set { _xmlPreview = value; Notify(); } }
    public string EditingValue
    {
        get => _editingValue;
        set { _editingValue = value; Notify(); }
    }
    public string NewDefName
    {
        get => _newDefName;
        set { _newDefName = value; Notify(); }
    }
    public string NewDefType
    {
        get => _newDefType;
        set { _newDefType = value; Notify(); }
    }
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; Notify(); }
    }
    public string DefinitionCount => $"{Definitions.Count} DEFINITIONS";
    public string CatalogCount => $"{_catalog.Count} LOCAL DEFS";
    public string DiagnosticCount => Diagnostics.Count == 0 ? "NO LOCAL ERRORS" : $"{Diagnostics.Count} DIAGNOSTICS";
    public string DirtyFlag => IsDirty ? "● 未保存修改" : "○ 无未保存修改";
    public string SelectedDefName => SelectedDefinition?.Entry.DefName ?? "未选择定义";
    public string SelectedPath => SelectedField?.Field.Path ?? "选择一个字段来编辑";

    public DefinitionRow? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (ReferenceEquals(_selectedDefinition, value)) return;
            _selectedDefinition = value;
            Notify();
            Notify(nameof(SelectedDefName));
            RefreshFields();
        }
    }

    public FieldRow? SelectedField
    {
        get => _selectedField;
        set
        {
            _selectedField = value;
            Notify();
            Notify(nameof(SelectedPath));
            EditingValue = value?.Field.Value ?? "";
        }
    }

    public CatalogRow? SelectedCatalog
    {
        get => _selectedCatalog;
        set { _selectedCatalog = value; Notify(); }
    }

    public void NewFile()
    {
        SetDocument(DefDocument.CreateEmpty(), "未命名 · Defs.xml");
        Status = "新建 XML · 可通过左侧建立 ThingDef、PawnKindDef 或 BodyDef";
    }

    public void LoadFile(string source, string fileName, string? selectDef = null)
    {
        var document = DefDocument.Parse(source, fileName);
        SetDocument(document, fileName);
        if (selectDef != null)
            SelectedDefinition = VisibleDefinitions.FirstOrDefault(x => x.Entry.DefName == selectDef)
                ?? Definitions.FirstOrDefault(x => x.Entry.DefName == selectDef);
        Status = $"已读取 · {Path.GetFileName(fileName)} · {Definitions.Count} 个定义";
    }

    private void SetDocument(DefDocument document, string name)
    {
        _document = document;
        SourceName = name;
        RefreshAll();
    }

    private void RefreshAll(int? preferredIndex = null, string? fieldPath = null)
    {
        Definitions.Clear();
        if (_document != null)
        {
            foreach (var entry in _document.Definitions()) Definitions.Add(new DefinitionRow(entry));
        }
        Notify(nameof(DefinitionCount));
        FilterDefinitions();

        _selectedDefinition = preferredIndex == null ? Definitions.FirstOrDefault() :
            Definitions.FirstOrDefault(x => x.Entry.Index == preferredIndex.Value);
        Notify(nameof(SelectedDefinition));
        Notify(nameof(SelectedDefName));
        RefreshFields(fieldPath);

        XmlPreview = _document?.Export() ?? "";
        Diagnostics.Clear();
        if (_document != null)
            foreach (var d in _document.Validate()) Diagnostics.Add(new DiagnosticRow(d));
        Notify(nameof(DiagnosticCount));
        Notify(nameof(DirtyFlag));
        Notify(nameof(IsDirty));
        Notify(nameof(CanUndo));
        Notify(nameof(CanRedo));
        Notify(nameof(HasDocument));
    }

    private void RefreshFields(string? preferredPath = null)
    {
        Fields.Clear();
        BodyParts.Clear();
        if (_document != null && _selectedDefinition != null)
        {
            foreach (var f in _document.Fields(_selectedDefinition.Entry)) Fields.Add(new FieldRow(f));
            foreach (var part in _document.BodyParts(_selectedDefinition.Entry))
                BodyParts.Add(new BodyRow(part));
        }
        SelectedField = preferredPath == null ? Fields.FirstOrDefault()
            : Fields.FirstOrDefault(x => x.Field.Path == preferredPath);
    }

    private void FilterDefinitions()
    {
        VisibleDefinitions.Clear();
        foreach (var row in Definitions.Where(x => SearchQuery.Length == 0 ||
                     x.Display.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)))
            VisibleDefinitions.Add(row);
    }

    private void FilterCatalog()
    {
        VisibleCatalog.Clear();
        foreach (var row in _catalog.Where(x =>
                     CatalogQuery.Length == 0 ||
                     x.Identity.DefName.Contains(CatalogQuery, StringComparison.OrdinalIgnoreCase) ||
                     x.Label.Contains(CatalogQuery, StringComparison.OrdinalIgnoreCase))
                     .Take(300))
            VisibleCatalog.Add(new CatalogRow(row));
        Notify(nameof(CatalogCount));
    }

    public void ApplyEdit()
    {
        if (_document == null || SelectedField == null) return;
        var index = SelectedDefinition?.Entry.Index;
        var path = SelectedField.Field.Path;
        try
        {
            _document.SetField(SelectedField.Field, EditingValue);
            RefreshAll(index, path);
            Status = "字段已写入当前文档 · 未保存到磁盘";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void Undo()
    {
        if (_document == null) return;
        var index = SelectedDefinition?.Entry.Index;
        var path = SelectedField?.Field.Path;
        if (_document.Undo()) { RefreshAll(index, path); Status = "已撤销"; }
    }

    public void Redo()
    {
        if (_document == null) return;
        var index = SelectedDefinition?.Entry.Index;
        var path = SelectedField?.Field.Path;
        if (_document.Redo()) { RefreshAll(index, path); Status = "已重做"; }
    }

    public void ReplaceRawXml(string raw)
    {
        if (_document == null) throw new InvalidOperationException("No XML document is open.");
        var previousDefName = SelectedDefinition?.Entry.DefName;
        _document.ReplaceSource(raw);
        RefreshAll();
        if (previousDefName != null)
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Entry.DefName == previousDefName);
        Status = "完整 XML 已重新解析为一次可撤销修改 · 尚未写入磁盘";
    }

    public void AddDefinition()
    {
        if (_document == null) { Status = "请先新建或打开 XML 文件"; return; }
        try
        {
            var entry = _document.AddDefinition(NewDefType, NewDefName.Trim());
            RefreshAll(entry.Index);
            NewDefName = "MP_NewMech" + (Definitions.Count + 1);
            Status = $"已建立 {entry.Type} · {entry.DefName}，请按需要补充游戏字段";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public string? GetExportText()
    {
        if (_document == null) { Status = "请先新建或打开 XML 文件"; return null; }
        var blocking = _document.Validate().FirstOrDefault(x => x.Severity == Severity.Error);
        if (blocking != null)
        {
            Status = $"导出被阻止 · {blocking.Code}: {blocking.Message}";
            return null;
        }
        return _document.Export();
    }

    public void Saved(string path, string exported)
    {
        if (_document == null) return;
        var index = SelectedDefinition?.Entry.Index;
        var fieldPath = SelectedField?.Field.Path;
        _document.MarkSaved();
        SourceName = path;
        RefreshAll(index, fieldPath);
        Status = $"已导出 · {path}";
    }

    public async Task LoadCatalogAsync(string folder)
    {
        IsBusy = true;
        Status = "正在索引本地 RimWorld / Mod 定义 · 可在其他窗口继续操作";
        try
        {
            _catalog = await DefCatalog.ScanAsync(folder);
            FilterCatalog();
            Status = $"本地定义索引完成 · {_catalog.Count} 项 · 只读";
        }
        catch (Exception ex) { ReportError(ex); }
        finally { IsBusy = false; }
    }

    public void ReportError(Exception ex) =>
        Status = $"操作失败 · {ex.GetType().Name}: {ex.Message}";
}

public sealed class DefinitionRow(DefEntry entry)
{
    public DefEntry Entry { get; } = entry;
    public string Display => Entry.DisplayName;
}

public sealed class FieldRow(DefField field)
{
    public DefField Field { get; } = field;
    public string Display => Field.DisplayName;
}

public sealed class BodyRow(BodyPartEntry part)
{
    public string Name => part.IndentedName;
    public string Coverage => part.CoverageText;
    public string Effective => part.EffectiveText;
    public string Layers => $"{part.Height} / {part.DepthLayer}";
}

public sealed class DiagnosticRow(Diagnostic diagnostic)
{
    public string Display => diagnostic.ToString();
}

public sealed class CatalogRow(CatalogEntry entry)
{
    public CatalogEntry Entry { get; } = entry;
    public string Display => $"[{entry.SourcePackage}] {entry.Identity.Type}  ·  {entry.Identity.DefName}";
}
