using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Data-first, vertically ordered visual worksheet: the project XML is the
/// authoritative model. Every row owns a separate draft and an explicit apply
/// action; no chassis preset ever overwrites a user's choices.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<WorksheetFieldRow> WorksheetFields { get; } = new();
    private string _worksheetSearch = "";
    private string _worksheetMessage = "从上到下填写；每一行都可以单独保存或恢复为继承值。";
    public string WorksheetMessage
    {
        get => _worksheetMessage;
        private set { _worksheetMessage = value; Notify(); }
    }
    public string WorksheetSearch
    {
        get => _worksheetSearch;
        set
        {
            _worksheetSearch = value ?? "";
            Notify();
            RefreshWorksheet();
        }
    }
    public string WorksheetCount => WorksheetFields.Count + " 个可编辑项目";

    private void RefreshWorksheet()
    {
        var name = CurrentMechName;
        var previous = WorksheetFields.ToDictionary(x =>
            (x.Spec.DefType, x.Spec.Path), x => x);
        WorksheetFields.Clear();
        if (name == null || _workspace == null)
        {
            Notify(nameof(WorksheetCount));
            return;
        }
        var available = _contentOptions.ToDictionary(x => (x.DefType,x.Path));
        foreach (var field in VisualContentEditor.Common)
            available[(field.DefType,field.Path)] = field;

        // Discover user-inserted fields even when the external game catalog
        // is not connected: an imported Mod may use completely new scalar names.
        foreach (var type in new[] { "ThingDef", "PawnKindDef" })
        {
            var file = _workspace.FindDefinitionFile(type,name);
            var def = file?.Document.Definitions()
                .FirstOrDefault(x => x.Type == type && x.DefName == name);
            if (def == null) continue;
            foreach (var leaf in def.Element.Descendants()
                .Where(x => !x.HasElements &&
                    x.Nodes().All(n => n is XText or XCData)))
            {
                var ancestors=leaf.Ancestors().TakeWhile(x=>x!=def.Element)
                    .Reverse().ToArray();
                var steps=ancestors.Select(x=>x.Name.LocalName)
                    .Append(leaf.Name.LocalName).ToArray();
                if (steps.Contains("li",StringComparer.Ordinal) ||
                    steps.Length>5 || steps.Length==0 ||
                    steps[0]=="defName" || steps[^1]=="defName") continue;
                var path=string.Join('/',steps);
                if (available.ContainsKey((type,path)))continue;
                var known=StandardFieldRules.Describe(steps[^1]);
                var kind=known?.Kind ?? (bool.TryParse(leaf.Value,out _)
                    ? "Boolean"
                    : decimal.TryParse(leaf.Value,NumberStyles.Float,
                        CultureInfo.InvariantCulture,out _) ? "Number" : "Text");
                available[(type,path)]=new VisualContentEditor.Option(
                    type,path,known?.Description ?? steps[^1],
                    VisualContentEditor.CategoryOf(type,path),kind,
                    leaf.Value, known?.Hint ?? "工程中的自定义原生字段",
                    "当前工程",known!=null, known?.Allowed);
            }
        }
        var order=new[]
        {
            "身份与描述","基础性能","机械师与生成","武器与战斗",
            "外观与音效","组件与能力","其他原生属性"
        };
        var ordered=available.Values.Where(x => 
                VisualContentEditor.Common.Contains(x) ||
                VisualContentEditor.Read(_workspace,name,x)!=null)
            .Where(x => WorksheetSearch.Length==0 ||
                x.Label.Contains(WorksheetSearch,StringComparison.OrdinalIgnoreCase) ||
                x.Path.Contains(WorksheetSearch,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>Array.IndexOf(order,x.Category) is var pos && pos>=0?pos:99)
            .ThenBy(x=>VisualContentEditor.Common.IndexOf(x) is var i && i>=0?i:1000)
            .ThenBy(x=>x.Label,StringComparer.Ordinal).ToArray();

        string? lastCategory=null;
        foreach (var field in ordered)
        {
            var saved=VisualContentEditor.Read(_workspace,name,field);
            var header=lastCategory!=field.Category;
            lastCategory=field.Category;
            // Only retain a draft if it was deliberately modified and the
            // original underlying XML value has not changed in the meantime.
            var key=(field.DefType,field.Path);
            if (previous.TryGetValue(key,out var prior) && prior.IsEdited &&
                prior.SavedValue==saved)
                WorksheetFields.Add(prior with { ShowCategoryHeader=header });
            else WorksheetFields.Add(new WorksheetFieldRow(field,saved,header));
        }
        Notify(nameof(WorksheetCount));
    }

    public void SaveWorksheetField(WorksheetFieldRow? row)
    {
        if (row==null || CurrentMechName==null || _workspace==null)return;
        try
        {
            var text=row.ExplicitValue();
            VisualContentEditor.Apply(_workspace,CurrentMechName,row.Spec,text);
            RefreshAll(SelectedDefinition?.Entry.Index);
            RefreshWorksheet();
            WorksheetMessage="已保存“"+row.Spec.Label+"”，可以撤销。";
            Status=WorksheetMessage;
        }
        catch(Exception ex){WorksheetMessage=ex.Message;ReportError(ex);}
    }
    public void RemoveWorksheetField(WorksheetFieldRow? row)
    {
        if (row==null || CurrentMechName==null || _workspace==null)return;
        try
        {
            VisualContentEditor.Remove(_workspace,CurrentMechName,row.Spec);
            RefreshAll(SelectedDefinition?.Entry.Index);
            RefreshWorksheet();
            WorksheetMessage="已取消单独设置“"+row.Spec.Label+"”；恢复原版继承行为。";
            Status=WorksheetMessage;
        }
        catch(Exception ex){WorksheetMessage=ex.Message;ReportError(ex);}
    }
}

