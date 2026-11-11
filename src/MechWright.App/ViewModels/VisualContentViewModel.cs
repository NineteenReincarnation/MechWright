using System.Collections.ObjectModel;
using System.Globalization;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// The universal visual "add content" dialog is backed by the same native
/// DefDocument state and undo stack as the guided workbench.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private NativeListTemplateCatalog _contentTemplates = NativeListTemplateCatalog.Empty;
    private IReadOnlyList<VisualContentEditor.Option> _contentOptions =
        VisualContentEditor.All(NativeFieldCatalog.Empty);

    public ObservableCollection<ContentCategoryRow> ContentCategories { get; } =
        new([
            new("全部内容","▦"),
            new("身份与描述","✎"),
            new("基础性能","◈"),
            new("机械师与生成","♙"),
            new("武器与战斗","⚔"),
            new("外观与音效","◉"),
            new("组件与能力","⬡"),
            new("其他原生属性","⋯")
        ]);
    public ObservableCollection<ContentFieldRow> ContentFields { get; } = new();
    public ObservableCollection<ContentTemplateRow> ContentTemplates { get; } = new();
    public ObservableCollection<ContentModuleRow> InstalledContentModules { get; } = new();
    public ObservableCollection<ContentParameterRow> ContentParameters { get; } = new();
    public ObservableCollection<string> ContentChoices { get; } = new();

    private ContentCategoryRow? _selectedContentCategory;
    private ContentFieldRow? _selectedContentField;
    private ContentTemplateRow? _selectedContentTemplate;
    private ContentModuleRow? _selectedContentModule;
    private ContentParameterRow? _selectedContentParameter;
    private string _contentSearch = "";
    private bool _showObservedContent;
    private string _contentText = "";
    private decimal _contentNumber;
    private bool _contentBoolean;
    private string? _contentChoice;
    private string _contentModuleText = "";
    private decimal _contentModuleNumber;
    private bool _contentModuleBoolean;
    private string _contentStatus = "选择一项内容，修改参数，点击应用即可。";
    private bool _contentIsOpen;

    public bool ContentIsOpen
    {
        get => _contentIsOpen;
        private set { _contentIsOpen = value; Notify(); }
    }
    public string ContentStatus
    {
        get => _contentStatus;
        private set { _contentStatus = value; Notify(); }
    }
    public string ContentSearch
    {
        get => _contentSearch;
        set { _contentSearch = value ?? ""; Notify(); FilterContent(); }
    }
    public bool ShowObservedContent
    {
        get => _showObservedContent;
        set { _showObservedContent = value; Notify(); FilterContent(); }
    }
    public ContentCategoryRow? SelectedContentCategory
    {
        get => _selectedContentCategory;
        set
        {
            if (ReferenceEquals(_selectedContentCategory,value)) return;
            _selectedContentCategory = value;
            Notify();
            FilterContent();
        }
    }
    public ContentFieldRow? SelectedContentField
    {
        get => _selectedContentField;
        set
        {
            _selectedContentField = value;
            Notify();
            Notify(nameof(ContentHasField));
            Notify(nameof(ContentIsNumber));
            Notify(nameof(ContentIsBoolean));
            Notify(nameof(ContentIsText));
            Notify(nameof(ContentIsChoice));
            Notify(nameof(ContentCurrentValue));
            Notify(nameof(ContentSelectedHelp));
            ContentChoices.Clear();
            foreach (var option in value?.Spec.Choices ?? []) ContentChoices.Add(option);
            var existing = value == null || CurrentMechName == null || _workspace == null
                ? null : VisualContentEditor.Read(_workspace,CurrentMechName,value.Spec);
            var suggestion = existing ?? value?.Spec.Suggestion ?? "";
            ContentText = suggestion;
            ContentBoolean = bool.TryParse(suggestion,out var flag) && flag;
            ContentChoice = suggestion;
            ContentNumber = decimal.TryParse(suggestion,NumberStyles.Float,
                CultureInfo.InvariantCulture,out var n) ? n : 0;
        }
    }
    public ContentTemplateRow? SelectedContentTemplate
    {
        get => _selectedContentTemplate;
        set { _selectedContentTemplate = value; Notify(); Notify(nameof(ContentHasTemplate)); }
    }
    public ContentModuleRow? SelectedContentModule
    {
        get => _selectedContentModule;
        set
        {
            _selectedContentModule = value;
            Notify();
            Notify(nameof(ContentHasModule));
            ContentParameters.Clear();
            if (value != null)
            {
                try
                {
                    foreach (var param in VisualContentEditor.Parameters(value.XmlFragment))
                        ContentParameters.Add(new ContentParameterRow(param));
                }
                catch (Exception e) { ContentStatus = "无法读取参数：" + e.Message; }
            }
            SelectedContentParameter = ContentParameters.FirstOrDefault();
        }
    }
    public ContentParameterRow? SelectedContentParameter
    {
        get => _selectedContentParameter;
        set
        {
            _selectedContentParameter = value;
            Notify();
            Notify(nameof(ContentHasParameter));
            Notify(nameof(ContentParamIsNumber));
            Notify(nameof(ContentParamIsBoolean));
            Notify(nameof(ContentParamIsText));
            var text=value?.Parameter.Value ?? "";
            ContentModuleText = text;
            ContentModuleBoolean = bool.TryParse(text,out var b) && b;
            ContentModuleNumber = decimal.TryParse(text,NumberStyles.Float,
                CultureInfo.InvariantCulture,out var n) ? n : 0;
        }
    }

    public bool ContentHasField => SelectedContentField != null;
    public bool ContentHasTemplate => SelectedContentTemplate != null;
    public bool ContentHasModule => SelectedContentModule != null;
    public bool ContentHasParameter => SelectedContentParameter != null;
    public bool ContentIsNumber => SelectedContentField?.Spec.Kind is "Integer" or "Number";
    public bool ContentIsBoolean => SelectedContentField?.Spec.Kind == "Boolean";
    public bool ContentIsChoice => SelectedContentField?.Spec.Kind == "Enum" &&
        SelectedContentField.Spec.Choices is { Length: > 0 };
    public bool ContentIsText => ContentHasField &&
        !ContentIsNumber && !ContentIsBoolean && !ContentIsChoice;
    public bool ContentParamIsNumber => SelectedContentParameter?.Parameter.Kind is "Integer" or "Number";
    public bool ContentParamIsBoolean => SelectedContentParameter?.Parameter.Kind == "Boolean";
    public bool ContentParamIsText => ContentHasParameter && !ContentParamIsNumber && !ContentParamIsBoolean;
    public bool HasNativeTemplates => _contentTemplates.All.Count > 0;
    public string ContentCurrentValue
    {
        get
        {
            var name=CurrentMechName;
            if (name == null || _workspace == null || SelectedContentField == null) return "尚未设置";
            var existing=VisualContentEditor.Read(_workspace,name,SelectedContentField.Spec);
            return existing == null ? "未单独设置 · 游戏可能继承基础值" : "当前已设置：" + existing;
        }
    }
    public string ContentSelectedHelp => SelectedContentField == null
        ? "选择左侧的内容，右侧会显示输入控件和详细说明。"
        : SelectedContentField.Spec.Help +
            (SelectedContentField.Spec.Verified ? " · 原生常见配置" :
                " · 仅从本机 XML 样本观察到，类型未完整验证");

    public string ContentText { get=>_contentText; set { _contentText=value??"";Notify(); } }
    public decimal ContentNumber { get=>_contentNumber;set { _contentNumber=value;Notify(); } }
    public bool ContentBoolean { get=>_contentBoolean;set { _contentBoolean=value;Notify(); } }
    public string? ContentChoice { get=>_contentChoice;set { _contentChoice=value;Notify(); } }
    public string ContentModuleText { get=>_contentModuleText;set { _contentModuleText=value??"";Notify(); } }
    public decimal ContentModuleNumber { get=>_contentModuleNumber;set { _contentModuleNumber=value;Notify(); } }
    public bool ContentModuleBoolean { get=>_contentModuleBoolean;set { _contentModuleBoolean=value;Notify(); } }

    public void OpenContentEditor(string? category=null)
    {
        ContentIsOpen = true;
        _contentOptions = VisualContentEditor.All(_nativeFields);
        SelectedContentCategory = ContentCategories.FirstOrDefault(x => x.Title ==
            (category ?? WorkflowStep switch
            {
                0 => "基础性能",
                1 => "其他原生属性",
                2 => "武器与战斗",
                3 => "外观与音效",
                _ => "全部内容"
            })) ?? ContentCategories[0];
        FilterContent();
        RefreshInstalledContent();
    }

    public void CloseContentEditor() => ContentIsOpen=false;

    private void RefreshContentCatalog(DefCatalog.CatalogScan scan)
    {
        _contentOptions=VisualContentEditor.All(scan.NativeFields);
        _contentTemplates=scan.ListTemplates;
        Notify(nameof(HasNativeTemplates));
        if (ContentIsOpen) FilterContent();
    }

    private static string TemplateCategory(string container) => container switch
    {
        "verbs" or "tools" or "weaponTags" => "武器与战斗",
        "comps" or "modExtensions" or "abilities" => "组件与能力",
        "recipes" => "机械师与生成",
        "apparelTags" or "tradeTags" or "costList" => "其他原生属性",
        _ => "其他原生属性"
    };

    private void FilterContent()
    {
        var category=SelectedContentCategory?.Title ?? "全部内容";
        var match=ContentSearch.Trim();
        var oldField=SelectedContentField?.Spec;
        var oldTemplate=SelectedContentTemplate?.Template;
        ContentFields.Clear();
        ContentTemplates.Clear();
        foreach (var field in _contentOptions)
        {
            if (category != "全部内容" && field.Category != category) continue;
            if (!ShowObservedContent && !field.Verified) continue;
            if (match.Length>0 &&
                !field.Label.Contains(match,StringComparison.OrdinalIgnoreCase) &&
                !field.Path.Contains(match,StringComparison.OrdinalIgnoreCase) &&
                !field.Help.Contains(match,StringComparison.OrdinalIgnoreCase)) continue;
            ContentFields.Add(new ContentFieldRow(field));
            if (ContentFields.Count >= 500) break;
        }
        foreach (var item in _contentTemplates.All)
        {
            if (category != "全部内容" && TemplateCategory(item.Container) != category)
                continue;
            if (match.Length>0 &&
                !item.Label.Contains(match,StringComparison.OrdinalIgnoreCase) &&
                !item.FromUnit.Contains(match,StringComparison.OrdinalIgnoreCase) &&
                !item.Container.Contains(match,StringComparison.OrdinalIgnoreCase)) continue;
            ContentTemplates.Add(new ContentTemplateRow(item,TemplateCategory(item.Container)));
            if (ContentTemplates.Count>=500)break;
        }
        SelectedContentField=ContentFields.FirstOrDefault(x=>x.Spec==oldField) ??
            ContentFields.FirstOrDefault();
        SelectedContentTemplate=ContentTemplates.FirstOrDefault(x=>x.Template==oldTemplate) ??
            ContentTemplates.FirstOrDefault();
        RefreshInstalledContent();
    }

    private string? ContentValue()
    {
        var spec=SelectedContentField?.Spec;
        return spec?.Kind switch
        {
            "Number" => ContentNumber.ToString(CultureInfo.InvariantCulture),
            "Integer" => ContentNumber.ToString("0",CultureInfo.InvariantCulture),
            "Boolean" => ContentBoolean ? "true" : "false",
            "Enum" when spec.Choices is { Length:>0 } => ContentChoice,
            _ => ContentText
        };
    }

    public void ApplySelectedContentField()
    {
        if (_workspace==null || CurrentMechName==null || SelectedContentField==null)return;
        try
        {
            var value=ContentValue() ?? "";
            VisualContentEditor.Apply(_workspace,CurrentMechName,SelectedContentField.Spec,value);
            AfterContentEdit("已应用“"+SelectedContentField.Spec.Label+"”，可以撤销。");
        }
        catch(Exception ex){ ContentStatus=ex.Message; ReportError(ex);}
    }

    public void RemoveSelectedContentField()
    {
        if (_workspace==null || CurrentMechName==null || SelectedContentField==null)return;
        try
        {
            VisualContentEditor.Remove(_workspace,CurrentMechName,SelectedContentField.Spec);
            AfterContentEdit("已移除显式配置；未修改其他字段。");
        }
        catch(Exception ex){ContentStatus=ex.Message;ReportError(ex);}
    }

    public void InstallSelectedContentTemplate()
    {
        if (_workspace==null || CurrentMechName==null || SelectedContentTemplate==null)return;
        try
        {
            NativeListTemplateCatalog.Install(_workspace,CurrentMechName,
                SelectedContentTemplate.Template);
            AfterContentEdit("已添加“"+SelectedContentTemplate.Title+
                "”；如引用了其他 Def，请在导出检查中验证依赖。");
        }
        catch(Exception ex){ContentStatus=ex.Message;ReportError(ex);}
    }

    private void RefreshInstalledContent()
    {
        var selected=SelectedContentModule;
        InstalledContentModules.Clear();
        if(_workspace!=null && CurrentMechName is { } name)
        {
            var category=SelectedContentCategory?.Title ?? "全部内容";
            foreach(var type in new[]{"ThingDef","PawnKindDef"})
            {
                var file=_workspace.FindDefinitionFile(type,name);
                var def=file?.Document.Definitions().FirstOrDefault(x=>x.Type==type &&
                    x.DefName==name);
                if(file==null || def==null)continue;
                foreach(var group in NativeListEditor.Lists(file.Document,def)
                             .Where(x=>x.Exists && x.ItemCount>0))
                {
                    if(category!="全部内容" && TemplateCategory(group.Container)!=category)continue;
                    foreach(var item in NativeListEditor.Items(file.Document,def,group.Container))
                    {
                        if(ContentSearch.Length>0 &&
                           !item.Summary.Contains(ContentSearch,StringComparison.OrdinalIgnoreCase) &&
                           !group.Container.Contains(ContentSearch,StringComparison.OrdinalIgnoreCase))
                            continue;
                        InstalledContentModules.Add(new ContentModuleRow(
                            type,group.Container,item.XmlPath,item.Summary,item.Source));
                    }
                }
            }
        }
        SelectedContentModule=InstalledContentModules.FirstOrDefault(x=>
            x.DefType==selected?.DefType && x.Path==selected?.Path) ??
            InstalledContentModules.FirstOrDefault();
    }

    public void DuplicateSelectedContentModule()
    {
        EditInstalledModule((doc,def,module)=>
            NativeListEditor.Duplicate(doc,def,module.Container,module.Path),
            "已复制配置项，可独立调整参数。");
    }

    public void RemoveSelectedContentModule()
    {
        EditInstalledModule((doc,def,module)=>
        {
            NativeListEditor.Remove(doc,def,module.Container,module.Path);
            return null;
        },"已移除配置项，可以撤销。");
    }

    public void ApplySelectedModuleParameter()
    {
        if(SelectedContentParameter==null)return;
        var value=SelectedContentParameter.Parameter.Kind switch
        {
            "Number" or "Integer" => ContentModuleNumber.ToString(CultureInfo.InvariantCulture),
            "Boolean" => ContentModuleBoolean ? "true":"false",
            _ => ContentModuleText
        };
        EditInstalledModule((doc,def,module)=>
        {
            VisualContentEditor.UpdateModuleParameter(doc,def,module.Container,
                module.Path,SelectedContentParameter.Parameter.Path,value);
            return module.Path;
        },"已更改该配置项的参数，其他设置保持不变。");
    }

    private void EditInstalledModule(
        Func<DefDocument,DefEntry,ContentModuleRow,string?> action,string success)
    {
        if(_workspace==null || CurrentMechName==null || SelectedContentModule==null)return;
        try
        {
            var module=SelectedContentModule;
            var file=_workspace.FindDefinitionFile(module.DefType,CurrentMechName)!;
            var def=file.Document.Definitions().Single(x=>x.Type==module.DefType &&
                x.DefName==CurrentMechName);
            action(file.Document,def,module);
            AfterContentEdit(success);
        }
        catch(Exception ex){ContentStatus=ex.Message;ReportError(ex);}
    }

    private void AfterContentEdit(string message)
    {
        var index=SelectedDefinition?.Entry.Index;
        RefreshAll(index);
        RefreshVisualDesigner();
        RefreshInstalledContent();
        Notify(nameof(ContentCurrentValue));
        RefreshVisualReview();
        ContentStatus=message;
        Status=message;
    }
}

