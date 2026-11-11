using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualGraphicView : UserControl
{
    public VisualGraphicView() => InitializeComponent();

    private async void SelectArchive_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider == null) return;
        try
        {
            var selected = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "选择机械体方向贴图 ZIP 包",
                FileTypeFilter = [new FilePickerFileType("ZIP 贴图包")
                {
                    Patterns = ["*.zip"]
                }]
            });
            var path = selected.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) vm.OpenTextureArchive(path);
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }

    private void ImportArchive_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ImportSelectedArchiveTextures();
    }

    private async void SelectTextures_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider == null) return;
        try
        {
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = "选择机械体 south / north / east / west 方向的透明 PNG 图片",
                FileTypeFilter =
                [
                    new FilePickerFileType("RimWorld directional PNG")
                    {
                        Patterns = ["*.png"]
                    }
                ]
            });
            var selected = files.Select(x => x.TryGetLocalPath())
                .Where(x => x != null).Cast<string>().ToArray();
            vm.RegisterMechTextures(selected);
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }
}
