using System.Collections.ObjectModel;
using System.Xml.Linq;
using Avalonia.Media;
using MechWright.RimWorld;
using MechWright.Core;

namespace MechWright.App.ViewModels;

/// <summary>
/// A novice-facing inspection of the same documents used by advanced XML
/// validators. Warnings don't pretend that a mod has been loaded in the game.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<VisualReviewRow> VisualReviewChecks { get; } = new();
    private string _visualReviewSummary = "新建机械体后，可在这里查看设计完成情况。";

    public string VisualReviewSummary
    {
        get => _visualReviewSummary;
        private set { _visualReviewSummary = value; Notify(); }
    }

    private void RefreshVisualReview()
    {
        VisualReviewChecks.Clear();
        var name = CurrentMechName;
        if (_workspace == null || name == null)
        {
            VisualReviewSummary = "请先新建或导入机械体。";
            return;
        }
        var thing = _workspace.FindDefinitionFile("ThingDef", name);
        var pawn = _workspace.FindDefinitionFile("PawnKindDef", name);
        var body = _workspace.FindDefinitionFile("BodyDef", name + "_Body");
        var hasBasics = thing != null && pawn != null && body != null;
        VisualReviewChecks.Add(new VisualReviewRow("基础配置",
            hasBasics
                ? "机体、生成规则与身体已关联。"
                : "缺少机体、生成规则或身体结构文件。", hasBasics));

        var parts = body == null ? 0 :
            body.Document.BodyParts(body.Document.Definitions().First(x => x.Type == "BodyDef")).Count;
        VisualReviewChecks.Add(new VisualReviewRow("身体结构",
            parts > 1 ? $"已配置 {parts} 个身体部件。"
                : "目前只有核心部件，可以从原版身体预设快速建立结构。",
            parts > 1));
        var toolCount = thing?.Document.Xml.Descendants("tools").Elements("li").Count() ?? 0;
        var weaponTags = VisualMechDesigner.CurrentString(_workspace, "PawnKindDef",
            name, "weaponTags");
        var hasWeapon = toolCount > 0 || !string.IsNullOrWhiteSpace(weaponTags);
        VisualReviewChecks.Add(new VisualReviewRow("战斗装备", hasWeapon
            ? "已配置近战攻击或原版远程武器方案。"
            : "尚未选择武器；可以在“武器与战斗”中点选预设。", hasWeapon));
        var images = _workspace.Assets.Count(x =>
            x.RelativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
            x.RelativePath.Contains("Textures/", StringComparison.OrdinalIgnoreCase));
        var texPath = VisualMechDesigner.CurrentString(_workspace, "PawnKindDef",
            name, "lifeStages/li/bodyGraphicData/texPath");
        var hasGraphic = images >= 3 && !string.IsNullOrWhiteSpace(texPath);
        VisualReviewChecks.Add(new VisualReviewRow("方向贴图",
            hasGraphic ? $"已登记 {images} 张 PNG，机体外观已关联。"
                : "还缺少 north / south / east 等方向的贴图，可以在外观页面导入。",
            hasGraphic));
        var recipePresent = VisualGestationEditor.Exists(_workspace,name);
        VisualReviewChecks.Add(new VisualReviewRow("机械培育",
            recipePresent ? "已配置机械培育配方，导出时会包含其材料与科技要求。"
                : "尚未配置机械培育（可选；普通机械体可以不培育）。",
            recipePresent,Optional:true));
        var validation = _workspace.Validate(
            HasFieldEvidenceReport ? _catalog : null,
            HasFieldEvidenceReport ? _fieldEvidenceScan?.ParentTemplates : null);
        var errors = validation.Count(x => x.Issue.Severity == Severity.Error);
        var warnings = validation.Count(x => x.Issue.Severity == Severity.Warning);
        VisualReviewChecks.Add(new VisualReviewRow("XML 结构检查",
            errors > 0 ? $"发现 {errors} 处阻断错误，请先处理。"
                : warnings > 0 ? $"没有阻断错误；另有 {warnings} 条引用、模板或 XML 警告，请在下方核查。"
                : "本地 XML 检查没有发现阻断错误或警告。", errors == 0));

        var done = VisualReviewChecks.Count(x => x.Ready && !x.Optional);
        var required = VisualReviewChecks.Count(x => !x.Optional);
        VisualReviewSummary = $"{done} / {required} 项必要配置已完成 · " +
            (errors > 0 ? "存在需要修正的 XML 错误"
                : warnings > 0 ? $"有 {warnings} 条非阻断警告，导出前建议检查"
                : "可以导出并在 RimWorld 中测试");
    }
}
public sealed record VisualReviewRow(string Title, string Description,
    bool Ready, bool Optional = false)
{
    public string Symbol => Optional && !Ready ? "○" : Ready ? "✓" : "!";
    public IBrush Accent => Optional && !Ready
        ? Brushes.SlateGray : Ready ? Brushes.LightSeaGreen : Brushes.Goldenrod;
    public string State => Optional && !Ready ? "可选" : Ready ? "完成" : "需要注意";
}
