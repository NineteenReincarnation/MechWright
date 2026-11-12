using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Primary file-first editor. The current DefDocument is the only source of
/// truth; every row is generated from real XML, including unfamiliar nodes.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private bool _isDocumentMode = true;
    public bool IsDocumentMode => _isDocumentMode;
    public bool IsGuidedMode => !_isDocumentMode;

    public void UseDocumentMode()
    {
        _isDocumentMode = true;
        Notify(nameof(IsDocumentMode));
        Notify(nameof(IsGuidedMode));
        Notify(nameof(WorkbenchArea));
        RefreshDocumentWorksheet();
    }

    public void UseGuidedMode()
    {
        if (CurrentMechName == null)
        {
            Status = "当前文件没有可关联的完整机械体，先继续编辑 XML 定义。";
            return;
        }
        if (HasUncommittedDocumentInputs && !CommitDocumentInputs()) return;
        _isDocumentMode = false;
        Notify(nameof(IsDocumentMode));
        Notify(nameof(IsGuidedMode));
        StartDesign(CurrentMechName);
        Notify(nameof(WorkbenchArea));
    }

    public ObservableCollection<DocumentNodeRow> DocumentNodes { get; } = new();
    public ObservableCollection<DocumentFieldChoice> DocumentFieldChoices { get; } = new();
    public IReadOnlyList<DocumentDefTypeChoice> DocumentDefinitionTypes { get; } =
        FreeformDefinitionEditor.CommonTypes
            .Select(x => new DocumentDefTypeChoice(x.Type, x.Label)).ToArray();

    private DocumentDefTypeChoice? _documentSelectedType;
    private DocumentNodeRow? _documentSelectedNode;
    private DocumentFieldChoice? _documentSelectedField;
    private string _documentDefName = "MP_NewDefinition";
    private string _documentDefLabel = "";
    private string _documentCustomFieldName = "";
    private string _documentNewValue = "";
    private string _documentMessage = "这是一个空白 XML。添加第一项定义后，直接在下面逐行填写。";

    public bool DocumentHasDefinition => SelectedDefinition != null && _document != null;
    public bool DocumentHasNodes => DocumentNodes.Count != 0;
    public string DocumentFileTitle => _document == null ? "未打开文件" :
        Path.GetFileName(SourceName);
    public string DocumentDefinitionTitle => SelectedDefinition == null
        ? "此文件尚无定义" : SelectedDefinition.Entry.DefName + " · " + SelectedDefinition.Entry.Type;
    public string DocumentNodeCount => DocumentNodes.Count + " 个结构节点";

    public DocumentDefTypeChoice? DocumentSelectedType
    {
        get => _documentSelectedType;
        set { _documentSelectedType = value; Notify(); }
    }
    public string DocumentDefName
    {
        get => _documentDefName;
        set { _documentDefName = value ?? ""; Notify(); }
    }
    public string DocumentDefLabel
    {
        get => _documentDefLabel;
        set { _documentDefLabel = value ?? ""; Notify(); }
    }
    public string DocumentCustomFieldName
    {
        get => _documentCustomFieldName;
        set { _documentCustomFieldName = value ?? ""; Notify(); }
    }
    public string DocumentNewValue
    {
        get => _documentNewValue;
        set { _documentNewValue = value ?? ""; Notify(); }
    }
    public string DocumentMessage
    {
        get => _documentMessage;
        private set { _documentMessage = value; Notify(); }
    }

    public DocumentNodeRow? DocumentSelectedNode
    {
        get => _documentSelectedNode;
        set
        {
            if (ReferenceEquals(_documentSelectedNode, value)) return;
            _documentSelectedNode = value;
            Notify();
            RefreshDocumentFieldChoices();
            Notify(nameof(DocumentInsertLocation));
        }
    }
    public DocumentFieldChoice? DocumentSelectedField
    {
        get => _documentSelectedField;
        set
        {
            _documentSelectedField = value;
            Notify();
            Notify(nameof(DocumentUsesCustomName));
            Notify(nameof(DocumentNewFieldHint));
        }
    }
    public bool DocumentUsesCustomName => DocumentSelectedField == null ||
        DocumentSelectedField.Name == "";
    public string DocumentNewFieldHint => DocumentSelectedField?.IsGroup == true
        ? "分组可以继续添加任意数量的子项。" :
        "只填写这一项，不会改动其他字段。";
    public string DocumentInsertLocation
    {
        get
        {
            var current = DocumentSelectedNode;
            if (current == null || current.IsRoot) return "位置：当前定义下";
            return DocumentWorksheetEditor.CanContainFields(current.Node)
                ? "位置：" + current.Name + " 内部"
                : "位置：与“" + current.Name + "”同级";
        }
    }

    private static readonly DocumentFieldChoice[] RootSuggestions =
    [
        new("label", "显示名称", false),
        new("description", "说明文字", false),
        new("statBases", "基础属性分组", true),
        new("race", "种族属性分组", true),
        new("graphicData", "外观分组", true),
        new("tools", "近战工具列表", true),
        new("verbs", "远程攻击列表", true),
        new("comps", "组件列表", true),
        new("modExtensions", "扩展列表", true),
        new("", "自定义字段或分组…", false)
    ];
    private static readonly DocumentFieldChoice[] StatSuggestions =
    [
        new("MoveSpeed", "移动速度", false),
        new("Mass", "质量", false),
        new("MarketValue", "市场价值", false),
        new("ArmorRating_Sharp", "锐器护甲", false),
        new("ArmorRating_Blunt", "钝器护甲", false),
        new("ArmorRating_Heat", "热能护甲", false),
        new("BandwidthCost", "控制带宽", false),
        new("", "自定义字段或分组…", false)
    ];
    private static readonly DocumentFieldChoice[] RaceSuggestions =
    [
        new("body", "身体结构引用", false),
        new("baseBodySize", "身体大小", false),
        new("baseHealthScale", "部件生命倍率", false),
        new("mechWeightClass", "重量类型", false),
        new("needsRest", "需要休息", false),
        new("hasGenders", "有性别", false),
        new("", "自定义字段或分组…", false)
    ];
    private static readonly DocumentFieldChoice[] ListSuggestions =
    [
        new("li", "新增列表项", true),
        new("", "自定义字段或分组…", false)
    ];
    private static readonly DocumentFieldChoice[] GenericSuggestions =
    [
        new("def", "定义引用", false),
        new("label", "显示名称", false),
        new("power", "伤害或效能", false),
        new("cooldownTime", "冷却时间", false),
        new("coverage", "覆盖率", false),
        new("parts", "子部件列表", true),
        new("", "自定义字段或分组…", false)
    ];

    private void RefreshDocumentFieldChoices()
    {
        var previous = DocumentSelectedField?.Name;
        DocumentFieldChoices.Clear();
        var current = DocumentSelectedNode;
        var parent = current != null && DocumentWorksheetEditor.CanContainFields(current.Node)
            ? current?.Name : DocumentNodes.FirstOrDefault(x =>
                x.Path == DocumentWorksheetEditor.ParentPath(current?.Path ?? ""))?.Name;
        var suggestions = parent switch
        {
            "statBases" => StatSuggestions,
            "race" => RaceSuggestions,
            "tools" or "verbs" or "comps" or "modExtensions" or "parts" => ListSuggestions,
            "li" => GenericSuggestions,
            _ when current?.IsRoot != false || parent == SelectedDefinition?.Entry.Type
                => RootSuggestions,
            _ => GenericSuggestions
        };
        foreach (var item in suggestions) DocumentFieldChoices.Add(item);
        DocumentSelectedField = DocumentFieldChoices.FirstOrDefault(x => x.Name == previous)
            ?? DocumentFieldChoices.FirstOrDefault();
    }

    private void RefreshDocumentWorksheet(string? preferNode = null)
    {
        if (DocumentSelectedType == null)
            DocumentSelectedType = DocumentDefinitionTypes.FirstOrDefault();
        var oldNode = preferNode ?? DocumentSelectedNode?.Path;
        var sameDefinition = ReferenceEquals(_documentRowOwner, _document) &&
            _documentRowOwnerName == SelectedDefinition?.Entry.DefName;
        var drafts = sameDefinition ? DocumentNodes.Where(x => x.IsEdited)
            .ToDictionary(x => x.Path, x => x) :
            new Dictionary<string, DocumentNodeRow>();
        DocumentNodes.Clear();
        if (_document != null && SelectedDefinition != null)
        {
            var known = InspectorFieldCatalog.For(SelectedDefinition.Entry.Type, _nativeFields, _gameFields)
                .ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var node in FreeformDefinitionEditor.Read(_document, SelectedDefinition.Entry))
            {
                if (drafts.TryGetValue(node.Path, out var prior) &&
                    prior.SavedValue == node.Value)
                    DocumentNodes.Add(prior);
                else
                {
                    var path = InspectorFieldCatalog.ToRelativePath(node.Path);
                    known.TryGetValue(path ?? "", out var description);
                    DocumentNodes.Add(new DocumentNodeRow(node, description));
                }
            }
        }
        DocumentSelectedNode = DocumentNodes.FirstOrDefault(x => x.Path == oldNode)
            ?? DocumentNodes.FirstOrDefault();
        Notify(nameof(DocumentHasDefinition));
        Notify(nameof(DocumentHasNodes));
        Notify(nameof(DocumentNodeCount));
        Notify(nameof(DocumentDefinitionTitle));
        Notify(nameof(DocumentFileTitle));
        Notify(nameof(DocumentInsertLocation));
        RefreshDocumentFieldChoices();
        RefreshDocumentExperience();
    }

    public void AppendDocumentDefinition()
    {
        if (!CommitDocumentInputs()) return;
        if (_document == null)
        {
            DocumentMessage = "先新建 XML 文件。";
            return;
        }
        try
        {
            var type = DocumentSelectedType?.Type ?? "ThingDef";
            var label = DocumentDefLabel.Trim();
            var name = DocumentDefName.Trim();
            if (_workspace != null && _workspace.Files.SelectMany(x => x.Document.Definitions())
                    .Any(x => x.DefName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("当前工程已存在相同的定义标识。");
            var newDef = DocumentWorksheetEditor.AppendDefinition(_document, type, name, label);
            RefreshAll(newDef.Index);
            DocumentDefLabel = "";
            var next = 1;
            while ((_workspace?.Files.SelectMany(x => x.Document.Definitions()) ??
                        _document.Definitions()).Any(x => x.DefName == "MP_NewDefinition" + next))
                next++;
            DocumentDefName = "MP_NewDefinition" + next;
            DocumentMessage = "已加入 " + name + "。下方可以逐行增加和修改字段。";
            Status = DocumentMessage;
        }
        catch (Exception ex) { DocumentMessage = ex.Message; ReportError(ex); }
    }

    public void AddDocumentField()
    {
        if (_document == null || SelectedDefinition == null) return;
        try
        {
            var selected = DocumentSelectedField;
            var name = selected?.Name is { Length: > 0 } chosen
                ? chosen : DocumentCustomFieldName.Trim();
            var group = selected?.Name is { Length: > 0 }
                ? selected.IsGroup : DocumentCustomIsGroup;
            var parent = DocumentSelectedNode?.Path ??
                FreeformDefinitionEditor.Read(_document, SelectedDefinition.Entry).First().Path;
            var newPath = DocumentWorksheetEditor.AddField(_document,
                SelectedDefinition.Entry, parent, name, DocumentNewValue, group);
            var index = SelectedDefinition.Entry.Index;
            RefreshAll(index);
            RefreshDocumentWorksheet(newPath);
            DocumentNewValue = "";
            DocumentMessage = "已添加“" + name + "”到本文件；可撤销。";
            Status = DocumentMessage;
        }
        catch (Exception ex) { DocumentMessage = ex.Message; ReportError(ex); }
    }

    private bool _documentCustomIsGroup;
    public bool DocumentCustomIsGroup
    {
        get => _documentCustomIsGroup;
        set { _documentCustomIsGroup = value; Notify(); }
    }

    /// <summary>
    /// One operation in the workbench is editing the list. The XML is updated
    /// in memory when a cell is committed (or when the user exports), not
    /// written to disk until the top-level Save XML action.
    /// </summary>
    public bool HasUncommittedDocumentInputs =>
        HasInlinePendingEdits ||
        DocumentNodes.Any(x => x.CanEdit && x.IsEdited) ||
        (_document != null && SelectedDefinition != null &&
            DocumentLabelDraft != (_document.Xml.Root?.Elements()
                .FirstOrDefault(x => x.Element("defName")?.Value ==
                    SelectedDefinition.Entry.DefName)?.Element("label")?.Value ?? ""));

    public bool CommitDocumentInputs()
    {
        if (_document == null || SelectedDefinition == null) return true;
        if (!CommitInlineInspector()) return false;
        try
        {
            var changes = DocumentNodes.Where(x => x.CanEdit && x.IsEdited &&
                x.GetValue() != x.SavedValue)
                .Select(x => (x.Path, Value: x.GetValue(), x.Name)).ToArray();
            // Validate all first; an invalid last row must not partially
            // commit the earlier rows.
            foreach (var change in changes)
            {
                var error = StandardFieldRules.Validate(change.Name, change.Value);
                if (error != null) throw new ArgumentException(error);
            }
            var label = _document.Xml.Root?.Elements()
                .FirstOrDefault(x => x.Element("defName")?.Value ==
                    SelectedDefinition.Entry.DefName)?.Element("label")?.Value ?? "";
            var changeLabel = DocumentLabelDraft != label;
            if (changeLabel && string.IsNullOrWhiteSpace(DocumentLabelDraft))
                throw new ArgumentException("名称不能留空。请填写名称或保留原有内容。");

            if (changes.Length == 0 && !changeLabel) return true;
            var index = SelectedDefinition.Entry.Index;
            foreach (var change in changes)
                DocumentWorksheetEditor.UpdateValue(
                    _document, SelectedDefinition.Entry, change.Path, change.Value);
            if (changeLabel)
                DocumentWorksheetEditor.SetDisplayLabel(
                    _document, SelectedDefinition.Entry, DocumentLabelDraft);
            RefreshAll(index);
            Status = "已应用 " + (changes.Length + (changeLabel ? 1 : 0)) +
                " 项修改 · 使用顶部保存 XML 写入文件";
            return true;
        }
        catch (Exception ex)
        {
            DocumentMessage = "输入未应用：" + ex.Message;
            ReportError(ex);
            return false;
        }
    }

    public void SaveDocumentNode(DocumentNodeRow? row)
    {
        if (_document == null || SelectedDefinition == null || row == null ||
            !row.CanEdit || !row.IsEdited || !DocumentNodes.Contains(row)) return;
        try
        {
            var value = row.GetValue();
            if (value == row.SavedValue) return;
            var path = row.Path;
            var index = SelectedDefinition.Entry.Index;
            DocumentWorksheetEditor.UpdateValue(_document, SelectedDefinition.Entry, path, value);
            RefreshAll(index);
            RefreshDocumentWorksheet(path);
            DocumentMessage = "已保存“" + row.Name + "”，可撤销。";
            Status = DocumentMessage;
        }
        catch (Exception ex) { DocumentMessage = ex.Message; ReportError(ex); }
    }

    public void RemoveDocumentNode(DocumentNodeRow? row)
    {
        if (_document == null || SelectedDefinition == null || row == null || !row.CanDelete) return;
        try
        {
            var index = SelectedDefinition.Entry.Index;
            DocumentWorksheetEditor.RemoveField(_document, SelectedDefinition.Entry, row.Path);
            RefreshAll(index);
            DocumentMessage = "字段已移除，可撤销。";
            Status = DocumentMessage;
        }
        catch (Exception ex) { DocumentMessage = ex.Message; ReportError(ex); }
    }
}

public sealed record DocumentDefTypeChoice(string Type, string Label)
{
    public string Display => Label + " · " + Type;
}
public sealed record DocumentFieldChoice(string Name, string Title, bool IsGroup)
{
    public string Display => Title;
}
public sealed class DocumentNodeRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    public FreeformDefinitionEditor.Node Node { get; }
    public string Path => Node.Path;
    public int Depth => Node.Depth;
    public string Name => Node.Name;
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Notify();
            Notify(nameof(ExpandLabel));
        }
    }
    public string ExpandLabel => _isExpanded ? "折叠" : "展开";
    private readonly InspectorFieldCatalog.Field? _metadata;
    public string ContextHint => _metadata?.Hint ?? "";
    public string SourceHint => _metadata?.SourceLabel ?? "";
    public bool HasContextHint => ContextHint.Length > 0;
    public bool IsRoot => Node.IsRoot;
    public bool IsGroup => Node.IsGroup ||
        (string.IsNullOrEmpty(Node.Value) && Node.Name is
            ("statBases" or "race" or "graphicData" or "tools" or "verbs" or
             "comps" or "parts" or "li"));
    public bool CanEdit => !Node.IsRoot && !IsGroup && !Node.IsIdentity;
    public bool CanDelete => !Node.IsRoot && !Node.IsIdentity && Node.Name != "label";
    public string Title => _metadata?.Label ?? (Node.Name switch
    {
        "defName" => "定义标识",
        "label" => "显示名称",
        "description" => "说明文字",
        "statBases" => "基础属性",
        "race" => "机体属性",
        "graphicData" => "外观与显示",
        "tools" => "近战工具",
        "verbs" => "远程攻击",
        "comps" => "附加组件",
        "modExtensions" => "扩展内容",
        "parts" => "身体部件",
        "li" => "列表项",
        _ => StandardFieldRules.Describe(Node.Name)?.Description ?? Node.Name
    });
    public string StructureHint => IsGroup ? "包含子项目" :
        Node.Attributes.Length > 0 ? Node.Attributes : "";
    public bool HasStructureHint => StructureHint.Length > 0;
    public Avalonia.Thickness RowIndent => new(
        Math.Clamp(Node.Depth - 1, 0, 5) * 13, 0, 0, 0);
    public string Indent => new string(' ', Math.Min(12, Node.Depth) * 2);
    public string? SavedValue => Node.Value;
    public bool IsEdited { get; private set; }
    private string _text;
    private decimal _number;
    private bool _boolean;
    private string? _choice;
    private readonly string _kind;
    private readonly string[] _choices;
    public bool IsNumber => CanEdit && (_kind is "Number" or "Integer");
    public bool IsBoolean => CanEdit && _kind == "Boolean";
    public bool IsChoice => CanEdit && _kind == "Enum" && _choices.Length > 0;
    public bool IsText => CanEdit && !IsNumber && !IsBoolean && !IsChoice;
    public IReadOnlyList<string> Choices => _choices;
    public string Text
    {
        get => _text;
        set { if (_text == value) return; _text = value ?? ""; Changed(); }
    }
    public decimal Number
    {
        get => _number;
        set { if (_number == value) return; _number = value; Changed(); }
    }
    public bool Boolean
    {
        get => _boolean;
        set { if (_boolean == value) return; _boolean = value; Changed(); }
    }
    public string? Choice
    {
        get => _choice;
        set { if (_choice == value) return; _choice = value; Changed(); }
    }
    private void Changed() { IsEdited = true; Notify(nameof(IsEdited)); }
    public DocumentNodeRow(FreeformDefinitionEditor.Node node,
        InspectorFieldCatalog.Field? metadata = null)
    {
        Node = node;
        _metadata = metadata;
        var rule = StandardFieldRules.Describe(node.Name);
        _choices = rule?.Allowed ?? metadata?.Choices.ToArray() ?? [];
        _kind = rule?.Kind ?? metadata?.Kind ??
            (bool.TryParse(node.Value, out _) ? "Boolean" : "Text");
        _text = node.Value ?? "";
        _number = decimal.TryParse(_text, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var value) ? value : 0;
        _boolean = bool.TryParse(_text, out var flag) && flag;
        _choice = _text;
    }
    public string GetValue() => _kind switch
    {
        "Number" => Number.ToString(CultureInfo.InvariantCulture),
        "Integer" => Number == decimal.Truncate(Number)
            ? Number.ToString("0", CultureInfo.InvariantCulture)
            : throw new ArgumentException("请输入整数。"),
        "Boolean" => Boolean ? "true" : "false",
        "Enum" when IsChoice => Choice ?? "",
        _ => Text
    };
}
