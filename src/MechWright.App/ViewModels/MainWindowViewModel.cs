using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MechWright.Core;
using MechWright.RimWorld;
using MechWright.Infrastructure;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel : INotifyPropertyChanged
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
    private ReferenceRow? _selectedReference;
    private MechPropertyRow? _selectedMechProperty;
    private string _propertyValue = "";

    public ObservableCollection<DefinitionRow> Definitions { get; } = new();
    public ObservableCollection<DefinitionRow> VisibleDefinitions { get; } = new();
    public ObservableCollection<FieldRow> Fields { get; } = new();
    public ObservableCollection<BodyRow> BodyParts { get; } = new();
    public ObservableCollection<DiagnosticRow> Diagnostics { get; } = new();
    public ObservableCollection<CatalogRow> VisibleCatalog { get; } = new();
    public ObservableCollection<ReferenceRow> References { get; } = new();
    public ObservableCollection<MechPropertyRow> MechProperties { get; } = new();
    public string[] SupportedKinds { get; } = ["ThingDef", "PawnKindDef", "BodyDef", "BodyPartDef", "RecipeDef"];
    private IReadOnlyList<CatalogEntry> _catalog = Array.Empty<CatalogEntry>();

    public bool HasDocument => _document != null;
    public bool IsDirty => _workspace?.IsDirty ?? (_document?.IsDirty ?? false);
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
        private set { _isBusy = value; Notify(); Notify(nameof(VanillaEmptyMessage)); }
    }
    public string DefinitionCount => $"{Definitions.Count} DEFINITIONS";
    public string CatalogCount => $"{_catalog.Count} LOCAL DEFS";
    public string DiagnosticCount => Diagnostics.Count == 0 ? "NO LOCAL ERRORS" : $"{Diagnostics.Count} DIAGNOSTICS";
    public string DirtyFlag => IsDirty ? "● 未保存修改" : "○ 无未保存修改";
    public string SelectedDefName => SelectedDefinition?.Entry.DefName ?? "未选择定义";
    public string SelectedPath => SelectedField?.Field.Path ?? "选择一个字段来编辑";
    public string SelectedFieldHint => SelectedField is null
        ? "选择字段以查看类型和允许值"
        : StandardFieldRules.Describe(SelectedField.Field.Name)?.Hint
          ?? "未收录的原生或扩展字段 · 按 XML 字符串保留，不臆造类型";

    public DefinitionRow? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (_isRefreshingDocument) return;
            if (ReferenceEquals(_selectedDefinition, value)) return;
            // Switching the selected Def is a navigation action. Apply the
            // current list inputs first so moving between definitions cannot
            // silently discard an unfinished edit.
            if (_isDocumentMode && _selectedDefinition != null && _document != null &&
                _selectedDefinition.Entry.DefName != value?.Entry.DefName &&
                HasUncommittedDocumentInputs)
            {
                var target = value?.Entry.DefName;
                if (!CommitDocumentInputs()) return;
                value = Definitions.FirstOrDefault(x => x.Entry.DefName == target);
            }
            _selectedDefinition = value;
            Notify();
            Notify(nameof(SelectedDefName));
            InvalidateVanillaComparison();
            RefreshFields();
            RefreshDocumentWorksheet();
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
            Notify(nameof(SelectedFieldHint));
            EditingValue = value?.Field.Value ?? "";
        }
    }

    public CatalogRow? SelectedCatalog
    {
        get => _selectedCatalog;
        set { _selectedCatalog = value; Notify(); }
    }

    public ReferenceRow? SelectedReference
    {
        get => _selectedReference;
        set { _selectedReference = value; Notify(); }
    }

    public MechPropertyRow? SelectedMechProperty
    {
        get => _selectedMechProperty;
        set
        {
            _selectedMechProperty = value;
            Notify();
            Notify(nameof(PropertyHint));
            PropertyValue = value?.Value ?? value?.Spec.Suggestion ?? "";
        }
    }
    public string PropertyValue
    {
        get => _propertyValue;
        set { _propertyValue = value; Notify(); }
    }
    public string PropertyHint => SelectedMechProperty == null ? "选择标准字段"
        : $"{SelectedMechProperty.Spec.Help} · 建议值：{SelectedMechProperty.Spec.Suggestion}（不自动写出）";

    public void ApplyMechProperty()
    {
        if (_document == null || SelectedDefinition == null || SelectedMechProperty == null) return;
        var index = SelectedDefinition.Entry.Index;
        var field = SelectedMechProperty.Spec;
        try
        {
            MechanoidProperties.Apply(_document, SelectedDefinition.Entry, field, PropertyValue);
            RefreshAll(index);
            SelectedMechProperty = MechProperties.FirstOrDefault(x => x.Spec == field);
            Status = $"已显式写入 {field.Label} = {PropertyValue}；可撤销";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void ResetMechProperty()
    {
        if (_document == null || SelectedDefinition == null || SelectedMechProperty == null) return;
        var index = SelectedDefinition.Entry.Index;
        var field = SelectedMechProperty.Spec;
        try
        {
            MechanoidProperties.Reset(_document, SelectedDefinition.Entry, field);
            RefreshAll(index);
            SelectedMechProperty = MechProperties.FirstOrDefault(x => x.Spec == field);
            Status = $"已移除 {field.Label} 的显式覆写（由原版继承或保持未定义）";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void JumpToReference()
    {
        if (SelectedReference is not { } item) return;
        if (item.Reference.Location == ReferenceLocation.ProjectFile && _workspace != null)
        {
            var file = ProjectFiles.FirstOrDefault(x =>
                string.Equals(x.File.RelativePath, item.Reference.OriginPackage,
                    StringComparison.OrdinalIgnoreCase));
            if (file != null) SelectedProjectFile = file;
        }
        else if (!item.Reference.CanNavigateLocally)
        {
            Status = "引用位于外部 / 未加载目录 · 请先导入相关 Def";
            return;
        }
        var match = Definitions.FirstOrDefault(x => x.Entry.Type == item.Reference.TargetType
                         && x.Entry.DefName == item.Reference.TargetDefName);
        if (match == null) { Status = "本地引用目标已变化，请刷新诊断"; return; }
        SearchQuery = "";
        SelectedDefinition = match;
        Status = $"已定位引用 · {item.Reference.TargetType}/{item.Reference.TargetDefName}";
    }

    public void NewFile()
    {
        SetDocument(DefDocument.CreateEmpty(), "未命名 · Defs.xml");
        Status = "空白 XML 已打开。可直接在列表中添加定义与字段。";
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
        // Opening one standalone XML exits project mode only after the
        // caller's unsaved-changes confirmation.
        _workspace = null;
        ProjectFiles.Clear();
        _selectedProjectFile = null;
        Notify(nameof(HasWorkspace));
        Notify(nameof(WorkspaceTitle));
        Notify(nameof(SelectedProjectFile));
        _document = document;
        SourceName = name;
        RefreshAll();
        UseDocumentMode();
    }

    private bool _isRefreshingDocument;

    private void RefreshAll(int? preferredIndex = null, string? fieldPath = null, string? bodyPath = null)
    {
        // Clearing/repopulating the bound Def list can temporarily set
        // ListBox.SelectedItem to null. Ignore that UI feedback while the
        // underlying document is being rebuilt.
        if (_isRefreshingDocument) return;
        _isRefreshingDocument = true;
        try
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
        RefreshFields(fieldPath, bodyPath);

        InvalidateVanillaComparison();
        XmlPreview = _document?.Export() ?? "";
        Diagnostics.Clear();
        if (_workspace != null)
        {
            foreach (var item in _workspace.Validate())
                Diagnostics.Add(new DiagnosticRow(new Diagnostic(item.Issue.Code,
                    item.Issue.Severity, $"[{item.FilePath}] {item.Issue.Message}", item.Issue.Location)));
        }
        else if (_document != null)
            foreach (var d in _document.Validate()) Diagnostics.Add(new DiagnosticRow(d));
        RefreshWorkspaceIndicators();
        RefreshReferences();
        Notify(nameof(DiagnosticCount));
        Notify(nameof(DirtyFlag));
        Notify(nameof(IsDirty));
        Notify(nameof(CanUndo));
        Notify(nameof(CanRedo));
        Notify(nameof(HasDocument));
        RefreshWorkflowVisibility();
        RefreshDocumentWorksheet();
    
        }
        finally { _isRefreshingDocument = false; }
    }

    private void RefreshFields(string? preferredPath = null, string? bodyPath = null)
    {
        Fields.Clear();
        BodyParts.Clear();
        MechProperties.Clear();
        RefreshCombat();
        RefreshGraphics();
        RefreshXmlAttributes();
        RefreshNativeFields();
        RefreshNativeListEditor();
        if (_document != null && _selectedDefinition != null)
        {
            foreach (var f in _document.Fields(_selectedDefinition.Entry)) Fields.Add(new FieldRow(f));
            foreach (var part in _document.BodyParts(_selectedDefinition.Entry))
                BodyParts.Add(new BodyRow(part));
            foreach (var spec in MechanoidProperties.For(_selectedDefinition.Entry.Type))
                MechProperties.Add(new MechPropertyRow(spec, MechanoidProperties.Read(_selectedDefinition.Entry, spec)));
        }
        SelectedMechProperty = MechProperties.FirstOrDefault();
        SelectedField = preferredPath == null ? Fields.FirstOrDefault()
            : Fields.FirstOrDefault(x => x.Field.Path == preferredPath);
        SelectedBodyPart = bodyPath == null ? BodyParts.FirstOrDefault()
            : BodyParts.FirstOrDefault(x => DefDocument.GetPath(x.Part.Xml) == bodyPath)
                  ?? BodyParts.FirstOrDefault();
    }

    private void RefreshReferences()
    {
        References.Clear();
        if (_document == null) return;
        if (_workspace != null && SelectedProjectFile != null)
        {
            foreach (var reference in _workspace.GetReferences(SelectedProjectFile.File, _catalog))
                References.Add(new ReferenceRow(reference.Reference));
        }
        else
        {
            foreach (var reference in DefinitionReferences.Collect(_document, _catalog))
                References.Add(new ReferenceRow(reference));
        }
        SelectedReference = References.FirstOrDefault();
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
            var validation = StandardFieldRules.Validate(SelectedField.Field.Name, EditingValue);
            if (validation != null) throw new ArgumentException(validation);
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
        if (_workspace != null)
        {
            Status = $"已导出当前 XML 副本 · {path}（工程内其他修改未清除）";
            return;
        }
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
            var scan = await DefCatalog.ScanFullAsync(folder);
            _catalog = scan.Entries;
            FilterCatalog();
            RefreshReferences();
            RefreshVanillaCatalog(scan);
            UseNativeFieldCatalog(scan);
            RefreshContentCatalog(scan);
            RefreshWorksheet();
            SetGestationParentOptions(scan.GestationRecipeParents);
            RefreshVisualGestation();
            RefreshBodyPresetOptions();
            RefreshVisualRangedPresets();
            RefreshVisualPartPresets();
            if (scan.IsGameInstallation)
            {
                RecentGamePath.Remember(folder);
                Status = $"已连接原版游戏目录 · {_vanillaMechs.Count} 台机械体 · {_catalog.Count} 个原生定义";
            }
            else
            {
                Status = "所选路径不是完整的 RimWorld 根目录或 Data 文件夹，请重新选择。";
            }
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
    public string FriendlyTitle =>
        string.IsNullOrWhiteSpace(Entry.Label) ||
        Entry.Label.Equals(Entry.DefName, StringComparison.Ordinal)
            ? Entry.Type switch
            {
                "ThingDef" => "机械体 / 物品",
                "PawnKindDef" => "生成种类",
                "BodyDef" => "身体结构",
                "BodyPartDef" => "身体部件",
                "RecipeDef" => "制作配方",
                _ => "自定义内容"
            }
            : Entry.Label;
    public string TechnicalId => Entry.DefName;
}

public sealed class FieldRow(DefField field)
{
    public DefField Field { get; } = field;
    public string Display => Field.DisplayName;
}

public sealed class BodyRow(BodyPartEntry part)
{
    public BodyPartEntry Part { get; } = part;
    public double EffectivePercent => double.IsFinite(Part.CumulativeCoverage)
        ? Math.Clamp(Part.CumulativeCoverage * 100, 0, 100) : 0;
    public string Name => Part.IndentedName;
    public string Coverage => Part.CoverageText;
    public string Effective => Part.EffectiveText;
    public string Layers => $"{Part.Height} / {Part.DepthLayer}";
}

public sealed class DiagnosticRow(Diagnostic diagnostic)
{
    public string Display => diagnostic.ToString();
}

public sealed class CatalogRow(CatalogEntry entry)
{
    public CatalogEntry Entry { get; } = entry;
    public string Display => $"[{Entry.SourcePackage}] {Entry.Identity.Type}  ·  {Entry.Identity.DefName}";
}

public sealed class ReferenceRow(DefinitionReference reference)
{
    public DefinitionReference Reference { get; } = reference;
    public string Display => $"{Reference.FromDefName}  →  {Reference.TargetType}/{Reference.TargetDefName}";
    public string Location => Reference.Location switch
    {
        ReferenceLocation.CurrentDocument => "当前文件 ✓",
        ReferenceLocation.GameCatalog => $"本地目录 · {Reference.OriginPackage}",
        ReferenceLocation.ProjectFile => $"工程文件 · {Reference.OriginPackage}",
        _ => "待解析 / 外部 Mod"
    };
    public string Path => Reference.FieldPath;
}

public sealed class MechPropertyRow(MechanoidProperties.Spec spec, string? value)
{
    public MechanoidProperties.Spec Spec { get; } = spec;
    public string? Value { get; } = value;
    public string Label => Spec.Label;
    public string DisplayValue => Value == null
        ? $"未显式配置  /  建议 {Spec.Suggestion}"
        : $"显式值  {Value}";
}
