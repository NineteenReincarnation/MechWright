using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// A true one-list Inspector: all common fields are visible before they exist
/// in XML. Only edited values create XML nodes. No add-field wizard is part
/// of the standard editing path; a search can reveal other discovered fields.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<InlineInspectorRow> InlineInspectorRows { get; } = new();

    private DefDocument? _inlineOwner;
    private string? _inlineOwnerId;
    private string _inlineSearch = "";
    private bool _inlineAdvanced;
    private string _inlineMessage = "";
    private string _inlineIdentity = "";
    private bool _applyingInline;

    public bool InlineAdvancedOpen
    {
        get => _inlineAdvanced;
        private set { _inlineAdvanced = value; Notify(); }
    }

    public string InlineMessage
    {
        get => _inlineMessage;
        private set { _inlineMessage = value; Notify(); }
    }

    public string InlineIdentityDraft
    {
        get => _inlineIdentity;
        set { _inlineIdentity = value ?? ""; Notify(); }
    }

    public string InlineSearch
    {
        get => _inlineSearch;
        set
        {
            if (_inlineSearch == (value ?? "")) return;
            _inlineSearch = value ?? "";
            Notify();
            RefreshInlineInspector();
        }
    }

    public bool HasInlineInspectorRows => InlineInspectorRows.Count > 0;
    public bool HasInlinePendingEdits =>
        InlineInspectorRows.Any(x => x.IsEdited) ||
        (_document != null && SelectedDefinition != null &&
         InlineIdentityDraft != SelectedDefinition.Entry.DefName);

    public void ToggleInlineAdvanced()
    {
        InlineAdvancedOpen = !InlineAdvancedOpen;
        if (!InlineAdvancedOpen)
        {
            if (DocumentQuickPanelOpen) ToggleDocumentQuickPanel();
            if (DocumentStructurePanelOpen) ToggleDocumentStructurePanel();
        }
    }

    private void RefreshInlineInspector()
    {
        var ownerId = SelectedDefinition?.Entry.DefName;
        var sameOwner = ReferenceEquals(_inlineOwner, _document) &&
                        _inlineOwnerId == ownerId;
        var drafts = sameOwner
            ? InlineInspectorRows.Where(x => x.IsEdited)
                .ToDictionary(x => x.Path, StringComparer.Ordinal)
            : new Dictionary<string, InlineInspectorRow>(StringComparer.Ordinal);

        if (!sameOwner)
        {
            _inlineOwner = _document;
            _inlineOwnerId = ownerId;
            _inlineAdvanced = false;
            _inlineSearch = "";
            Notify(nameof(InlineAdvancedOpen));
            Notify(nameof(InlineSearch));
            InlineMessage = "";
        }

        InlineInspectorRows.Clear();
        InlineIdentityDraft = ownerId ?? "";
        if (_document == null || SelectedDefinition == null)
        {
            Notify(nameof(HasInlineInspectorRows));
            return;
        }

        var fields = InspectorFieldCatalog.For(
            SelectedDefinition.Entry.Type, _nativeFields, _gameFields);
        var definitions = fields
            // Main surface = readable starter rows. Other discovered fields
            // appear as real input rows when searched, never in a modal picker.
            .Where(x => x.IsStarter || _inlineSearch.Trim().Length > 0)
            .Where(x => x.Path != "label" && x.Path != "defName")
            .ToDictionary(x => x.Path, StringComparer.Ordinal);

        // Never hide an existing ordinary scalar, including unfamiliar mod
        // values, even if its name isn't in the built-in schema.
        foreach (var node in FreeformDefinitionEditor.Read(_document, SelectedDefinition.Entry))
        {
            if (node.IsRoot || node.IsGroup || node.IsIdentity ||
                node.Name is "label" or "defName") continue;
            var relative = InspectorFieldCatalog.ToRelativePath(node.Path);
            if (relative == null || definitions.ContainsKey(relative)) continue;
            if (relative.Split('/').Any(x => x == "li")) continue;
            var rule = StandardFieldRules.Describe(node.Name);
            definitions.Add(relative, new InspectorFieldCatalog.Field(
                SelectedDefinition.Entry.Type, relative,
                rule?.Description ?? node.Name, "其他",
                rule?.Kind ?? (bool.TryParse(node.Value, out _) ? "Boolean" : "Text"),
                "已有 XML 配置", "当前 XML", true, rule?.Allowed ?? [], false));
        }

        string? previousCategory = null;
        var search = _inlineSearch.Trim();
        foreach (var field in definitions.Values
                     .OrderBy(x => x.Category switch
                     {
                         "常用" => 0, "基础属性" => 1, "机体" => 2,
                         "武器与战斗" => 3, "外观与声音" => 4, _ => 5
                     })
                     .ThenBy(x => x.IsStarter ? 0 : 1)
                     .ThenBy(x => x.Label, StringComparer.CurrentCulture))
        {
            if (search.Length > 0 &&
                !field.Label.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !field.Path.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !field.Hint.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;

            var node = SelectedDefinition.Entry.Element;
            foreach (var segment in field.Path.Split('/')) node = node?.Element(segment);
            var saved = node?.HasElements == false ? node.Value : null;
            var row = drafts.TryGetValue(field.Path, out var existing) &&
                      existing.SavedValue == saved
                ? existing
                : new InlineInspectorRow(field, saved);
            row.ShowCategory = field.Category != previousCategory;
            previousCategory = field.Category;
            InlineInspectorRows.Add(row);
        }
        Notify(nameof(HasInlineInspectorRows));
        Notify(nameof(HasInlinePendingEdits));
    }

    public bool CommitInlineInspector(InlineInspectorRow? only = null)
    {
        if (_applyingInline || _document == null || SelectedDefinition == null)
            return true;
        if (only != null && !InlineInspectorRows.Contains(only)) return true;
        var edits = InlineInspectorRows
            .Where(x => x.IsEdited && (only == null || ReferenceEquals(x, only)))
            .Select(x => (Row: x, Value: x.CurrentValue)).ToArray();
        var changeIdentity = only == null &&
            InlineIdentityDraft != SelectedDefinition.Entry.DefName;
        if (edits.Length == 0 && !changeIdentity) return true;

        try
        {
            foreach (var edit in edits)
            {
                if (edit.Value.Length > 4096)
                    throw new ArgumentException(edit.Row.Title + "：输入内容过长。");
                if (edit.Value.Length == 0) continue; // clear optional XML field
                var issue = StandardFieldRules.Validate(edit.Row.Path.Split('/')[^1], edit.Value);
                if (issue != null) throw new ArgumentException(issue);
                if (edit.Row.IsNumber && (!decimal.TryParse(edit.Value,
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                    edit.Row.IsInteger && number != decimal.Truncate(number)))
                    throw new ArgumentException(edit.Row.Title + "：请输入有效数值。");
            }

            var newId = InlineIdentityDraft.Trim();
            if (changeIdentity)
            {
                if (!Regex.IsMatch(newId, @"^MP_[A-Za-z_][A-Za-z0-9_]*$") ||
                    newId.Length > 110)
                    throw new ArgumentException("定义标识应以 MP_ 开头，只能包含英文字母、数字和下划线。");
                if ((_workspace?.Files.SelectMany(x => x.Document.Definitions()) ??
                     _document.Definitions()).Any(x =>
                         x.DefName.Equals(newId, StringComparison.OrdinalIgnoreCase) &&
                         x.DefName != SelectedDefinition.Entry.DefName))
                    throw new InvalidOperationException("当前工程中已存在这个定义标识。");
            }

            _applyingInline = true;
            var index = SelectedDefinition.Entry.Index;
            var document = _document;
            var definition = SelectedDefinition.Entry.Element;
            document.Transform(_ =>
            {
                if (changeIdentity) definition.Element("defName")!.Value = newId;
                foreach (var edit in edits)
                {
                    var segments = edit.Row.Path.Split('/');
                    var current = definition;
                    foreach (var segment in segments[..^1])
                    {
                        var next = current.Element(segment);
                        if (next == null)
                        {
                            if (edit.Value.Length == 0) break;
                            next = new XElement(segment);
                            current.Add(next);
                        }
                        current = next;
                    }

                    var leaf = current.Element(segments[^1]);
                    if (edit.Value.Length == 0)
                    {
                        if (leaf == null) continue;
                        if (leaf.HasElements) throw new InvalidOperationException(
                            edit.Row.Title + " 不是普通数值，请在复杂结构编辑器中操作。");
                        leaf.Remove();
                        // Remove now-empty intermediate XML containers while
                        // preserving attributes, comments and unknown nodes.
                        var parent = current;
                        while (parent != definition && !parent.HasElements &&
                            !parent.HasAttributes && !parent.Nodes().Any(n =>
                                n is XComment or XCData ||
                                n is XText t && !string.IsNullOrWhiteSpace(t.Value)))
                        {
                            var ancestor = parent.Parent;
                            parent.Remove();
                            if (ancestor == null) break;
                            parent = ancestor;
                        }
                    }
                    else if (leaf == null) current.Add(new XElement(segments[^1], edit.Value));
                    else if (!leaf.HasElements) leaf.Value = edit.Value;
                    else throw new InvalidOperationException(
                        edit.Row.Title + " 不是普通数值，请使用复杂结构编辑器。");
                }
            });
            foreach (var edit in edits) edit.Row.Accept(edit.Value);
            _inlineOwnerId = newId;
            RefreshAll(index);
            InlineMessage = "已应用更改，顶部保存后写入文件。";
            Status = InlineMessage;
            return true;
        }
        catch (Exception e)
        {
            InlineMessage = "请检查输入：" + e.Message;
            Status = InlineMessage;
            return false;
        }
        finally { _applyingInline = false; }
    }
}

public sealed class InlineInspectorRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    private string _text = "";
    private decimal? _number;
    private bool? _boolean;
    private string? _choice;
    private bool _showCategory;
    public string Path { get; }
    public string Title { get; }
    public string Category { get; }
    public string Hint { get; }
    public string SavedValue { get; private set; }
    public bool IsSet => SavedValue.Length > 0;
    public IReadOnlyList<string> Choices { get; }
    public bool IsNumber { get; }
    public bool IsInteger { get; }
    public bool IsBoolean { get; }
    public bool IsChoice { get; }
    public bool IsText => !IsNumber && !IsBoolean && !IsChoice;
    public string TypeHint => IsSet ? "已填写" : "可选";
    public bool ShowCategory
    {
        get => _showCategory;
        set { if (_showCategory != value) { _showCategory = value; Changed(); } }
    }
    public string Text
    {
        get => _text;
        set { if (_text != (value ?? "")) { _text = value ?? ""; Changed(); Changed(nameof(IsEdited)); } }
    }
    public decimal? Number
    {
        get => _number;
        set { if (_number != value) { _number = value; Changed(); Changed(nameof(IsEdited)); } }
    }
    public bool? Boolean
    {
        get => _boolean;
        set { if (_boolean != value) { _boolean = value; Changed(); Changed(nameof(IsEdited)); } }
    }
    public string? Choice
    {
        get => _choice;
        set { if (_choice != value) { _choice = value; Changed(); Changed(nameof(IsEdited)); } }
    }
    public string CurrentValue => IsNumber
        ? Number?.ToString(CultureInfo.InvariantCulture) ?? ""
        : IsBoolean ? Boolean is bool b ? b ? "true" : "false" : ""
        : IsChoice ? Choice ?? "" : Text.Trim();
    public bool IsEdited => CurrentValue != SavedValue;

    public InlineInspectorRow(InspectorFieldCatalog.Field field, string? savedValue)
    {
        Path = field.Path;
        Title = field.Label;
        Category = field.Category;
        Hint = field.Hint + " · " + field.Path;
        IsNumber = field.Kind is "Number" or "Integer";
        IsInteger = field.Kind == "Integer";
        IsBoolean = field.Kind == "Boolean";
        IsChoice = field.Kind == "Enum" && field.Choices.Count > 0;
        Choices = field.Choices;
        SavedValue = savedValue ?? "";
        Accept(SavedValue);
    }
    public void Accept(string value)
    {
        SavedValue = value;
        _text = value;
        _number = decimal.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var n) ? n : null;
        _boolean = bool.TryParse(value, out var b) ? b : null;
        _choice = value.Length > 0 ? value : null;
        Changed(nameof(Text));
        Changed(nameof(Number));
        Changed(nameof(Boolean));
        Changed(nameof(Choice));
        Changed(nameof(IsEdited));
        Changed(nameof(IsSet));
        Changed(nameof(TypeHint));
    }
}
