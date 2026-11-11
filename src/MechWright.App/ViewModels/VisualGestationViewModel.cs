using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>A no-XML editor for the vanilla Biotech gestation recipe.</summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<GestationParentRow> GestationParents { get; } =
        new([new("MediumMechanoidRecipe","中型机械培育模板")]);
    public IReadOnlyList<GestationResearchRow> GestationResearchOptions { get; } =
    [
        new("无科技限制","不限制"),
        new("BasicMechtech","基础机械技术"),
        new("HighMechtech","高级机械技术"),
        new("UltraMechtech","顶级机械技术")
    ];
    public IReadOnlyList<GestationMaterialRow> GestationMaterials { get; } =
        VisualGestationEditor.SuggestedMaterials
            .Select(x=>new GestationMaterialRow(x.DefName,x.Label)).ToArray();
    public ObservableCollection<GestationIngredientRow> GestationIngredients { get; }=new();

    private GestationParentRow? _selectedGestationParent;
    private GestationResearchRow? _selectedGestationResearch;
    private GestationMaterialRow? _selectedGestationMaterial;
    private decimal _gestationCycles=4;
    private decimal _newMaterialAmount=10;
    private string _gestationStatus="培育配方需要 Biotech。建议值仅作为起点，点击创建后才写入 XML。";

    public GestationParentRow? SelectedGestationParent
    {
        get=>_selectedGestationParent;
        set{_selectedGestationParent=value;Notify();}
    }
    public GestationResearchRow? SelectedGestationResearch
    {
        get=>_selectedGestationResearch;
        set{_selectedGestationResearch=value;Notify();}
    }
    public GestationMaterialRow? SelectedGestationMaterial
    {
        get=>_selectedGestationMaterial;
        set{_selectedGestationMaterial=value;Notify();}
    }
    public decimal GestationCycles
    {
        get=>_gestationCycles;
        set{_gestationCycles=value;Notify();}
    }
    public decimal NewGestationMaterialAmount
    {
        get=>_newMaterialAmount;
        set{_newMaterialAmount=value;Notify();}
    }
    public string GestationStatus
    {
        get=>_gestationStatus;
        private set{_gestationStatus=value;Notify();}
    }
    public bool HasGestationRecipe =>
        _workspace != null && CurrentMechName != null &&
        VisualGestationEditor.Exists(_workspace,CurrentMechName);
    public string GestationSaveLabel => HasGestationRecipe ? "保存培育配方" : "创建培育配方";

    private void SetGestationParentOptions(IReadOnlyList<string> installed)
    {
        var previouslySelected=SelectedGestationParent?.DefName;
        GestationParents.Clear();
        foreach(var name in installed.DefaultIfEmpty("MediumMechanoidRecipe"))
        {
            var title=name.Contains("Ultra",StringComparison.OrdinalIgnoreCase)
                ? "超重型机械培育"
                : name.Contains("Heavy",StringComparison.OrdinalIgnoreCase)
                ? "重型机械培育"
                : name.Contains("Medium",StringComparison.OrdinalIgnoreCase)
                ? "中型机械培育"
                : name.Contains("Light",StringComparison.OrdinalIgnoreCase)
                ? "轻型机械培育"
                : "机械培育模板";
            GestationParents.Add(new GestationParentRow(name,title));
        }
        SelectedGestationParent=GestationParents.FirstOrDefault(x=>x.DefName==previouslySelected) ??
            GestationParents.FirstOrDefault();
    }

    private void RefreshVisualGestation()
    {
        GestationIngredients.Clear();
        var name=CurrentMechName;
        var existing=name==null || _workspace==null?null:
            VisualGestationEditor.Read(_workspace,name);
        if(existing==null)
        {
            GestationCycles=4;
            SelectedGestationResearch=GestationResearchOptions
                .FirstOrDefault(x=>x.DefName=="HighMechtech");
            foreach(var (item,amount) in new[]
            {
                ("Steel",100),("Plasteel",90),("ComponentIndustrial",6),("SubcoreHigh",1)
            })
                GestationIngredients.Add(new GestationIngredientRow(item,
                    GestationMaterials.First(x=>x.DefName==item).Label,amount));
            var weight=name==null?null:VisualMechDesigner.CurrentString(_workspace!,
                "ThingDef",name,"race/mechWeightClass");
            var preferred=weight switch
            {
                "Light"=>"LightMechanoidRecipe",
                "Heavy"=>"HeavyMechanoidRecipe",
                "UltraHeavy"=>"UltraHeavyMechanoidRecipe",
                _=>"MediumMechanoidRecipe"
            };
            SelectedGestationParent=GestationParents.FirstOrDefault(x=>x.DefName==preferred)
                ?? GestationParents.FirstOrDefault();
        }
        else
        {
            GestationCycles=Math.Max(1,existing.Cycles);
            SelectedGestationParent=GestationParents.FirstOrDefault(x=>
                x.DefName==existing.ParentName)??GestationParents.FirstOrDefault();
            SelectedGestationResearch=GestationResearchOptions.FirstOrDefault(x=>
                x.DefName==existing.Research)??GestationResearchOptions.First();
            foreach(var item in existing.Ingredients)
                GestationIngredients.Add(new GestationIngredientRow(item.DefName,
                    GestationMaterials.FirstOrDefault(x=>x.DefName==item.DefName)?.Label
                        ?? item.DefName,item.Amount));
        }
        SelectedGestationMaterial=GestationMaterials.FirstOrDefault();
        Notify(nameof(HasGestationRecipe));
        Notify(nameof(GestationSaveLabel));
    }

    public void AddGestationMaterial()
    {
        if(SelectedGestationMaterial==null)return;
        if(GestationIngredients.Any(x=>x.DefName==SelectedGestationMaterial.DefName))
        {
            GestationStatus="该材料已经在清单内，可以直接调整它的数量。";
            return;
        }
        if(GestationIngredients.Count>=32)
        {
            GestationStatus="单个配方最多支持 32 种材料。";
            return;
        }
        GestationIngredients.Add(new GestationIngredientRow(
            SelectedGestationMaterial.DefName,SelectedGestationMaterial.Label,
            Math.Max(1,(int)NewGestationMaterialAmount)));
        GestationStatus="材料已加入设计。点击保存配方后才会写入工程。";
    }

    public void RemoveGestationMaterial(GestationIngredientRow? row)
    {
        if(row==null)return;
        GestationIngredients.Remove(row);
        GestationStatus="已从设计列表移除。点击保存配方后才会写入工程。";
    }

    public void SaveVisualGestation()
    {
        if(_workspace==null || CurrentMechName==null || SelectedGestationParent==null)
        {
            GestationStatus="请先创建机械体并选择培育模板。";
            return;
        }
        try
        {
            if(GestationIngredients.Any(x=>x.Amount!=decimal.Truncate(x.Amount)) ||
               GestationCycles!=decimal.Truncate(GestationCycles))
                throw new ArgumentException("材料数量和培育周期需要整数。");
            var tech=SelectedGestationResearch?.DefName;
            var configuration=new VisualGestationEditor.Configuration(
                SelectedGestationParent.DefName,(int)GestationCycles,
                tech is "无科技限制" or null ? null:tech,
                GestationIngredients.Select(x=>new VisualGestationEditor.Ingredient(
                    x.DefName,(int)x.Amount)).ToArray());
            var label=VisualMechDesigner.CurrentString(_workspace,"ThingDef",
                CurrentMechName,"label")??CurrentMechName;
            VisualGestationEditor.Apply(_workspace,CurrentMechName,label,configuration);
            var recipeFile=_workspace.FindDefinitionFile("RecipeDef",
                VisualGestationEditor.RecipeDefName(CurrentMechName));
            if(recipeFile!=null && ProjectFiles.All(x=>!ReferenceEquals(x.File,recipeFile)))
                ProjectFiles.Add(new ProjectFileRow(recipeFile));
            RefreshAll(SelectedDefinition?.Entry.Index);
            Notify(nameof(HasGestationRecipe));
            Notify(nameof(GestationSaveLabel));
            GestationStatus="培育配方已保存到工程。可在检查步骤验证并导出。";
            Status=GestationStatus;
        }
        catch(Exception ex){GestationStatus=ex.Message;ReportError(ex);}
    }
}

public sealed record GestationParentRow(string DefName,string Title);
public sealed record GestationResearchRow(string DefName,string Label);
public sealed record GestationMaterialRow(string DefName,string Label);

public sealed class GestationIngredientRow : INotifyPropertyChanged
{
    private decimal _amount;
    public event PropertyChangedEventHandler? PropertyChanged;
    public GestationIngredientRow(string defName,string label,decimal amount)
    {
        DefName=defName;
        Label=label;
        _amount=amount;
    }
    public string DefName { get; }
    public string Label { get; }
    public decimal Amount
    {
        get=>_amount;
        set
        {
            if(_amount==value)return;
            _amount=value;
            PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Amount)));
        }
    }
}
