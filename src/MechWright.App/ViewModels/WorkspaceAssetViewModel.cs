using System.Collections.ObjectModel;
using MechWright.Infrastructure;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<ProjectAssetRow> ProjectAssets { get; } = new();
    private ProjectAssetRow? _selectedProjectAsset;
    private string _assetInputFile = "";
    private string _assetOutputPath = "";
    private string _assetMessage = "资源只在项目中登记；原文件只读，导出时由你决定是否复制。";

    public ProjectAssetRow? SelectedProjectAsset
    {
        get => _selectedProjectAsset;
        set { _selectedProjectAsset = value; Notify(); Notify(nameof(HasProjectAsset)); }
    }

    public bool HasProjectAsset => SelectedProjectAsset != null;
    public string AssetInputFile
    {
        get => _assetInputFile;
        private set { _assetInputFile = value; Notify(); }
    }
    public string AssetOutputPath
    {
        get => _assetOutputPath;
        set { _assetOutputPath = value; Notify(); }
    }
    public string AssetMessage
    {
        get => _assetMessage;
        private set { _assetMessage = value; Notify(); }
    }
    public string AssetSummary => _workspace == null
        ? "未打开工程"
        : WorkspaceAssetService.InventoryOf(_workspace).Description + "（仅项目资源）";

    private void RefreshProjectAssets()
    {
        ProjectAssets.Clear();
        if (_workspace != null)
            foreach (var asset in _workspace.Assets.OrderBy(x => x.RelativePath))
                ProjectAssets.Add(new ProjectAssetRow(asset));
        SelectedProjectAsset = ProjectAssets.FirstOrDefault();
        Notify(nameof(AssetSummary));
        RefreshWorkspaceIndicators();
    }

    public void ChooseAssetSource(string localFile)
    {
        if (_workspace == null)
        {
            AssetMessage = "先新建或导入工程，才能登记项目资源。";
            return;
        }
        AssetInputFile = localFile;
        var name = Path.GetFileName(localFile);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var kind = extension switch
        {
            ".png" or ".jpg" or ".jpeg" => "Textures/Things/Mechanoids/",
            ".ogg" or ".wav" => "Sounds/",
            ".dll" => "Assemblies/",
            _ => "Textures/"
        };
        AssetOutputPath = kind + name;
        AssetMessage = "请核对导出路径，再点击“登记资源”；不会立刻复制文件。";
    }

    public void RegisterProjectAsset()
    {
        if (_workspace == null || string.IsNullOrWhiteSpace(AssetInputFile))
        {
            AssetMessage = "请先打开工程并选择一个本机资源文件。";
            return;
        }
        try
        {
            WorkspaceAssetService.Register(_workspace, AssetInputFile, AssetOutputPath);
            AssetMessage = "已登记 · " + AssetOutputPath + "；导出时需要启用复制资源。";
            RefreshProjectAssets();
            Status = "已将本地资源添加到项目清单，未修改源文件";
        }
        catch (Exception ex)
        {
            AssetMessage = "无法添加项目资源：" + ex.Message;
            ReportError(ex);
        }
    }

    public void RemoveProjectAsset()
    {
        if (_workspace == null || SelectedProjectAsset == null) return;
        var name = SelectedProjectAsset.Asset.RelativePath;
        try
        {
            _workspace.RemoveAsset(name);
            RefreshProjectAssets();
            AssetMessage = "已从项目导出清单中移除 " + name + "；本地文件未删除。";
            Status = AssetMessage;
        }
        catch (Exception ex) { ReportError(ex); }
    }
}

public sealed record ProjectAssetRow(WorkspaceAsset Asset)
{
    public string Display => (Asset.IsNew ? "● " : "○ ") + Asset.RelativePath;
    public string Size => (Asset.Length / 1024d / 1024d).ToString("0.0") + " MiB";
    public string Source => Asset.SourcePath;
}
