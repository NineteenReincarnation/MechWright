using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using MechWright.RimWorld;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MechWright.App.ViewModels;
using MechWright.Infrastructure;

namespace MechWright.App;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Opened += async (_, _) =>
        {
            var saved = RecentGamePath.Find();
            if (saved == null) return;
            await _vm.LoadCatalogAsync(saved);
        };
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        // Text fields that still have keyboard focus have not necessarily
        // been committed to the document yet.
        if (!_vm.IsDirty && !_vm.HasUncommittedDocumentInputs) return true;
        var dialog = new Window
        {
            Width = 390, Height = 165,
            CanResize = false,
            Title = "尚未导出修改",
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.Black
        };
        var question = new TextBlock
        {
            Text = "当前 XML 有未导出的修改。确定放弃这些修改？",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(12)
        };
        var discard = new Button { Content = "放弃修改", MinWidth = 100 };
        var cancel = new Button { Content = "继续编辑", MinWidth = 100 };
        discard.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { cancel, discard }
        };
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 12,
            Children = { question, buttons }
        };
        return await dialog.ShowDialog<bool>(this);
    }

    private async void NewFile_Click(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmDiscardAsync()) _vm.NewFile();
    }


    private async void NewWorkspace_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardAsync()) return;
        _vm.CreateWorkspace();
    }

    private async void NewMechanoidWizard_Click(object? sender, RoutedEventArgs e)
    {
        if (!_vm.HasWorkspace && !await ConfirmDiscardAsync()) return;
        try
        {
            var sequence = 1;
            while (_vm.Workspace?.FindDefinitionFile("ThingDef",
                       $"MP_Mech_{sequence:000}") != null) sequence++;
            var blueprint = await new MechanoidWizardWindow($"MP_Mech_{sequence:000}")
                .ShowDialog<MechWright.RimWorld.MechanoidBlueprint?>(this);
            if (blueprint == null) return;
            if (!_vm.HasWorkspace) _vm.CreateWorkspace();
            _vm.GenerateMechanoid(blueprint);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private async void ImportWorkspace_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardAsync()) return;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择 Mod 根目录（包含 Defs / Patches 或 1.7/Defs）",
                AllowMultiple = false
            });
            var folder = folders.FirstOrDefault()?.TryGetLocalPath();
            if (folder == null) return;
            _vm.StatusMessage("正在读取 Mod 工程 XML；原文件不会被修改");
            var project = await WorkspaceDiskService.ImportAsync(folder);
            _vm.OpenWorkspace(project);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private void SwitchToDocument_Click(object? sender, RoutedEventArgs e) =>
        _vm.UseDocumentMode();

    private void SwitchToGuided_Click(object? sender, RoutedEventArgs e) =>
        _vm.UseGuidedMode();

    private void AddDefinitionInline_Click(object? sender, RoutedEventArgs e)
    {
        _vm.UseDocumentMode();
        _vm.ToggleDocumentCreatePanel();
    }

    private void PreviousStage_Click(object? sender, RoutedEventArgs e) => _vm.PreviousStage();
    private void NextStage_Click(object? sender, RoutedEventArgs e) => _vm.NextStage();
    private void ReturnToReview_Click(object? sender, RoutedEventArgs e) => _vm.WorkflowStep = 4;
    private void OpenAdvanced_Click(object? sender, RoutedEventArgs e) => _vm.WorkflowStep = 5;
    private async void OpenContentEditor_Click(object? sender,RoutedEventArgs e)
    {
        if (!_vm.HasWorkspace)
        {
            _vm.StatusMessage("请先创建或导入一台机械体，再添加配置。");
            return;
        }
        try { await new VisualContentWindow(_vm).ShowDialog(this); }
        catch (Exception ex) { _vm.ReportError(ex); }
    }
    private void GoToAssets_Click(object? sender, RoutedEventArgs e) => _vm.WorkflowStep = 3;

    private void AddProjectXml_Click(object? sender, RoutedEventArgs e) =>
        _vm.AddProjectXml();
    private async void ChooseProjectAsset_Click(object? sender, RoutedEventArgs e)
    {
        if (!_vm.HasWorkspace) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "登记本机 PNG / OGG / WAV / DLL 资源（不修改原始文件）",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Mod resources")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.ogg", "*.wav", "*.dll"]
                    }
                ]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) _vm.ChooseAssetSource(path);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private void RegisterProjectAsset_Click(object? sender, RoutedEventArgs e) =>
        _vm.RegisterProjectAsset();
    private void RemoveProjectAsset_Click(object? sender, RoutedEventArgs e) =>
        _vm.RemoveProjectAsset();


    private sealed record ProjectExportOptions(string Name, string PackageId,
        string FolderName, bool IncludeAssets);

    private async Task<ProjectExportOptions?> GetProjectExportOptionsAsync(string name)
    {
        var dialog = new Window
        {
            Title = "EXPORT PROJECT · 工程 XML 导出设置",
            Width = 500, Height = 380, MinWidth = 420, MinHeight = 350,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.Black
        };
        var nameBox = new TextBox { Text = name };
        var suggested = System.Text.RegularExpressions.Regex.Replace(
            name.ToLowerInvariant(), "[^a-z0-9]", "");
        if (suggested.Length == 0) suggested = "newmod";
        var packageBox = new TextBox { Text = $"local.mechwright.{suggested}" };
        var folderBox = new TextBox { Text = "MechWright-" + suggested };
        var includeAssets = new CheckBox
        {
            Content = $"包含此工程自有资源（{_vm.Workspace?.Assets.Count ?? 0} 项）",
            IsChecked = false
        };
        var warning = new TextBlock
        {
            Text = "仅在勾选时复制当前工程清单中的本地素材；不会复制原版/DLC 文件，也不会覆盖已有目标文件夹。",
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        };
        var issue = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "确认参数并选择目标目录" };
        var cancel = new Button { Content = "取消" };
        ok.Click += (_, _) =>
        {
            var folder = folderBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(folder) ||
                folder is "." or ".." ||
                folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                folder.Contains('/') || folder.Contains('\\') ||
                folder.Contains(':') || folder.EndsWith('.'))
            {
                issue.Text = "输出目录名只能是单个合法文件夹名称，不允许路径或上级目录。";
                return;
            }
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                issue.Text = "请填写 Mod 名称。";
                return;
            }
            dialog.Close(new ProjectExportOptions(nameBox.Text.Trim(),
                packageBox.Text?.Trim() ?? "", folder, includeAssets.IsChecked == true));
        };
        cancel.Click += (_, _) => dialog.Close(null);
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "MOD NAME · 显示名称", Foreground = Brushes.LightGray });
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = "PACKAGE ID · 使用独立反向域名标识", Foreground = Brushes.LightGray });
        panel.Children.Add(packageBox);
        panel.Children.Add(new TextBlock { Text = "OUTPUT FOLDER · 新目录名称", Foreground = Brushes.LightGray });
        panel.Children.Add(folderBox);
        panel.Children.Add(includeAssets);
        panel.Children.Add(warning);
        panel.Children.Add(issue);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 9,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, ok }
        };
        panel.Children.Add(buttons);
        dialog.Content = new ScrollViewer { Content = panel };
        return await dialog.ShowDialog<ProjectExportOptions?>(this);
    }

    private async void ExportWorkspace_Click(object? sender, RoutedEventArgs e)
    {
        var project = _vm.Workspace;
        if (project == null)
        {
            _vm.StatusMessage("请先新建或导入多文件工程");
            return;
        }
        var options = await GetProjectExportOptionsAsync(project.Name);
        if (options == null) return;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择导出目录的上级文件夹（不会覆盖已有内容）",
                AllowMultiple = false
            });
            var parent = folders.FirstOrDefault()?.TryGetLocalPath();
            if (parent == null) return;
            var destination = Path.Combine(parent, options.FolderName);
            _vm.StatusMessage("正在安全导出完整 XML 工程…");
            await WorkspaceDiskService.ExportAsync(project, destination,
                options.Name, options.PackageId, includeAssets: options.IncludeAssets);
            _vm.WorkspaceExported(destination, options.IncludeAssets);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private async void OpenFile_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardAsync()) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "打开 RimWorld XML",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("XML Definitions") { Patterns = ["*.xml"] }]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return;
            var content = await File.ReadAllTextAsync(path);
            _vm.LoadFile(content, path);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private VanillaLibraryWindow? _vanillaLibraryWindow;

    private void OpenVanillaLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (_vanillaLibraryWindow != null)
        {
            _vanillaLibraryWindow.Activate();
            return;
        }
        var viewer = new VanillaLibraryWindow { DataContext = _vm };
        _vanillaLibraryWindow = viewer;
        viewer.Closed += (_, _) => _vanillaLibraryWindow = null;
        viewer.Show(this);
        _vm.StatusMessage("原版机械体图鉴已打开，可与主设计工作台并排使用。");
    }

    private async void ConnectGameSchema_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择 RimWorld 游戏根目录或 Data 文件夹（只读）",
                AllowMultiple = false
            });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) await _vm.LoadCatalogAsync(path);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private async void ScanCatalog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择 RimWorld 游戏根目录、Data 或 Mod 目录",
                AllowMultiple = false
            });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (path != null)
            {
                await _vm.LoadCatalogAsync(path);
                OpenVanillaLibrary_Click(sender, e);
            }
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private async void OpenCatalog_Click(object? sender, RoutedEventArgs e)
    {
        var item = _vm.SelectedCatalog?.Entry;
        if (item == null || !await ConfirmDiscardAsync()) return;
        try
        {
            var text = await File.ReadAllTextAsync(item.FilePath);
            _vm.LoadFile(text, item.FilePath, item.Identity.DefName);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        // The toolbar is the only disk-save action. Apply any focused list
        // input first so Save XML never exports stale values.
        if (_vm.IsDocumentMode && !_vm.CommitDocumentInputs()) return;
        var xml = _vm.GetExportText();
        if (xml == null) return;
        try
        {
            // Always ask the user where to write; never silently modify the original game directory.
            var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出游戏原生 Def XML",
                SuggestedFileName = "MP_Mechanoid.xml",
                FileTypeChoices = [new FilePickerFileType("XML Definitions") { Patterns = ["*.xml"] }],
                DefaultExtension = "xml",
                ShowOverwritePrompt = true
            });
            var path = destination?.TryGetLocalPath();
            if (path == null) return;
            await AtomicFileWriter.WriteAsync(path, xml);
            _vm.Saved(path, xml);
        }
        catch (Exception ex) { _vm.ReportError(ex); }
    }

    private async void EditRaw_Click(object? sender, RoutedEventArgs e)
    {
        if (!_vm.HasDocument) { _vm.NewFile(); }
        var dialog = new Window
        {
            Title = "XML SOURCE · 精确编辑",
            Width = 980, Height = 680, MinWidth = 640, MinHeight = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.Black
        };
        var editor = new TextBox
        {
            Text = _vm.XmlPreview,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var error = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
        var apply = new Button { Content = "解析并应用（可撤销）" };
        var cancel = new Button { Content = "取消" };
        apply.Click += (_, _) =>
        {
            try
            {
                DefDocument.Parse(editor.Text ?? "");
                dialog.Close(true);
            }
            catch (Exception ex) { error.Text = "XML 无效 · " + ex.Message; }
        };
        cancel.Click += (_, _) => dialog.Close(false);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, apply }
        };
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(15),
            RowSpacing = 10
        };
        var info = new TextBlock
        {
            Text = "直接编辑完整 XML（含未知原版 / DLC 字段）。应用后作为一次操作写入当前文档，可 Ctrl+Z 撤销。",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(info, 0); grid.Children.Add(info);
        Grid.SetRow(editor, 1); grid.Children.Add(editor);
        Grid.SetRow(error, 2); grid.Children.Add(error);
        Grid.SetRow(buttons, 3); grid.Children.Add(buttons);
        dialog.Content = grid;
        if (await dialog.ShowDialog<bool>(this))
        {
            try { _vm.ReplaceRawXml(editor.Text ?? ""); }
            catch (Exception ex) { _vm.ReportError(ex); }
        }
    }

    private void ApplyEdit_Click(object? sender, RoutedEventArgs e) => _vm.ApplyEdit();
    private void Undo_Click(object? sender, RoutedEventArgs e) => _vm.Undo();
    private void Redo_Click(object? sender, RoutedEventArgs e) => _vm.Redo();
    private void AddDefinition_Click(object? sender, RoutedEventArgs e) => _vm.AddDefinition();
    private void ApplyBody_Click(object? sender, RoutedEventArgs e) => _vm.ApplyBodyPart();
    private void JumpReference_Click(object? sender, RoutedEventArgs e) => _vm.JumpToReference();
    private void ApplyMechProperty_Click(object? sender, RoutedEventArgs e) => _vm.ApplyMechProperty();
    private void ApplyNativeField_Click(object? sender, RoutedEventArgs e) => _vm.ApplyNativeField();
    private void AddNativeList_Click(object? sender, RoutedEventArgs e) => _vm.AddNativeListEntry();
    private void ReplaceNativeList_Click(object? sender, RoutedEventArgs e) => _vm.ReplaceNativeListEntry();
    private void DuplicateNativeList_Click(object? sender, RoutedEventArgs e) => _vm.DuplicateNativeListEntry();
    private void MoveNativeListUp_Click(object? sender, RoutedEventArgs e) => _vm.MoveNativeListEntry(-1);
    private void MoveNativeListDown_Click(object? sender, RoutedEventArgs e) => _vm.MoveNativeListEntry(1);
    private void RemoveNativeList_Click(object? sender, RoutedEventArgs e) => _vm.RemoveNativeListEntry();
    private void ApplyXmlAttribute_Click(object? sender, RoutedEventArgs e) => _vm.ApplyXmlAttribute();
    private void RemoveXmlAttribute_Click(object? sender, RoutedEventArgs e) => _vm.RemoveXmlAttribute();
    private void AddXmlAttribute_Click(object? sender, RoutedEventArgs e) => _vm.AddXmlAttribute();
    private void ResetMechProperty_Click(object? sender, RoutedEventArgs e) => _vm.ResetMechProperty();
    private void AddBody_Click(object? sender, RoutedEventArgs e) => _vm.AddBodyPart();
    private void RemoveBody_Click(object? sender, RoutedEventArgs e) => _vm.RemoveBodyPart();
    private void MoveBodyUp_Click(object? sender, RoutedEventArgs e) => _vm.MoveBodyPart(-1);
    private void MoveBodyDown_Click(object? sender, RoutedEventArgs e) => _vm.MoveBodyPart(1);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Keep native TextBox undo/copy/editing shortcuts intact.
        if (e.Source is TextBox)
        {
            base.OnKeyDown(e);
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z) { _vm.Undo(); e.Handled = true; }
            else if (e.Key == Key.Y) { _vm.Redo(); e.Handled = true; }
            else if (e.Key == Key.N) { NewFile_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.O) { OpenFile_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.S) { Export_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Right && _vm.IsGuidedMode)
            {
                _vm.NextStage();
                e.Handled = true;
            }
            else if (e.Key == Key.Left && _vm.IsGuidedMode)
            {
                _vm.PreviousStage();
                e.Handled = true;
            }
        }
        if (!e.Handled) base.OnKeyDown(e);
    }
}
