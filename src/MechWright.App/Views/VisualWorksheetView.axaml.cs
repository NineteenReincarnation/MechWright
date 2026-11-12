using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualWorksheetView : UserControl
{
    public VisualWorksheetView()=>InitializeComponent();

    private void SaveRow_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm &&
           sender is Button { DataContext: WorksheetFieldRow row })
            vm.SaveWorksheetField(row);
    }

    private void ResetRow_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm &&
           sender is Button { DataContext: WorksheetFieldRow row })
            vm.RemoveWorksheetField(row);
    }

    private void AddField_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm) vm.ToggleWorksheetCatalog();
    }

    private void AddCatalogRow_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm &&
           sender is Button { DataContext: WorksheetCatalogRow row })
            vm.AddWorksheetCatalogField(row);
    }

    private void DiscardRow_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm &&
           sender is Button { DataContext: WorksheetFieldRow row })
            vm.DiscardWorksheetDraft(row);
    }

    private async void ScanFields_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel vm)return;
        try
        {
            var provider=TopLevel.GetTopLevel(this)?.StorageProvider;
            if(provider==null)return;
            var chosen=await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title="选择 RimWorld 安装目录或 Data 文件夹",
                AllowMultiple=false
            });
            var path=chosen.FirstOrDefault()?.TryGetLocalPath();
            if(path!=null)await vm.LoadCatalogAsync(path);
        }
        catch(Exception ex){vm.ReportError(ex);}
    }
}
