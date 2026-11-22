using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DefCatalog.CatalogScan? _fieldEvidenceScan;

    public bool HasFieldEvidenceReport => _fieldEvidenceScan?.IsGameInstallation == true;

    public string GenerateFieldEvidenceReport() =>
        FieldCoverageReport.Generate(_fieldEvidenceScan ??
            throw new InvalidOperationException(
                "请先通过“连接本机游戏字段”读取 RimWorld 安装目录。"));

    private void UseFieldEvidenceScan(DefCatalog.CatalogScan scan)
    {
        _fieldEvidenceScan = scan.IsGameInstallation ? scan : null;
        Notify(nameof(HasFieldEvidenceReport));
    }
}
