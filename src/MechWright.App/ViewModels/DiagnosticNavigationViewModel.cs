using System.Text.RegularExpressions;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DiagnosticRow? _selectedDiagnostic;

    public DiagnosticRow? SelectedDiagnostic
    {
        get => _selectedDiagnostic;
        set
        {
            if (ReferenceEquals(value, _selectedDiagnostic)) return;
            _selectedDiagnostic = value;
            Notify();
            Notify(nameof(HasSelectedDiagnostic));
        }
    }

    public bool HasDiagnostics => Diagnostics.Count != 0;
    public bool HasSelectedDiagnostic => SelectedDiagnostic != null;

    /// <summary>
    /// Locates a diagnostic's source XML file and nearest existing structure
    /// node. Never resolves other files by message text or guesses a missing
    /// external Def; no source XML is rewritten by navigation itself.
    /// </summary>
    public void JumpToDiagnostic()
    {
        var diagnostic = SelectedDiagnostic;
        if (diagnostic == null) return;

        if (diagnostic.FilePath.Length != 0 && _workspace != null)
        {
            var file = ProjectFiles.FirstOrDefault(x => string.Equals(
                x.File.RelativePath, diagnostic.FilePath,
                StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                Status = "诊断对应的工程文件不再存在；请重新运行检查。";
                return;
            }
            SelectedProjectFile = file;
            if (!ReferenceEquals(SelectedProjectFile, file))
            {
                Status = "当前输入未成功保存，无法切换到诊断文件。";
                return;
            }
        }

        OpenWorkbenchArea(0);
        if (!_isDocumentMode) UseDocumentMode();
        if (_document == null)
        {
            Status = "目标 XML 文件已关闭。";
            return;
        }

        var location = diagnostic.Issue.Location;
        if (location.StartsWith("/Defs/", StringComparison.Ordinal))
            location = "/Defs[1]" + location["/Defs".Length..];

        var target = Regex.Match(location,
            @"^/Defs\[\d+\]/(?<type>[A-Za-z_][A-Za-z0-9_]*)\[(?<index>[1-9][0-9]*)\]",
            RegexOptions.CultureInvariant);
        if (target.Success && int.TryParse(target.Groups["index"].Value, out var ordinal))
        {
            var definition = Definitions.Where(x =>
                x.Entry.Type == target.Groups["type"].Value)
                .Skip(ordinal - 1).FirstOrDefault();
            if (definition != null)
            {
                SearchQuery = "";
                SelectedDefinition = definition;
                if (SelectedDefinition?.Entry.Type != definition.Entry.Type ||
                    SelectedDefinition?.Entry.Index != definition.Entry.Index)
                {
                    Status = "请先处理当前未提交的输入，再定位该字段。";
                    return;
                }
            }
        }

        var field = DocumentNodes.FirstOrDefault(x => x.Path == location);
        if (field == null)
            field = DocumentNodes.Where(x => location.StartsWith(x.Path + "/",
                    StringComparison.Ordinal))
                .OrderByDescending(x => x.Path.Length).FirstOrDefault();

        if (field != null)
        {
            DocumentSelectedNode = field;
            if (!InlineAdvancedOpen) ToggleInlineAdvanced();
            Status = $"已定位 {diagnostic.Issue.Code} · {SourceName} · {field.Path}";
        }
        else
            Status = $"已打开诊断所在文件 · {SourceName} · {location}。" +
                "目标可能是整个文件、已删除的字段或 Patch XPath；可查看 XML 源码。";
    }
}
