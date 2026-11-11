using System.Collections.ObjectModel;
using MechWright.Infrastructure;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<ArchiveTextureGroupRow> ArchiveTextureGroups { get; } = new();
    private ArchiveTextureGroupRow? _selectedArchiveTextureGroup;
    private string? _textureArchivePath;
    private string _archiveTextureInfo = "可直接打开机械体贴图 ZIP，选择其中一台机械体导入。";

    public ArchiveTextureGroupRow? SelectedArchiveTextureGroup
    {
        get => _selectedArchiveTextureGroup;
        set { _selectedArchiveTextureGroup = value; Notify(); Notify(nameof(CanImportArchiveTextures)); }
    }

    public bool CanImportArchiveTextures => _workspace != null &&
        CurrentMechName != null && SelectedArchiveTextureGroup != null;
    public bool HasArchiveTextureGroups => ArchiveTextureGroups.Count > 0;
    public string ArchiveTextureInfo
    {
        get => _archiveTextureInfo;
        private set { _archiveTextureInfo = value; Notify(); }
    }

    public void OpenTextureArchive(string zipPath)
    {
        ArchiveTextureGroups.Clear();
        SelectedArchiveTextureGroup = null;
        _textureArchivePath = null;
        Notify(nameof(HasArchiveTextureGroups));
        try
        {
            foreach (var group in DirectionalTextureArchive.Inspect(zipPath))
                ArchiveTextureGroups.Add(new ArchiveTextureGroupRow(group));
            _textureArchivePath = zipPath;
            SelectedArchiveTextureGroup = ArchiveTextureGroups.FirstOrDefault();
            Notify(nameof(HasArchiveTextureGroups));
            ArchiveTextureInfo = ArchiveTextureGroups.Count == 0
                ? "没有找到南、北、东三个方向齐全的机械体。支持空格或下划线命名。"
                : "找到 " + ArchiveTextureGroups.Count + " 组完整贴图。选择一组导入当前机械体。";
        }
        catch (Exception ex)
        {
            ArchiveTextureInfo = "无法读取 ZIP：" + ex.Message;
            ReportError(ex);
        }
    }

    public void ImportSelectedArchiveTextures()
    {
        if (_textureArchivePath == null || SelectedArchiveTextureGroup == null ||
            !CanImportArchiveTextures)
        {
            ArchiveTextureInfo = "请先打开工程、选择机械体，再选择 ZIP 内的贴图。";
            return;
        }
        try
        {
            var files = DirectionalTextureArchive.Extract(
                _textureArchivePath, SelectedArchiveTextureGroup.Group);
            RegisterMechTextures(files);
            ArchiveTextureInfo = "已选择 " + SelectedArchiveTextureGroup.Group.Name +
                " 的方向贴图，已交给当前机械体的资源登记流程。";
        }
        catch (Exception ex)
        {
            ArchiveTextureInfo = "导入失败：" + ex.Message;
            ReportError(ex);
        }
    }
}

public sealed record ArchiveTextureGroupRow(DirectionalTextureArchive.TextureGroup Group)
{
    public string Display => Group.Display;
}
