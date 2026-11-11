using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualBodyView : UserControl
{
    public VisualBodyView() => InitializeComponent();

    private void ApplyBodyTemplate_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ApplySelectedBodyTemplate();
    }
    private void AddPart_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.AddVisualBodyPart();
    }
    private void AddCustomBodyPart_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm)vm.AddBodyPart();
    }
    private void ApplyCoverage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ApplyVisualPartCoverage();
    }
    private void RemovePart_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.RemoveBodyPart();
    }
    private async void ConnectGame_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider == null) return;
        try
        {
            var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择 RimWorld 游戏目录或 Data 文件夹",
                AllowMultiple = false
            });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) await vm.LoadCatalogAsync(path);
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }
}
