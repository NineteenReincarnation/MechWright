using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// Game validation is explicitly separate from static XML diagnostics and CI.
/// Only derived log counters and hashes are retained; Player.log text is
/// discarded immediately after scanning.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public ObservableCollection<GameLogIssueRow> GameLogIssues { get; } = new();
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
        var report = GameLoadEvidence.Analyze(log, names, snapshot);
        _gameLogEvidence = report;
        GameLogIssues.Clear();
        foreach (var issue in report.Issues) GameLogIssues.Add(new GameLogIssueRow(issue));
        Notify(nameof(GameLogSummary));
        Status = "已扫描本地日志；没有保存原始日志。请在验收区核对游戏版本和实际操作。";
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
        return GameVerificationReport.Generate(checklist, _gameLogEvidence,
            GameLoadEvidence.ProjectFingerprint(_workspace, _document), number);
    }
}

public sealed record GameLogIssueRow(GameLoadEvidence.LogIssue Evidence)
{
    public string Display => Evidence.Display;
}
