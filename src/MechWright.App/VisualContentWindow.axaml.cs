using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MechWright.App.ViewModels;

namespace MechWright.App;

public sealed partial class VisualContentWindow : Window
{
    private readonly MainWindowViewModel _vm;

    public VisualContentWindow(MainWindowViewModel vm, string? category = null)
    {
        InitializeComponent();
        _vm = vm;
        _vm.OpenContentEditor(category);
        DataContext = _vm;
        Closed += (_, _) => _vm.CloseContentEditor();
    }

    private void ApplyField_Click(object? sender,RoutedEventArgs e) =>
        _vm.ApplySelectedContentField();
    private void RemoveField_Click(object? sender,RoutedEventArgs e) =>
        _vm.RemoveSelectedContentField();
    private void InstallModule_Click(object? sender,RoutedEventArgs e) =>
        _vm.InstallSelectedContentTemplate();
    private void SaveModuleParam_Click(object? sender,RoutedEventArgs e) =>
        _vm.ApplySelectedModuleParameter();
    private void DuplicateModule_Click(object? sender,RoutedEventArgs e) =>
        _vm.DuplicateSelectedContentModule();
    private void RemoveModule_Click(object? sender,RoutedEventArgs e) =>
        _vm.RemoveSelectedContentModule();
    private void Close_Click(object? sender,RoutedEventArgs e) => Close();

    private async void ScanGame_Click(object? sender,RoutedEventArgs e)
    {
        var provider=StorageProvider;
        try
        {
            var folders=await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title="选择 RimWorld 游戏安装目录或 Data 文件夹",
                AllowMultiple=false
            });
            var path=folders.FirstOrDefault()?.TryGetLocalPath();
            if(path!=null)await _vm.LoadCatalogAsync(path);
        }
        catch(Exception ex){ _vm.ReportError(ex); }
    }
}
