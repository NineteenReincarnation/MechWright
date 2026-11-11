using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>Read-only original mechanoid encyclopedia; never writes into installed game content.</summary>
public sealed partial class MainWindowViewModel
{
    private VanillaMechanoidLibrary _vanillaMechs = VanillaMechanoidLibrary.Empty;
    private VanillaMechSummaryRow? _selectedVanillaMech;
    private string _vanillaSearch = "";
    private string _vanillaPackage = "全部";
    private string _vanillaWeight = "全部";
    private string _vanillaStatus = "尚未扫描游戏 · 从顶部“原版 / DLC 目录”选择 RimWorld 根目录或 Data 文件夹";
    private string _vanillaDescription = "";
    private string _vanillaOriginalXml = "";
    private string _vanillaSource = "";
    private string _vanillaParentTemplate = "";
    private string _vanillaBodyName = "";
    private string _vanillaNotes = "";

    public ObservableCollection<VanillaMechSummaryRow> VisibleVanillaMechs { get; } = new();
    public ObservableCollection<VanillaFactRow> VanillaFacts { get; } = new();
    public ObservableCollection<VanillaBodyPartRow> VanillaBodyParts { get; } = new();
    public ObservableCollection<VanillaRelatedRow> VanillaKinds { get; } = new();
    public ObservableCollection<VanillaRelatedRow> VanillaWeapons { get; } = new();
    public ObservableCollection<VanillaRelatedRow> VanillaRecipes { get; } = new();
    public ObservableCollection<VanillaPackageRow> VanillaPackages { get; } = new();

    public IReadOnlyList<string> VanillaPackageFilters { get; } =
        ["全部", "Core", "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey"];
    public IReadOnlyList<string> VanillaWeightFilters { get; } =
        ["全部", "Light", "Medium", "Heavy", "UltraHeavy", "未在 XML 中定义"];

    public string VanillaSearch
    {
        get => _vanillaSearch;
        set { _vanillaSearch = value; Notify(); FilterVanillaMechs(); }
    }

    public string VanillaPackage
    {
        get => _vanillaPackage;
        set { _vanillaPackage = value; Notify(); FilterVanillaMechs(); }
    }

    public string VanillaWeight
    {
        get => _vanillaWeight;
        set { _vanillaWeight = value; Notify(); FilterVanillaMechs(); }
    }

    public string VanillaStatus
    {
        get => _vanillaStatus;
        private set { _vanillaStatus = value; Notify(); }
    }

    public string VanillaDescription
    {
        get => _vanillaDescription;
        private set { _vanillaDescription = value; Notify(); }
    }

    public string VanillaOriginalXml
    {
        get => _vanillaOriginalXml;
        private set { _vanillaOriginalXml = value; Notify(); }
    }

    public string VanillaSource
    {
        get => _vanillaSource;
        private set { _vanillaSource = value; Notify(); }
    }

    public string VanillaParentTemplate
    {
        get => _vanillaParentTemplate;
        private set { _vanillaParentTemplate = value; Notify(); }
    }

    public string VanillaBodyName
    {
        get => _vanillaBodyName;
        private set { _vanillaBodyName = value; Notify(); }
    }

    public string VanillaNotes
    {
        get => _vanillaNotes;
        private set { _vanillaNotes = value; Notify(); }
    }

    public string VanillaCount =>
        $"{VisibleVanillaMechs.Count} / {_vanillaMechs.Count} 机械体";

    public string VanillaTitle => SelectedVanillaMech?.Profile.Summary ?? "选择一台原版机械体";
    public string VanillaGrade => SelectedVanillaMech?.Profile.WeightClass ?? "—";

    public VanillaMechSummaryRow? SelectedVanillaMech
    {
        get => _selectedVanillaMech;
        set
        {
            if (ReferenceEquals(_selectedVanillaMech, value)) return;
            _selectedVanillaMech = value;
            Notify();
            Notify(nameof(VanillaTitle));
            Notify(nameof(VanillaGrade));
            RefreshVanillaDetails();
        }
    }

