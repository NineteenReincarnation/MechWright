using System.Collections.ObjectModel;
using MechWright.RimWorld;
using MechWright.Infrastructure;

namespace MechWright.App.ViewModels;

/// <summary>
/// Game validation is explicitly separate from static XML diagnostics and CI.
/// Only derived log counters and hashes are retained; Player.log text is
/// discarded immediately after scanning.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<GameLogIssueRow> GameLogIssues { get; } = new();
    public ObservableCollection<GameExportDifferenceRow> GameExportDifferences { get; } = new();
    private GameLogIssueRow? _selectedGameLogIssue;
    private GameExportAudit.Result? _gameExportAudit;

    public GameLogIssueRow? SelectedGameLogIssue
    {
        get => _selectedGameLogIssue;
        set
        {
            if (ReferenceEquals(_selectedGameLogIssue, value)) return;
            _selectedGameLogIssue = value;
            Notify();
            Notify(nameof(CanNavigateGameLogIssue));
        }
    }

    public bool CanNavigateGameLogIssue => SelectedGameLogIssue?.CanNavigate == true;

    public string GameExportAuditSummary
    {
        get
        {
            if (_gameExportAudit == null)
                return "尚未核对导出的 Mod 文件夹；此核对不包含素材、About.xml 或游戏加载结果。";
            var now = GameLoadEvidence.ProjectFingerprint(_workspace, _document);
            return _gameExportAudit.Summary +
                (now == _gameExportAudit.SourceXmlSha256 ?
                    " 当前工程 XML 与核对时一致。" :
                    " 注意：核对后工程 XML 已修改，请重新核对导出文件。");
        }
    }
    private GameLoadEvidence.Result? _gameLogEvidence;
    private string _gameVersion = "";
    private string _gameEnabledDlc = "";
    private string _gameTester = "";
    private string _gameNotes = "";
    private bool _gameLoaded;
    private bool _gameSpawned;
    private bool _gameGraphicsChecked;
    private bool _gameCombatChecked;
    private bool _gameGestationChecked;
    private bool _gameReimportChecked;

    public string GameVersion { get => _gameVersion;
        set { _gameVersion = value ?? ""; Notify(); } }
    public string GameEnabledDlc { get => _gameEnabledDlc;
        set { _gameEnabledDlc = value ?? ""; Notify(); } }
    public string GameTester { get => _gameTester;
        set { _gameTester = value ?? ""; Notify(); } }
    public string GameNotes { get => _gameNotes;
        set { _gameNotes = value ?? ""; Notify(); } }
    public bool GameLoaded { get => _gameLoaded;
        set { _gameLoaded = value; Notify(); } }
    public bool GameSpawned { get => _gameSpawned;
        set { _gameSpawned = value; Notify(); } }
    public bool GameGraphicsChecked { get => _gameGraphicsChecked;
        set { _gameGraphicsChecked = value; Notify(); } }
    public bool GameCombatChecked { get => _gameCombatChecked;
        set { _gameCombatChecked = value; Notify(); } }
    public bool GameGestationChecked { get => _gameGestationChecked;
        set { _gameGestationChecked = value; Notify(); } }
    public bool GameReimportChecked { get => _gameReimportChecked;
        set { _gameReimportChecked = value; Notify(); } }

    public string GameLogSummary
    {
        get
        {
            if (_gameLogEvidence == null)
                return "尚未导入 Player.log。所有实机验收项均由用户手动确认；CI 不是游戏运行证明。";
            var fingerprint = GameLoadEvidence.ProjectFingerprint(_workspace, _document);
            var note = _gameLogEvidence.ProjectSha256 == fingerprint
                ? "导入日志后工程未发生 XML 内容变化。"
                : "注意：导入日志后工程 XML 内容已变化，需重新对应游戏测试版本。";
            return _gameLogEvidence.Summary + " " + note;
        }
    }

    private void ClearGameLogEvidence()
    {
        _gameLogEvidence = null;
        GameLogIssues.Clear();
        SelectedGameLogIssue = null;
        _gameExportAudit = null;
        GameExportDifferences.Clear();
        Notify(nameof(GameExportAuditSummary));
        GameLoaded = false;
        GameSpawned = false;
        GameGraphicsChecked = false;
        GameCombatChecked = false;
        GameGestationChecked = false;
        GameReimportChecked = false;
        Notify(nameof(GameLogSummary));
    }

    public async Task ImportGameLogAsync(string path)
    {
        if (_document == null)
            throw new InvalidOperationException("请先打开 XML 或 Mod 工程。");
        var info = new FileInfo(path);
        if (info.Length > GameLoadEvidence.MaxLogCharacters)
            throw new InvalidDataException("日志文件超过 12 MB；请先在本地裁剪所需时间段。");
        if (info.Length == 0)
            throw new InvalidDataException("日志文件为空。");

        // Capture the original editor state before async disk access.
        var snapshot = GameLoadEvidence.ProjectFingerprint(_workspace, _document);
        var names = (_workspace == null
            ? _document.Definitions()
            : _workspace.Files.SelectMany(x => x.Document.Definitions()))
            .Select(x => x.DefName)
            .Where(x => x != "(unnamed)").Distinct().ToArray();
        var log = await File.ReadAllTextAsync(path);
        var report = await Task.Run(() =>
            GameLoadEvidence.Analyze(log, names, snapshot));
        // Resolve all source definitions once, without assuming a DefName is
        // globally unique across different Def types or project files.
        var entries = _workspace == null
            ? _document.Definitions().Select(x =>
                (Name: x.DefName, Type: x.Type, Path: "")).ToArray()
            : _workspace.Files.SelectMany(file => file.Document.Definitions()
                .Select(x => (Name: x.DefName, Type: x.Type,
                    Path: file.RelativePath))).ToArray();
        var locations = entries.Where(x => x.Name != "(unnamed)")
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        _gameLogEvidence = report;
        GameLogIssues.Clear();
        foreach (var issue in report.Issues)
        foreach (var name in issue.Definitions)
        {
            locations.TryGetValue(name, out var matched);
            var unique = matched is { Length: 1 } ? matched[0] :
                (Name: "", Type: "", Path: "");
            GameLogIssues.Add(new GameLogIssueRow(issue, name,
                unique.Type, unique.Path, matched is { Length: > 1 }));
        }
        SelectedGameLogIssue = GameLogIssues.FirstOrDefault();
        Notify(nameof(GameLogSummary));
        Status = "已扫描本地日志；没有保存原始日志。请在验收区核对游戏版本和实际操作。";
    }

    public void JumpToGameLogIssue()
    {
        var target = SelectedGameLogIssue;
        if (target == null || !target.CanNavigate)
        {
            Status = "该日志行包含重名或已删除的 Def，无法唯一定位；请核对原始 Player.log。";
            return;
        }
        if (HasUncommittedDocumentInputs && !CommitDocumentInputs()) return;
        UseDocumentMode();
        if (target.RelativePath.Length > 0)
        {
            var file = ProjectFiles.FirstOrDefault(x =>
                string.Equals(x.File.RelativePath, target.RelativePath,
                    StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                Status = "原工程文件已不存在，请重新导入日志或检查工程。";
                return;
            }
            SelectedProjectFile = file;
            if (!ReferenceEquals(SelectedProjectFile, file)) return;
        }
        SearchQuery = "";
        var definition = Definitions.FirstOrDefault(x =>
            x.Entry.Type == target.DefType && x.Entry.DefName == target.DefName);
        if (definition == null)
        {
            Status = "日志导入后，源定义已经更名或删除。请重新检查当前项目。";
            return;
        }
        SelectedDefinition = definition;
        Status = $"已定位日志第 {target.Evidence.LineNumber} 行关联的 " +
            $"{target.DefType}/{target.DefName}；请人工核对对应原始错误。";
    }

    public async Task AuditExportedModAsync(string folder)
    {
        if (_workspace == null)
            throw new InvalidOperationException("请先打开完整 Mod 工程才能核对导出文件夹。");
        var original = _workspace;
        // Import is read-only and runs in the existing disk-service worker;
        // it never overwrites the editor's XML or the user's export directory.
        var imported = await WorkspaceDiskService.ImportAsync(folder);
        if (!ReferenceEquals(_workspace, original))
            throw new InvalidOperationException("核对过程中切换了工程；请重新选择导出目录。");
        var audit = await Task.Run(() => GameExportAudit.Compare(original, imported));
        if (!ReferenceEquals(_workspace, original))
            throw new InvalidOperationException("核对过程中切换了工程；结果已丢弃。");
        _gameExportAudit = audit;
        GameExportDifferences.Clear();
        foreach (var difference in audit.Differences)
            GameExportDifferences.Add(new GameExportDifferenceRow(difference));
        Notify(nameof(GameExportAuditSummary));
        Status = audit.XmlMatches
            ? "已确认导出目录 XML 与当前工作站逐字一致；此结论不代表游戏加载成功。"
            : $"导出目录 XML 有 {audit.TotalDifferences} 处差异，请核对后再实机验证。";
    }

    public string GenerateGameVerificationReport()
    {
        if (_document == null)
            throw new InvalidOperationException("没有可关联的 XML 文件。");
        var checklist = new GameVerificationChecklist(
            GameVersion, GameEnabledDlc, GameTester, GameNotes, GameLoaded,
            GameSpawned, GameGraphicsChecked, GameCombatChecked,
            GameGestationChecked, GameReimportChecked);
        var number = _workspace?.DefinitionCount ?? _document.Definitions().Count;
        var located = GameLogIssues.Where(x => x.CanNavigate)
            .Select(x => new GameLogDefinitionLocation(x.Evidence.LineNumber,
                x.DefName, x.DefType, x.RelativePath)).ToArray();
        return GameVerificationReport.Generate(checklist, _gameLogEvidence,
            GameLoadEvidence.ProjectFingerprint(_workspace, _document), number,
            _gameExportAudit, located);
    }
}

public sealed record GameLogIssueRow(
    GameLoadEvidence.LogIssue Evidence, string DefName,
    string DefType, string RelativePath, bool IsAmbiguous)
{
    public bool CanNavigate => !IsAmbiguous && DefType.Length != 0;
    public string Display => $"第 {Evidence.LineNumber} 行 · {Evidence.Kind} · " +
        $"{DefName} · " + (IsAmbiguous ? "同名定义不唯一，无法自动定位" :
        CanNavigate ? (RelativePath.Length == 0 ? $"当前文件 / {DefType}" :
            $"{DefType} · {RelativePath}") : "源定义已不存在");
}

public sealed record GameExportDifferenceRow(GameExportAudit.Difference Difference)
{
    public string Display => Difference.Display;
}
