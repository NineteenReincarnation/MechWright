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
        var errors = _workspace.Validate().Count(x => x.Issue.Severity == Severity.Error);
        VisualReviewChecks.Add(new VisualReviewRow("XML 结构检查",
            errors == 0 ? "本地 XML 检查没有发现阻断错误。"
                : $"发现 {errors} 处 XML 结构错误，请先处理。", errors == 0));

        var done = VisualReviewChecks.Count(x => x.Ready);
        VisualReviewSummary = $"{done} / {VisualReviewChecks.Count} 项已完成 · " +
            (errors == 0 ? "可以导出并在 RimWorld 中测试" : "存在需要处理的 XML 错误");
    }
}
public sealed record VisualReviewRow(string Title, string Description, bool Ready)
{
    public string Symbol => Ready ? "✓" : "!";
    public IBrush Accent => Ready ? Brushes.LightSeaGreen : Brushes.Goldenrod;
    public string State => Ready ? "完成" : "需要注意";
}
