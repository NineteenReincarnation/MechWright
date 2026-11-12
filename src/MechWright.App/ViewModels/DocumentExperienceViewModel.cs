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
        ["常用", "基础属性", "机体", "武器与战斗", "外观与声音", "其他"];

    private readonly Dictionary<string, DocumentNodeRow> _documentRowDrafts = new();
    private DefDocument? _documentRowOwner;
    private string? _documentRowOwnerName;
    private string _documentFieldSearch = "";
    private string _documentQuickSearch = "";
    private string _documentQuickCategory = "常用";
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

    private static readonly DocumentQuickOption[] BuiltInOptions =
    [
        new("description", "说明文字", "常用", "介绍这个对象的用途", "Text"),
        new("statBases/MoveSpeed", "移动速度", "基础属性", "格/秒", "Number"),
        new("statBases/Mass", "质量", "基础属性", "单位 kg", "Number"),
        new("statBases/MarketValue", "市场价值", "基础属性", "物品的市场价值", "Number"),
        new("statBases/ArmorRating_Sharp", "锐器护甲", "基础属性", "如 0.4 表示 40%", "Number"),
        new("statBases/ArmorRating_Blunt", "钝器护甲", "基础属性", "如 0.2 表示 20%", "Number"),
        new("statBases/ArmorRating_Heat", "热能护甲", "基础属性", "对热伤害的护甲", "Number"),
        new("statBases/BandwidthCost", "控制带宽", "机体", "被机械师控制时使用的带宽", "Integer"),
        new("race/baseBodySize", "身体大小", "机体", "与画面贴图大小不同", "Number"),
        new("race/baseHealthScale", "生命倍率", "机体", "部件生命值的倍率", "Number"),
        new("race/mechWeightClass", "机体重量等级", "机体", "原生轻中重分类", "Enum"),
        new("race/body", "身体结构", "机体", "填写已有的 BodyDef 引用标识", "Text"),
        new("combatPower", "战斗点数 CP", "武器与战斗", "用于 PawnKindDef 生成的战斗点数", "Number"),
        new("maxPerGroup", "单组生成数量", "武器与战斗", "PawnKindDef 的生成上限", "Integer"),
        new("allowInMechClusters", "加入机械集群", "武器与战斗", "该类型是否出现在机械集群", "Boolean"),
        new("graphicData/texPath", "贴图路径", "外观与声音", "RimWorld 相对贴图路径", "Text"),
        new("graphicData/drawSize", "贴图绘制大小", "外观与声音", "如 1.5", "Number"),
    ];

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
            if (SelectedDefinition == null) return "选择要编辑的内容";
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
        set { _documentQuickSearch = value ?? ""; Notify(); RebuildQuickOptions(); }
    }
    public string DocumentQuickCategory
    {
        get => _documentQuickCategory;
        set { _documentQuickCategory = value ?? "常用"; Notify(); RebuildQuickOptions(); }
    }
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
            var name = value?.Path.Split('/')[^1];
            foreach (var choice in name == null
                         ? [] : StandardFieldRules.Describe(name)?.Allowed ?? [])
                DocumentQuickChoices.Add(choice);
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
    public string DocumentQuickSelectionHint => SelectedDocumentQuickOption?.Hint ??
        "选择一项配置，填写数值后加入当前列表。";
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
            return;
        }
        var kind = SelectedDefinition.Entry.Type;
        var query = DocumentQuickSearch.Trim();
        var all = BuiltInOptions.Concat(
            VisualContentEditor.All(_nativeFields).Where(x =>
                x.DefType == kind && x.Path.Split('/').All(part =>
                    part != "li" && part != "defName"))
                .Select(x => new DocumentQuickOption(x.Path, x.Label, x.Category,
                    x.Help, x.Kind)));
        var distinct = all.Where(x => x.Path != "label" && x.Path != "defName")
            .DistinctBy(x => x.Path)
            .Where(x => AllowedQuickPath(kind, x.Path))
            .Where(x => !DocumentPathExists(x.Path))
            .Where(x => DocumentQuickCategory == "常用" ||
                x.Category == DocumentQuickCategory || (DocumentQuickCategory == "其他" &&
                    x.Category is "机械师与生成" or "其他原生属性"))
            .Where(x => query.Length == 0 || x.Title.Contains(query,
                StringComparison.OrdinalIgnoreCase) ||
                x.Path.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Hint.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(120).ToArray();
        foreach (var item in distinct) DocumentQuickOptions.Add(item);
        var stillSelected = DocumentQuickOptions.FirstOrDefault(x => x.Path == previousPath);
        if (stillSelected != null) _selectedDocumentQuickOption = stillSelected;
        else SelectedDocumentQuickOption = null;
        Notify(nameof(SelectedDocumentQuickOption));
        Notify(nameof(DocumentCanAddQuickOption));
    }

    private static bool AllowedQuickPath(string kind, string path) => kind switch
    {
        "ThingDef" => path is not ("combatPower" or "maxPerGroup" or
            "allowInMechClusters") && !path.StartsWith("lifeStages/", StringComparison.Ordinal),
        "PawnKindDef" => path is "description" or "combatPower" or
            "maxPerGroup" or "allowInMechClusters" or "controlGroupPortraitZoom" ||
            !path.StartsWith("statBases/", StringComparison.Ordinal) &&
            !path.StartsWith("race/base", StringComparison.Ordinal),
        _ => path == "description" || path.StartsWith("statBases/", StringComparison.Ordinal)
    };

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

public sealed record DocumentQuickOption(string Path, string Title,
    string Category, string Hint, string Kind)
{
    public string ShortCategory => Category;
}