public sealed record WorksheetFieldRow : INotifyPropertyChanged
{
    public VisualContentEditor.Option Spec { get; init; }
    public string? SavedValue { get; }
    public bool ShowCategoryHeader { get; init; }
    private string _text;
    private decimal _number;
    private bool _boolean;
    private string? _choice;
    private bool _isEdited;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? member=null) =>
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(member));

    public WorksheetFieldRow(VisualContentEditor.Option spec,string? saved,bool header)
    {
        Spec=spec;
        SavedValue=saved;
        ShowCategoryHeader=header;
        var text=saved??spec.Suggestion;
        _text=text;
        _number=decimal.TryParse(text,NumberStyles.Float,
            CultureInfo.InvariantCulture,out var n)?n:0;
        _boolean=bool.TryParse(text,out var b)&&b;
        _choice=text;
    }
    public string Category => Spec.Category;
    public string Title => Spec.Label;
    public string Hint => Spec.Help;
    public string SourceLabel => Spec.DefType=="PawnKindDef"?"种类与生成":"机体定义";
    public bool IsNumber => Spec.Kind is "Number" or "Integer";
    public bool IsBoolean => Spec.Kind=="Boolean";
    public bool IsChoice => Spec.Kind=="Enum" && Spec.Choices is { Length:>0 };
    public bool IsText => !IsNumber && !IsBoolean && !IsChoice;
    public IReadOnlyList<string> Choices => Spec.Choices ?? [];
    public bool CanReset => SavedValue!=null;
    public string CurrentStatus => SavedValue==null?"尚未单独设置 · 当前显示建议值":"已写入当前工程";
    public bool IsEdited => _isEdited;
    public string Text
    {
        get=>_text;
        set{if(_text==value)return;_text=value??"";Changed();}
    }
    public decimal Number
    {
        get=>_number;
        set{if(_number==value)return;_number=value;Changed();}
    }
    public bool Boolean
    {
        get=>_boolean;
        set{if(_boolean==value)return;_boolean=value;Changed();}
    }
    public string? Choice
    {
        get=>_choice;
        set{if(_choice==value)return;_choice=value;Changed();}
    }
    private void Changed()
    {
        _isEdited=true;
        Notify();
        Notify(nameof(IsEdited));
    }
    public string ExplicitValue() => Spec.Kind switch
    {
        "Number" => Number.ToString(CultureInfo.InvariantCulture),
        "Integer" => Number==decimal.Truncate(Number)
            ? Number.ToString("0",CultureInfo.InvariantCulture)
            : throw new ArgumentException("这个属性需要整数。"),
        "Boolean" => Boolean?"true":"false",
        "Enum" when IsChoice => Choice??throw new ArgumentException("请先选择一个选项。"),
        _=>Text
    };
}
