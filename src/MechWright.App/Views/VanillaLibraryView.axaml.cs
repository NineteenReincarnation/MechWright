using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

/// <summary>Shared live read-only vanilla encyclopedia surface.</summary>
public sealed partial class VanillaLibraryView : UserControl
{
    public VanillaLibraryView() => InitializeComponent();

    private async void SelectRimWorld_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider == null) return;
        try
        {
            var folders = await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "选择 RimWorld 根目录或 Data 文件夹",
                    AllowMultiple = false
                });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
                await vm.LoadCatalogAsync(path);
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }

    private void CompareVanilla_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.CompareVanillaWithCurrent();
    }
}
