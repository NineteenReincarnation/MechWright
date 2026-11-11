using Avalonia.Controls;
using Avalonia.Input;
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
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_vm.IsDirty) return true;
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
            if (path != null) await _vm.LoadCatalogAsync(path);
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

    private void ApplyEdit_Click(object? sender, RoutedEventArgs e) => _vm.ApplyEdit();
    private void Undo_Click(object? sender, RoutedEventArgs e) => _vm.Undo();
    private void Redo_Click(object? sender, RoutedEventArgs e) => _vm.Redo();
    private void AddDefinition_Click(object? sender, RoutedEventArgs e) => _vm.AddDefinition();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z) { _vm.Undo(); e.Handled = true; }
            else if (e.Key == Key.Y) { _vm.Redo(); e.Handled = true; }
            else if (e.Key == Key.N) { NewFile_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.O) { OpenFile_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.S) { Export_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }
        if (!e.Handled) base.OnKeyDown(e);
    }
}