public sealed record ContentCategoryRow(string Title,string Symbol);
public sealed record ContentFieldRow(VisualContentEditor.Option Spec)
{
    public string Title=>Spec.Label;
    public string Location=>Spec.DefType=="PawnKindDef"?"生成与种类":"机体属性";
    public string Detail=>Location+"  ·  "+(Spec.Verified?"已验证":"原版样例");
    public string Icon=>Spec.Kind switch
    {
        "Number" or "Integer"=>"◫",
        "Boolean"=>"◉",
        "Enum"=>"▤",
        _=>"✎"
    };
}
public sealed record ContentTemplateRow(
    NativeListTemplateCatalog.Template Template,string Category)
{
    public string Title=>Template.Label;
    public string Detail=>Template.SourcePackage+" · 来源："+Template.FromUnit;
    public string Icon=>Template.Container switch
    {
        "verbs" or "tools"=>"⚔",
        "comps" or "modExtensions"=>"⬡",
        "abilities"=>"✦",
        _=>"▦"
    };
}
public sealed record ContentModuleRow(string DefType,string Container,
    string Path,string Title,string XmlFragment)
{
    public string Detail=>Container switch
    {
        "tools"=>"已安装 · 近战攻击",
        "verbs"=>"已安装 · 远程攻击",
        "comps"=>"已安装 · 组件",
        "modExtensions"=>"已安装 · 扩展",
        "abilities"=>"已安装 · 能力",
        _=>"已安装 · "+Container
    };
}
public sealed record ContentParameterRow(VisualContentEditor.ModuleParameter Parameter)
{
    public string Title=>Parameter.Label;
    public string Detail=>Parameter.Value;
}
