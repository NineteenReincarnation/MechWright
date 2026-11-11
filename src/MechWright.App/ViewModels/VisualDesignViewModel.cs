using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using MechWright.Infrastructure;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// The normal user flow is 100% ordinary controls and curated presets.
/// XML strings are produced internally and appear only in Advanced mode.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<VisualChassisRow> ChassisCards { get; } =
        new(VisualMechDesigner.Chassis.Select(x => new VisualChassisRow(x)));
    public ObservableCollection<VisualMeleeRow> MeleeCards { get; } =
        new(VisualMechDesigner.Melee.Select(x => new VisualMeleeRow(x)));
    public ObservableCollection<VisualBodyTemplateRow> BodyTemplateCards { get; } = new();
    public ObservableCollection<VisualToolRow> InstalledMelee { get; } = new();
    public ObservableCollection<VisualRangedRow> RangedCards { get; } = new();
    public ObservableCollection<VisualPartPresetRow> PartPresetCards { get; } = new();
    private VisualPartPresetRow? _selectedPartPreset;
    private decimal _visualPartCoverage = .10m;
    public VisualPartPresetRow? SelectedPartPreset
    {
        get => _selectedPartPreset;
        set { _selectedPartPreset = value; Notify(); }
    }
    public bool HasPartPresets => PartPresetCards.Count != 0;
    public decimal VisualPartCoverage
    {
        get => _visualPartCoverage;
        set { _visualPartCoverage = value; Notify(); }
    }

    private void RefreshVisualPartPresets()
    {
        PartPresetCards.Clear();
        foreach (var def in _catalog.Where(x => x.Identity.Type == "BodyPartDef" &&
                     (x.Identity.DefName.Contains("Mech", StringComparison.OrdinalIgnoreCase) ||
                      x.Identity.DefName.Contains("Sensor", StringComparison.OrdinalIgnoreCase) ||
                      x.Identity.DefName.Contains("Artificial", StringComparison.OrdinalIgnoreCase)))
                     .DistinctBy(x => x.Identity.DefName).Take(150))
            PartPresetCards.Add(new VisualPartPresetRow(
                def.Identity.DefName, def.Label, def.SourcePackage));
        SelectedPartPreset = PartPresetCards.FirstOrDefault();
        Notify(nameof(HasPartPresets));
    }

    public void AddVisualBodyPart()
    {
        if (_document == null || SelectedBodyPart == null || SelectedPartPreset == null)
        {
            VisualEditorInfo = "先在机体结构图中选择父部件，并选择要安装的子部件。";
            return;
        }
        if (SelectedDefinition?.Entry.Type != "BodyDef") return;
        try
        {
            var index = SelectedDefinition.Entry.Index;
            var path = BodyStructureEditor.AddChild(_document, SelectedBodyPart.Part,
                SelectedPartPreset.DefName, VisualPartCoverage.ToString(CultureInfo.InvariantCulture));
            RefreshAll(index, bodyPath: path);
            VisualEditorInfo = "已安装“" + SelectedPartPreset.Display + "”，撤销可恢复。";
            Status = VisualEditorInfo;
        }
        catch (Exception e) { VisualEditorInfo = e.Message; ReportError(e); }
    }

    public void ApplyVisualPartCoverage()
    {
        PartCoverage = VisualPartCoverage.ToString(CultureInfo.InvariantCulture);
        ApplyBodyPart();
    }

    private VisualRangedRow? _selectedRangedCard;
    public VisualRangedRow? SelectedRangedCard
    {
        get => _selectedRangedCard;
        set { _selectedRangedCard = value; Notify(); }
    }
    public bool HasRangedCards => RangedCards.Count != 0;
    public ObservableCollection<VisualTextureRow> VisualTextures { get; } = new();
    private VisualChassisRow? _selectedChassisCard;
    private VisualMeleeRow? _selectedMeleeCard;
    private VisualBodyTemplateRow? _selectedBodyTemplate;
    private VisualToolRow? _selectedInstalledMelee;
    private string _visualEditorInfo = "选择一个预设，可继续调整参数后应用。";
    private decimal _visualCombatPower = 200;
    private decimal _visualBodySize = 1;
    private decimal _visualHealthScale = 1;
    private decimal _visualMoveSpeed = 4.7m;
    private decimal _visualMass = 85;
    private decimal _visualSharp = .4m;
    private decimal _visualBlunt = .2m;
    private decimal _visualHeat = 2;
    private decimal _visualBandwidth = 2;
    private decimal _visualMeleeDamage = 8;
    private decimal _visualMeleeCooldown = 2.5m;
    private string _visualWeightClass = "Medium";
    private decimal _visualDrawSize = 1.8m;

    public IReadOnlyList<string> VisualWeightClasses { get; } =
        ["Light", "Medium", "Heavy", "UltraHeavy"];
    public string VisualEditorInfo
    {
        get => _visualEditorInfo;
        private set { _visualEditorInfo = value; Notify(); }
    }
    public string VisualMechTitle => CurrentMechName ?? "尚未选择机械体";
    public bool HasVisualMech => CurrentMechName != null;
    public bool HasBodyTemplates => BodyTemplateCards.Count != 0;
    public bool HasVisualTextures => VisualTextures.Count != 0;
    public bool HasInstalledMelee => InstalledMelee.Count != 0;

    private string? CurrentMechName
    {
        get
        {
            if (_workspace == null) return null;
            if (!string.IsNullOrEmpty(_workflowMechName) &&
                _workspace.FindDefinitionFile("ThingDef", _workflowMechName) != null)
                return _workflowMechName;
            return _workspace.Files.SelectMany(f => f.Document.Definitions())
                .FirstOrDefault(x => x.Type == "ThingDef" &&
                    x.DefName.StartsWith("MP_", StringComparison.Ordinal))?.DefName;
        }
    }

    public VisualChassisRow? SelectedChassisCard
    {
        get => _selectedChassisCard;
        set
        {
            if (ReferenceEquals(_selectedChassisCard, value)) return;
            _selectedChassisCard = value;
            Notify();
            if (value == null) return;
            var v = VisualMechDesigner.BasicSettings.From(value.Preset);
            VisualCombatPower = (decimal)v.CombatPower;
            VisualBodySize = (decimal)v.BodySize;
            VisualHealthScale = (decimal)v.HealthScale;
            VisualMoveSpeed = (decimal)v.MoveSpeed;
            VisualMass = (decimal)v.Mass;
            VisualSharp = (decimal)v.SharpArmor;
            VisualBlunt = (decimal)v.BluntArmor;
            VisualHeat = (decimal)v.HeatArmor;
            VisualBandwidth = v.BandwidthCost;
            VisualWeightClass = v.WeightClass;
            VisualEditorInfo = "已载入“" + value.Title + "”参数，可自由微调。点击“应用配置”才会写入工程。";
        }
    }

    public VisualMeleeRow? SelectedMeleeCard
    {
        get => _selectedMeleeCard;
        set
        {
            if (ReferenceEquals(_selectedMeleeCard, value)) return;
            _selectedMeleeCard = value;
            Notify();
            if (value != null)
            {
                VisualMeleeDamage = (decimal)value.Preset.Power;
                VisualMeleeCooldown = (decimal)value.Preset.Cooldown;
            }
        }
    }

    public VisualBodyTemplateRow? SelectedBodyTemplate
    {
        get => _selectedBodyTemplate;
        set { _selectedBodyTemplate = value; Notify(); }
    }
    public VisualToolRow? SelectedInstalledMelee
    {
        get => _selectedInstalledMelee;
        set { _selectedInstalledMelee = value; Notify(); Notify(nameof(CanRemoveVisualMelee)); }
    }
    public bool CanRemoveVisualMelee => SelectedInstalledMelee != null;

    public decimal VisualCombatPower { get => _visualCombatPower; set { _visualCombatPower = value; Notify(); } }
    public decimal VisualBodySize { get => _visualBodySize; set { _visualBodySize = value; Notify(); } }
    public decimal VisualHealthScale { get => _visualHealthScale; set { _visualHealthScale = value; Notify(); } }
    public decimal VisualMoveSpeed { get => _visualMoveSpeed; set { _visualMoveSpeed = value; Notify(); } }
    public decimal VisualMass { get => _visualMass; set { _visualMass = value; Notify(); } }
    public decimal VisualSharp { get => _visualSharp; set { _visualSharp = value; Notify(); } }
    public decimal VisualBlunt { get => _visualBlunt; set { _visualBlunt = value; Notify(); } }
    public decimal VisualHeat { get => _visualHeat; set { _visualHeat = value; Notify(); } }
    public decimal VisualBandwidth { get => _visualBandwidth; set { _visualBandwidth = value; Notify(); } }
    public decimal VisualMeleeDamage { get => _visualMeleeDamage; set { _visualMeleeDamage = value; Notify(); } }
    public decimal VisualMeleeCooldown { get => _visualMeleeCooldown; set { _visualMeleeCooldown = value; Notify(); } }
    public decimal VisualDrawSize { get => _visualDrawSize; set { _visualDrawSize = value; Notify(); } }
    public string VisualWeightClass { get => _visualWeightClass; set { _visualWeightClass = value; Notify(); } }

    private void RefreshVisualDesigner()
    {
        Notify(nameof(HasVisualMech));
        Notify(nameof(VisualMechTitle));
        var name = CurrentMechName;
        if (name == null) return;
        decimal Read(string type, string path, decimal fallback)
        {
            var text = VisualMechDesigner.CurrentString(_workspace!, type, name, path);
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value : fallback;
        }
        VisualCombatPower = Read("PawnKindDef", "combatPower", 200);
        VisualBodySize = Read("ThingDef", "race/baseBodySize", 1);
        VisualHealthScale = Read("ThingDef", "race/baseHealthScale", 1);
        VisualMoveSpeed = Read("ThingDef", "statBases/MoveSpeed", 4.7m);
        VisualMass = Read("ThingDef", "statBases/Mass", 85);
        VisualSharp = Read("ThingDef", "statBases/ArmorRating_Sharp", .4m);
        VisualBlunt = Read("ThingDef", "statBases/ArmorRating_Blunt", .2m);
        VisualHeat = Read("ThingDef", "statBases/ArmorRating_Heat", 2);
        VisualBandwidth = Read("ThingDef", "statBases/BandwidthCost", 2);
        VisualWeightClass = VisualMechDesigner.CurrentString(_workspace!, "ThingDef",
            name, "race/mechWeightClass") ?? "Medium";
        RefreshVisualMelee();
        RefreshVisualTextures();
    }

    public void ApplyVisualBasic()
    {
        var name = CurrentMechName;
        if (_workspace == null || name == null)
        {
            VisualEditorInfo = "请先新建一台机械体。"; return;
        }
        try
        {
            VisualMechDesigner.ApplyBasic(_workspace, name,
                new VisualMechDesigner.BasicSettings((double)VisualCombatPower,
                    (double)VisualBodySize, (double)VisualHealthScale, (double)VisualMoveSpeed,
                    (double)VisualMass, (double)VisualSharp, (double)VisualBlunt,
                    (double)VisualHeat, (int)VisualBandwidth, VisualWeightClass));
            RefreshAll(SelectedDefinition?.Entry.Index);
            VisualEditorInfo = "已保存机体属性：机体与生成定义均已更新，可撤销。";
            Status = VisualEditorInfo;
        }
        catch (Exception e) { VisualEditorInfo = e.Message; ReportError(e); }
    }

    private void RefreshBodyPresetOptions()
    {
        BodyTemplateCards.Clear();
        var dedup = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in _vanillaMechs.Entries)
        {
            if (string.IsNullOrWhiteSpace(profile.BodyDefName) ||
                !dedup.Add(profile.BodyDefName)) continue;
            var source = _catalog.FirstOrDefault(x =>
                x.Identity.Type == "BodyDef" && x.Identity.DefName == profile.BodyDefName);
            if (source == null) continue;
            BodyTemplateCards.Add(new VisualBodyTemplateRow(profile.Label,
                profile.BodyDefName, source.FilePath, profile.Package, profile.BodyParts.Count));
        }
        SelectedBodyTemplate = BodyTemplateCards.FirstOrDefault();
        Notify(nameof(HasBodyTemplates));
    }

    private void RefreshVisualRangedPresets()
    {
        RangedCards.Clear();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var original in _vanillaMechs.Entries)
        {
            var fact = original.Fields.FirstOrDefault(x => x.XmlPath == "weaponTags");
            if (fact?.Value is not { Length: > 0 } raw) continue;
            var tags = raw.Split(',', StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);
            if (tags.Length == 0 || tags.Length > 12) continue;
            var key = string.Join(",", tags);
            if (!keys.Add(key)) continue;
            RangedCards.Add(new VisualRangedRow(
                original.Label, original.Package, tags, original.WeaponCandidates.Count));
        }
        SelectedRangedCard = RangedCards.FirstOrDefault();
        Notify(nameof(HasRangedCards));
    }

    public void ApplySelectedRangedPreset()
    {
        var name = CurrentMechName;
        if (_workspace == null || name == null || SelectedRangedCard == null)
        {
            VisualEditorInfo = "请先选择原版远程武器方案。"; return;
        }
        try
        {
            var file = _workspace.FindDefinitionFile("PawnKindDef", name)
                ?? throw new InvalidOperationException("缺少机械体生成定义。");
            var def = file.Document.Definitions().Single(x => x.Type == "PawnKindDef" &&
                x.DefName == name);
            VisualMechDesigner.SetWeaponTags(file.Document, def, SelectedRangedCard.Tags);
            VisualEditorInfo = "已按“" + SelectedRangedCard.Label +
                "”写入原版武器生成标签。具体武器由游戏根据标签选择。";
            Status = VisualEditorInfo;
        }
        catch (Exception e) { VisualEditorInfo = e.Message; ReportError(e); }
    }

    public void ApplySelectedBodyTemplate()
    {
        if (_workspace == null || SelectedBodyTemplate == null || CurrentMechName == null)
        {
            VisualEditorInfo = "连接游戏目录并选择一种原版机体结构。"; return;
        }
        try
        {
            var file = _workspace.FindDefinitionFile("BodyDef", CurrentMechName + "_Body")
                ?? throw new InvalidOperationException("当前机体缺少 BodyDef。");
            var templateXml = DefDocument.Parse(File.ReadAllText(SelectedBodyTemplate.FilePath));
            var source = templateXml.Definitions().FirstOrDefault(x => x.Type == "BodyDef" &&
                x.DefName == SelectedBodyTemplate.BodyDef);
            if (source == null) throw new InvalidOperationException("原版 BodyDef 不在源 XML 文件中。");
            VisualMechDesigner.ApplyBodyTemplate(file.Document,
                file.Document.Definitions().Single(x => x.Type == "BodyDef"), source);
            if (WorkflowStep == 1) SelectStageDocument();
            RefreshAll(SelectedDefinition?.Entry.Index);
            VisualEditorInfo = "已按“" + SelectedBodyTemplate.Label +
                "”的原版结构装配身体，保留当前机体的名称；可撤销。";
            Status = VisualEditorInfo;
        }
        catch (Exception e) { VisualEditorInfo = e.Message; ReportError(e); }
    }

    private void RefreshVisualMelee()
    {
        InstalledMelee.Clear();
        var name = CurrentMechName;
        var file = name == null ? null : _workspace?.FindDefinitionFile("ThingDef", name);
        if (file != null)
        {
            var thing = file.Document.Definitions().FirstOrDefault(x => x.Type == "ThingDef" &&
                x.DefName == name);
            if (thing != null)
                foreach (var item in NativeListEditor.Items(file.Document, thing, "tools"))
                {
                    var xml = XElement.Parse(item.Source);
                    InstalledMelee.Add(new VisualToolRow(item.XmlPath,
                        xml.Element("label")?.Value ?? "机械近战攻击",
                        xml.Element("power")?.Value ?? "—",
                        xml.Element("cooldownTime")?.Value ?? "—"));
                }
        }
        SelectedInstalledMelee = InstalledMelee.FirstOrDefault();
        Notify(nameof(HasInstalledMelee));
    }

    public void AddVisualMelee()
    {
        var name = CurrentMechName;
        if (_workspace == null || name == null || SelectedMeleeCard == null) return;
        try
        {
            var file = _workspace.FindDefinitionFile("ThingDef", name)!;
            var def = file.Document.Definitions().Single(x => x.Type == "ThingDef" && x.DefName == name);
            VisualMechDesigner.AddMelee(file.Document, def,
                SelectedMeleeCard.Preset, (double)VisualMeleeDamage, (double)VisualMeleeCooldown);
            RefreshAll(SelectedDefinition?.Entry.Index);
            RefreshVisualMelee();
            VisualEditorInfo = "已添加“" + SelectedMeleeCard.Title + "”近战攻击，原生 XML 自动生成。";
            Status = VisualEditorInfo;
        }
        catch (Exception e) { VisualEditorInfo = e.Message; ReportError(e); }
    }

    public void RemoveVisualMelee()
    {
        var name = CurrentMechName;
        if (_workspace == null || name == null || SelectedInstalledMelee == null) return;
        try
        {
            var file = _workspace.FindDefinitionFile("ThingDef", name)!;
            var def = file.Document.Definitions().Single(x => x.Type == "ThingDef" && x.DefName == name);
            NativeListEditor.Remove(file.Document, def, "tools", SelectedInstalledMelee.ItemPath);
            RefreshAll(SelectedDefinition?.Entry.Index);
            RefreshVisualMelee();
            VisualEditorInfo = "已移除近战攻击，可撤销。";
        }
        catch (Exception e) { VisualEditorInfo = e.Message; ReportError(e); }
    }

    public void RegisterMechTextures(IReadOnlyList<string> files)
    {
        if (_workspace == null || CurrentMechName == null || files.Count == 0) return;
        try
        {
            var name = CurrentMechName;
            string? texPath = null;
            foreach (var file in files)
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                var suffix = new[] { "_south", "_east", "_north", "_west" }
                    .FirstOrDefault(x => stem.EndsWith(x, StringComparison.OrdinalIgnoreCase));
                if (suffix == null)
                    throw new ArgumentException("每张图片的名称需要以 _south、_east、_north 或 _west 结尾。");
                var baseName = stem[..^suffix.Length];
                if (texPath != null && !texPath.Equals("Things/Mechanoids/" + baseName,
                    StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("一次导入的所有方向贴图需要拥有相同的名称前缀。");
                texPath = "Things/Mechanoids/" + baseName;
            }
            foreach (var file in files)
            {
                var path = "Textures/Things/Mechanoids/" + Path.GetFileName(file);
                if (_workspace.Assets.Any(x => x.RelativePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    continue;
                WorkspaceAssetService.Register(_workspace, file, path);
            }
            var kind = _workspace.FindDefinitionFile("PawnKindDef", name)
                ?? throw new InvalidOperationException("机械体缺少 PawnKindDef。");
            var def = kind.Document.Definitions().Single(x =>
                x.Type == "PawnKindDef" && x.DefName == name);
            VisualMechDesigner.SetPawnGraphic(kind.Document, def,
                texPath!, (double)VisualDrawSize);
            RefreshProjectAssets();
            RefreshVisualTextures();
            VisualEditorInfo = "方向贴图已加入工程，并建立了图形路径。导出时记得勾选包含项目资源。";
            Status = VisualEditorInfo;
        }
        catch (Exception e) { VisualEditorInfo = e.Message; ReportError(e); }
    }

    private void RefreshVisualTextures()
    {
        foreach (var image in VisualTextures) image.Dispose();
        VisualTextures.Clear();
        if (_workspace == null) return;
        var name = CurrentMechName;
        var graphic = name == null ? null : VisualMechDesigner.CurrentString(_workspace,
            "PawnKindDef", name, "lifeStages/li/bodyGraphicData/texPath");
        foreach (var resource in _workspace.Assets)
        {
            if (!resource.RelativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            var relative = resource.RelativePath.Replace('\\', '/');
            var relativeBase = relative.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase)
                ? relative["Textures/".Length..] : relative;
            if (graphic != null &&
                !relativeBase.StartsWith(graphic + "_", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (!File.Exists(resource.SourcePath) ||
                    new FileInfo(resource.SourcePath).Length > 12 * 1024 * 1024) continue;
                using var stream = File.OpenRead(resource.SourcePath);
                VisualTextures.Add(new VisualTextureRow(Path.GetFileNameWithoutExtension(
                    resource.RelativePath), Bitmap.DecodeToWidth(stream, 220)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException
                or ArgumentException) { /* A bad optional PNG should not break the editor. */ }
        }
        Notify(nameof(HasVisualTextures));
    }
}

public sealed record VisualChassisRow(VisualMechDesigner.ChassisPreset Preset)
{
    public string Title => Preset.Title;
    public string Detail => Preset.Description;
    public string Icon => Preset.Symbol;
    public string Score => $"CP {Preset.CombatPower:0} · {Preset.WeightClass}";
}
public sealed record VisualMeleeRow(VisualMechDesigner.MeleePreset Preset)
{
    public string Title => Preset.Title;
    public string Detail => Preset.Description;
    public string Icon => Preset.Symbol;
    public string Score => $"伤害 {Preset.Power:0} · 冷却 {Preset.Cooldown:0.0}s";
}
public sealed record VisualBodyTemplateRow(
    string Label, string BodyDef, string FilePath, string Package, int Parts)
{
    public string Display => $"{Label} · {Parts} 部件";
    public string Detail => Package + " · " + BodyDef;
}
public sealed record VisualPartPresetRow(string DefName, string Label, string Source)
{
    public string Display => Label == DefName ? DefName : Label + " · " + DefName;
    public string Detail => Source;
}
public sealed record VisualRangedRow(
    string Label, string Package, IReadOnlyList<string> Tags, int MatchingWeapons)
{
    public string Display => Label + " · " + Package;
    public string Detail => MatchingWeapons + " 个已识别的候选武器";
}

public sealed record VisualToolRow(string ItemPath, string Label, string Damage, string Cooldown)
{
    public string Display => $"{Label} · {Damage} 伤害 · {Cooldown}s";
}
public sealed record VisualTextureRow(string Name, Bitmap Preview) : IDisposable
{
    public string Title => Name.Split('_').Last().ToUpperInvariant();
    public void Dispose() => Preview.Dispose();
}
