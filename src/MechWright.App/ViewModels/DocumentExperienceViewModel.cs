using System.Collections.ObjectModel;
using System.Globalization;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Single-surface, novice-friendly configuration workflow. Normal choices map
/// to real native paths; structural XML names remain in Advanced tools.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<DocumentNodeRow> DocumentVisibleNodes { get; } = new();
    public ObservableCollection<DocumentQuickOption> DocumentQuickOptions { get; } = new();
    public ObservableCollection<string> DocumentQuickChoices { get; } = new();

    public IReadOnlyList<string> DocumentQuickCategories { get; } =
        InspectorFieldCatalog.Categories;

    private readonly Dictionary<string, DocumentNodeRow> _documentRowDrafts = new();
    private DefDocument? _documentRowOwner;
    private string? _documentRowOwnerName;
    private string _documentFieldSearch = "";
    private string _documentQuickSearch = "";
    private string _documentQuickCategory = "常用";
    private string _documentQuickCount = "";
    private string _documentLabelDraft = "";
    private string _documentQuickText = "";
    private decimal? _documentQuickNumber;
    private bool? _documentQuickBoolean;
    private string? _documentQuickChoice;
    private DocumentQuickOption? _selectedDocumentQuickOption;
    private bool _documentQuickPanelOpen;
    private bool _documentStructurePanelOpen;
    private bool _documentCreatePanelOpen;
    private string _documentQuickMessage = "";

    public string DocumentObjectKind => SelectedDefinition?.Entry.Type switch
    {
        "ThingDef" => "机械体 / 物品",
        "PawnKindDef" => "生成种类",
        "BodyDef" => "身体结构",
        "BodyPartDef" => "身体部件",
        "RecipeDef" => "配方",
        "AbilityDef" => "能力",
        _ => "自定义内容"
    };
    public string DocumentObjectHeading
    {
        get
        {
            if (SelectedDefinition == null) return "配置列表";
            var label = SelectedDefinition.Entry.Label;
            return string.IsNullOrWhiteSpace(label) ||
                label.Equals(SelectedDefinition.Entry.DefName, StringComparison.Ordinal)
                    ? DocumentObjectKind : label;
        }
    }
    public string DocumentObjectIdentity => SelectedDefinition?.Entry.DefName ?? "";
    public bool DocumentIsEmpty => _document != null && Definitions.Count == 0;
    public bool DocumentHasEditableRows => DocumentVisibleNodes.Count != 0;
    public bool DocumentHasNoFields => DocumentHasDefinition && !DocumentHasEditableRows;
    public bool DocumentQuickPanelOpen
    {
        get => _documentQuickPanelOpen;
        private set { _documentQuickPanelOpen = value; Notify(); }
    }
    public bool DocumentStructurePanelOpen
    {
        get => _documentStructurePanelOpen;
        private set { _documentStructurePanelOpen = value; Notify(); }
    }
    public bool DocumentCreatePanelOpen
    {
        get => _documentCreatePanelOpen;
        private set { _documentCreatePanelOpen = value; Notify(); }
    }
    public string DocumentFieldSearch
    {
        get => _documentFieldSearch;
        set { _documentFieldSearch = value ?? ""; Notify(); RebuildDocumentVisibleNodes(); }
    }
    public string DocumentQuickSearch
    {
        get => _documentQuickSearch;
        set
        {
            _documentQuickSearch = value ?? "";
            Notify();
            if (_documentQuickSearch.Trim().Length > 0 && _documentQuickCategory == "常用")
            {
                _documentQuickCategory = "全部";
                Notify(nameof(DocumentQuickCategory));
            }
            RebuildQuickOptions();
        }
    }
    public string DocumentQuickCategory
    {
        get => _documentQuickCategory;
        set { _documentQuickCategory = value ?? "常用"; Notify(); RebuildQuickOptions(); }
    }
    public string DocumentQuickCount
    {
        get => _documentQuickCount;
        private set { _documentQuickCount = value; Notify(); }
    }
    public bool DocumentQuickHasOptions => DocumentQuickOptions.Count > 0;
    public string DocumentLabelDraft
    {
        get => _documentLabelDraft;
        set { _documentLabelDraft = value ?? ""; Notify(); }
    }
    public string DocumentQuickText
    {
        get => _documentQuickText;
        set { _documentQuickText = value ?? ""; Notify(); Notify(nameof(DocumentCanAddQuickOption)); }
    }
    public decimal? DocumentQuickNumber
    {
        get => _documentQuickNumber;
        set { _documentQuickNumber = value; Notify(); Notify(nameof(DocumentCanAddQuickOption)); }
    }
    public bool? DocumentQuickBoolean
    {
        get => _documentQuickBoolean;
        set { _documentQuickBoolean = value; Notify(); Notify(nameof(DocumentCanAddQuickOption)); }
    }
    public string? DocumentQuickChoice
    {
        get => _documentQuickChoice;
        set { _documentQuickChoice = value; Notify(); Notify(nameof(DocumentCanAddQuickOption)); }
    }
    public DocumentQuickOption? SelectedDocumentQuickOption
    {
        get => _selectedDocumentQuickOption;
        set
        {
            _selectedDocumentQuickOption = value;
            DocumentQuickText = "";
            DocumentQuickNumber = null;
            DocumentQuickBoolean = null;
            DocumentQuickChoice = null;
            DocumentQuickChoices.Clear();
            foreach (var choice in value?.Choices ?? []) DocumentQuickChoices.Add(choice);
            Notify();
            Notify(nameof(DocumentQuickIsText));
            Notify(nameof(DocumentQuickIsNumber));
            Notify(nameof(DocumentQuickIsBoolean));
            Notify(nameof(DocumentQuickIsChoice));
            Notify(nameof(DocumentQuickSelectionHint));
            Notify(nameof(DocumentCanAddQuickOption));
        }
    }
    public bool DocumentQuickIsNumber => SelectedDocumentQuickOption?.Kind is "Number" or "Integer";
    public bool DocumentQuickIsBoolean => SelectedDocumentQuickOption?.Kind == "Boolean";
    public bool DocumentQuickIsChoice => SelectedDocumentQuickOption?.Kind == "Enum";
    public bool DocumentQuickIsText => SelectedDocumentQuickOption != null &&
        !DocumentQuickIsNumber && !DocumentQuickIsBoolean && !DocumentQuickIsChoice;
    public bool DocumentCanAddQuickOption => SelectedDocumentQuickOption != null &&
        (DocumentQuickIsNumber ? DocumentQuickNumber.HasValue :
         DocumentQuickIsBoolean ? DocumentQuickBoolean.HasValue :
         DocumentQuickIsChoice ? DocumentQuickChoice != null :
         !string.IsNullOrWhiteSpace(DocumentQuickText));
    public string DocumentQuickSelectionHint => SelectedDocumentQuickOption == null
        ? "选择一项配置，填写数值后加入当前列表。"
        : SelectedDocumentQuickOption.Hint + " · " + SelectedDocumentQuickOption.SourceLabel;
    public string DocumentQuickMessage
    {
        get => _documentQuickMessage;
        private set { _documentQuickMessage = value; Notify(); }
    }

    private void RefreshDocumentExperience()
    {
        var ownerName = SelectedDefinition?.Entry.DefName;
        if (!ReferenceEquals(_documentRowOwner, _document) ||
            _documentRowOwnerName != ownerName)
        {
            _documentRowOwner = _document;
            _documentRowOwnerName = ownerName;
            _documentRowDrafts.Clear();
        }
        foreach (var row in DocumentNodes.Where(x => x.IsEdited))
            _documentRowDrafts[row.Path] = row;
        RebuildDocumentVisibleNodes();
        var labelNode = SelectedDefinition == null ? null :
            _document?.Xml.Root?.Elements().FirstOrDefault(x =>
                x.Element("defName")?.Value == SelectedDefinition.Entry.DefName)
                ?.Element("label");
        DocumentLabelDraft = labelNode?.Value ?? "";
        Notify(nameof(DocumentObjectKind));
        Notify(nameof(DocumentObjectHeading));
        Notify(nameof(DocumentObjectIdentity));
        Notify(nameof(DocumentIsEmpty));
        Notify(nameof(DocumentHasNoFields));
        RebuildQuickOptions();
    }

    private void RebuildDocumentVisibleNodes()
    {
        DocumentVisibleNodes.Clear();
        var q = DocumentFieldSearch.Trim();
        foreach (var row in DocumentNodes.Where(x =>
                     !x.IsRoot && !x.Node.IsIdentity && x.Name != "label" &&
                     (q.Length == 0 ||
                      x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                      x.Name.Contains(q, StringComparison.OrdinalIgnoreCase))))
            DocumentVisibleNodes.Add(row);
        Notify(nameof(DocumentHasEditableRows));
        Notify(nameof(DocumentHasNoFields));
    }

    public void ToggleDocumentQuickPanel()
    {
        DocumentQuickPanelOpen = !DocumentQuickPanelOpen;
        if (DocumentQuickPanelOpen)
        {
            DocumentQuickMessage = "";
            DocumentStructurePanelOpen = false;
            RebuildQuickOptions();
        }
    }

    public void ToggleDocumentStructurePanel(DocumentNodeRow? anchor = null)
    {
        if (anchor != null) DocumentSelectedNode = anchor;
        DocumentStructurePanelOpen = !DocumentStructurePanelOpen || anchor != null;
        if (DocumentStructurePanelOpen) DocumentQuickPanelOpen = false;
    }

    public void ToggleDocumentCreatePanel() =>
        DocumentCreatePanelOpen = !DocumentCreatePanelOpen;

    /// <summary>
    /// Standard workflow: a new XML can go straight into its property list.
    /// The minimal ThingDef identity is created only when editing begins.
    /// Other Def types remain available through optional choices.
    /// </summary>
    public void BeginDocumentConfiguration()
    {
        if (_document == null) return;
        if (SelectedDefinition == null) CreateQuickDefinition("ThingDef");
        if (SelectedDefinition != null && !DocumentQuickPanelOpen)
            ToggleDocumentQuickPanel();
    }

    public void CreateQuickDefinition(string defType)
    {
        if (_document == null) { Status = "先新建 XML 文件。"; return; }
        try
        {
            var prefix = defType switch
            {
                "ThingDef" => "MP_Thing",
                "PawnKindDef" => "MP_Kind",
                "BodyDef" => "MP_Body",
                "BodyPartDef" => "MP_Part",
                "RecipeDef" => "MP_Recipe",
                _ => "MP_Custom"
            };
            var used = _workspace == null
                ? _document.Definitions().Select(x => x.DefName)
                : _workspace.Files.SelectMany(x => x.Document.Definitions()).Select(x => x.DefName);
            var names = used.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var number = 1;
            string name;
            do { name = prefix + number.ToString("000", CultureInfo.InvariantCulture); number++; }
            while (names.Contains(name));
            var entry = DocumentWorksheetEditor.AppendDefinition(_document, defType, name, "");
            RefreshAll(entry.Index);
            DocumentCreatePanelOpen = false;
            DocumentQuickPanelOpen = false;
            DocumentMessage = "已添加内容。先填写上方名称，再添加需要的配置。";
            Status = DocumentMessage;
        }
        catch (Exception ex) { DocumentMessage = ex.Message; ReportError(ex); }
    }

    public void SaveDocumentDisplayName()
    {
        if (_document == null || SelectedDefinition == null) return;
        try
        {
            var index = SelectedDefinition.Entry.Index;
            DocumentWorksheetEditor.SetDisplayLabel(_document, SelectedDefinition.Entry,
                DocumentLabelDraft);
            RefreshAll(index);
            DocumentMessage = "名称已保存。";
            Status = DocumentMessage;
        }
        catch (Exception ex) { DocumentMessage = ex.Message; ReportError(ex); }
    }

    private void RebuildQuickOptions()
    {
        var previousPath = SelectedDocumentQuickOption?.Path;
        DocumentQuickOptions.Clear();
        if (SelectedDefinition == null || _document == null)
        {
            SelectedDocumentQuickOption = null;
            DocumentQuickCount = "";
            Notify(nameof(DocumentQuickHasOptions));
            return;
        }

        var descriptors = InspectorFieldCatalog.For(
            SelectedDefinition.Entry.Type, _nativeFields);
        var occupied = descriptors.Where(x => DocumentPathExists(x.Path))
            .Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
        var filtered = InspectorFieldCatalog.Filter(
            descriptors, DocumentQuickCategory, DocumentQuickSearch, occupied, 300);
        foreach (var field in filtered)
            DocumentQuickOptions.Add(new DocumentQuickOption(
                field.Path, field.Label, field.Category, field.Hint, field.Kind,
                field.SourceLabel, field.Choices, field.IsObserved));
        var current = DocumentQuickOptions.FirstOrDefault(x => x.Path == previousPath);
        if (current != null && SelectedDocumentQuickOption?.Path == current.Path)
            _selectedDocumentQuickOption = current;
        else SelectedDocumentQuickOption = null;
        DocumentQuickCount = DocumentQuickOptions.Count == 0
            ? "当前条件没有可添加的字段，可以换一个分类或搜索词。"
            : "可添加 " + DocumentQuickOptions.Count + " 项";
        Notify(nameof(SelectedDocumentQuickOption));
        Notify(nameof(DocumentCanAddQuickOption));
        Notify(nameof(DocumentQuickHasOptions));
    }

    private bool DocumentPathExists(string path)
    {
        if (SelectedDefinition == null || _document == null) return false;
        var cursor = _document.Xml.Root?.Elements().FirstOrDefault(x =>
            x.Element("defName")?.Value == SelectedDefinition.Entry.DefName);
        foreach (var segment in path.Split('/')) cursor = cursor?.Element(segment);
        return cursor != null;
    }

    public void ApplyDocumentQuickOption()
    {
        if (_document == null || SelectedDefinition == null ||
            SelectedDocumentQuickOption is not { } option || !DocumentCanAddQuickOption) return;
        try
        {
            var value = DocumentQuickIsNumber ? DocumentQuickNumber!.Value.ToString(
                    CultureInfo.InvariantCulture)
                : DocumentQuickIsBoolean ? DocumentQuickBoolean!.Value ? "true" : "false"
                : DocumentQuickIsChoice ? DocumentQuickChoice! : DocumentQuickText;
            DocumentWorksheetEditor.AppendFieldPath(_document, SelectedDefinition.Entry,
                option.Path, value);
            var index = SelectedDefinition.Entry.Index;
            RefreshAll(index);
            DocumentQuickPanelOpen = false;
            DocumentMessage = "已添加“" + option.Title + "”。可以继续调整，支持撤销。";
            Status = DocumentMessage;
        }
        catch (Exception ex) { DocumentQuickMessage = ex.Message; ReportError(ex); }
    }
}

public sealed record DocumentQuickOption(
    string Path, string Title, string Category, string Hint,
    string Kind, string SourceLabel, IReadOnlyList<string> Choices,
    bool IsObserved)
{
    public string ShortCategory => Category;
}
