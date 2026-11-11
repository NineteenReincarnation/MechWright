using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using MechWright.RimWorld;
using MechWright.Infrastructure;

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
    public ObservableCollection<VanillaGraphicRow> VanillaGraphics { get; } = new();
    public ObservableCollection<VanillaTextureRow> VanillaTextures { get; } = new();
    private VanillaGraphicRow? _selectedVanillaGraphic;
    private VanillaTextureRow? _selectedVanillaTexture;
    private Bitmap? _vanillaTextureImage;
    private string _vanillaGraphicStatus = "未选择原版图形定义";
    private long _vanillaImageRevision;

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

    public VanillaGraphicRow? SelectedVanillaGraphic
    {
        get => _selectedVanillaGraphic;
        set
        {
            if (ReferenceEquals(_selectedVanillaGraphic, value)) return;
            _selectedVanillaGraphic = value;
            Notify();
            RefreshOriginalTextures();
        }
    }

    public VanillaTextureRow? SelectedVanillaTexture
    {
        get => _selectedVanillaTexture;
        set
        {
            _selectedVanillaTexture = value;
            Notify();
            _ = LoadOriginalTextureAsync(value);
        }
    }

    public Bitmap? VanillaTextureImage
    {
        get => _vanillaTextureImage;
        private set
        {
            var previous = _vanillaTextureImage;
            _vanillaTextureImage = value;
            Notify();
            if (previous != null && !ReferenceEquals(previous, value)) previous.Dispose();
        }
    }

    public string VanillaGraphicStatus
    {
        get => _vanillaGraphicStatus;
        private set { _vanillaGraphicStatus = value; Notify(); }
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
        VanillaGraphics.Clear();
        VanillaTextures.Clear();
        _selectedVanillaGraphic = null;
        _selectedVanillaTexture = null;
        Notify(nameof(SelectedVanillaGraphic));
        Notify(nameof(SelectedVanillaTexture));
        Interlocked.Increment(ref _vanillaImageRevision);
        VanillaTextureImage = null;
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
        foreach (var graphic in profile.Graphics)
            VanillaGraphics.Add(new VanillaGraphicRow(graphic));
        SelectedVanillaGraphic = VanillaGraphics.FirstOrDefault();
    }

    private void RefreshOriginalTextures()
    {
        Interlocked.Increment(ref _vanillaImageRevision);
        VanillaTextureImage = null;
        VanillaTextures.Clear();
        var graphic = SelectedVanillaGraphic?.Graphic;
        if (graphic == null)
        {
            VanillaGraphicStatus = "该机械体尚未在已扫描 XML 中找到贴图路径";
            SelectedVanillaTexture = null;
            return;
        }
        // A vanilla Package's Defs and Textures belong to the same game Data package.
        // Other locations and packed Unity assets remain intentionally unresolved.
        var current = new FileInfo(graphic.SourceFile).Directory;
        while (current != null && !current.Name.Equals("Defs",
                   StringComparison.OrdinalIgnoreCase))
            current = current.Parent;
        var packageRoot = current?.Parent?.FullName;
        var desc = new GraphicReferences.GraphicDescriptor(
            "read-only XML", graphic.TexturePath, graphic.GraphicClass,
            graphic.DrawSize, graphic.GraphicClass != "未显式指定");
        var candidates = TextureAssetLocator.Resolve(packageRoot, desc);
        foreach (var candidate in candidates)
            VanillaTextures.Add(new VanillaTextureRow(candidate));
        VanillaGraphicStatus = $"{graphic.Package} · {graphic.TexturePath} · " +
            $"{candidates.Count(x => x.Exists)} / {candidates.Count} 本地 PNG" +
            (candidates.Any(x => x.Exists) ? " · 只读预览" :
                " · 原版资源可能位于 Unity 资源包，不能直接预览");
        SelectedVanillaTexture = VanillaTextures.FirstOrDefault(x => x.Candidate.Exists)
            ?? VanillaTextures.FirstOrDefault();
    }

    private async Task LoadOriginalTextureAsync(VanillaTextureRow? row)
    {
        var revision = Interlocked.Increment(ref _vanillaImageRevision);
        VanillaTextureImage = null;
        if (row?.Candidate.LocalPath is not { } local) return;
        try
        {
            if (new FileInfo(local).Length > 12 * 1024 * 1024)
            {
                VanillaGraphicStatus = "原版 PNG 超出安全预览大小限制";
                return;
            }
            var image = await Task.Run(() =>
            {
                using var stream = File.OpenRead(local);
                return Bitmap.DecodeToWidth(stream, 480);
            });
            if (revision != Interlocked.Read(ref _vanillaImageRevision))
            {
                image.Dispose();
                return;
            }
            VanillaTextureImage = image;
        }
        catch (Exception ex)
        {
            if (revision == Interlocked.Read(ref _vanillaImageRevision))
                VanillaGraphicStatus = "原版 PNG 预览失败：" + ex.Message;
        }
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

public sealed record VanillaGraphicRow(VanillaMechanoidLibrary.Graphic Graphic)
{
    public string Display => Graphic.Label + " · " + Graphic.TexturePath;
    public string Info => Graphic.GraphicClass + " · drawSize " + Graphic.DrawSize;
}
public sealed record VanillaTextureRow(TextureAssetLocator.Candidate Candidate)
{
    public string Display => Candidate.Display;
}