    private void RefreshVanillaCatalog(DefCatalog.CatalogScan scan)
    {
        _vanillaMechs = scan.Mechanoids;
        VanillaPackages.Clear();
        foreach (var item in scan.Packages)
            VanillaPackages.Add(new VanillaPackageRow(item.Name, item.Present, item.DefinitionCount));

        if (!scan.IsGameInstallation)
            VanillaStatus = "当前选择的不是完整 RimWorld Data 目录；已载入通用 Def，但原版机械体图鉴需要 Core/Defs。";
        else
        {
            var present = scan.Packages.Where(x => x.Present).Select(x => x.Name);
            VanillaStatus = $"已扫描：{string.Join(" / ", present)} · {_vanillaMechs.Count} 台机械体种族 · 本地 XML 只读";
            if (scan.Warnings.Count > 0)
                VanillaStatus += $" · {scan.Warnings.Count} 处文件读取提示";
        }
        FilterVanillaMechs();
    }

    private void FilterVanillaMechs()
    {
        var previouslySelected = SelectedVanillaMech?.Profile.DefName;
        VisibleVanillaMechs.Clear();
        var query = VanillaSearch.Trim();
        foreach (var mech in _vanillaMechs.Entries)
        {
            if (VanillaPackage != "全部" && mech.Package != VanillaPackage) continue;
            if (VanillaWeight != "全部" && mech.WeightClass != VanillaWeight) continue;
            if (query.Length != 0 && !mech.Label.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !mech.DefName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !mech.Description.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !mech.Kinds.Any(x => x.DefName.Contains(query, StringComparison.OrdinalIgnoreCase)))
                continue;
            VisibleVanillaMechs.Add(new VanillaMechSummaryRow(mech));
        }
        Notify(nameof(VanillaCount));
        SelectedVanillaMech = VisibleVanillaMechs.FirstOrDefault(x =>
            x.Profile.DefName == previouslySelected) ?? VisibleVanillaMechs.FirstOrDefault();
    }

    private void RefreshVanillaDetails()
    {
        VanillaFacts.Clear();
        VanillaBodyParts.Clear();
        VanillaKinds.Clear();
        VanillaWeapons.Clear();
        VanillaRecipes.Clear();
        var profile = SelectedVanillaMech?.Profile;
        VanillaDescription = profile?.Description ?? "";
        VanillaOriginalXml = profile?.OriginalXml ?? "";
        VanillaSource = profile?.Origin ?? "";
        VanillaParentTemplate = profile?.ParentTemplate ?? "";
        VanillaBodyName = profile?.BodyDefName ?? "";
        VanillaNotes = profile == null ? "" : string.Join("\n", profile.Notes);
        if (profile == null) return;
        foreach (var fact in profile.Fields) VanillaFacts.Add(new VanillaFactRow(fact));
        foreach (var part in profile.BodyParts) VanillaBodyParts.Add(new VanillaBodyPartRow(part));
        foreach (var kind in profile.Kinds) VanillaKinds.Add(new VanillaRelatedRow(kind));
        foreach (var weapon in profile.WeaponCandidates)
            VanillaWeapons.Add(new VanillaRelatedRow(weapon));
        foreach (var recipe in profile.Recipes)
            VanillaRecipes.Add(new VanillaRelatedRow(recipe));
    }
}

public sealed record VanillaMechSummaryRow(VanillaMechanoidLibrary.Profile Profile)
{
    public string Display => Profile.Summary;
    public string Meta => $"{Profile.Package}   ·   {Profile.WeightClass}";
}

public sealed record VanillaFactRow(VanillaMechanoidLibrary.Fact Fact)
{
    public string Name => Fact.Label;
    public string Value => Fact.Display;
    public string Origin => Fact.Provenance;
    public string XmlPath => Fact.XmlPath;
}

public sealed record VanillaBodyPartRow(VanillaMechanoidLibrary.BodyPart Part)
{
    public string Name => new string(' ', Part.Level * 3) +
        (Part.Level == 0 ? "◆ " : "└ ") + Part.Name;
    public string Details => $"coverage {Part.Coverage} · {Part.Height} / {Part.Depth}";
}

public sealed record VanillaRelatedRow(VanillaMechanoidLibrary.RelatedDef Def)
{
    public string Display => Def.Label + "  /  " + Def.DefName;
    public string Info => Def.Package + " · " + Def.Description;
    public string Source => Def.SourceFile;
}

public sealed record VanillaPackageRow(string Name, bool Present, int Count)
{
    public string Display => Name + (Present ? $"  {Count} Defs" : "  未找到");
}
