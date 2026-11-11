using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualAssetsView : UserControl
{
    public VisualAssetsView() => InitializeComponent();

    private async void ChooseResources_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider == null) return;
        try
        {
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = "添加自己的 PNG / OGG / WAV / DLL 资源",
                FileTypeFilter = [new FilePickerFileType("Mod 资源")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.ogg", "*.wav", "*.dll"]
                }]
            });
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (path == null) continue;
                vm.ChooseAssetSource(path);
                vm.RegisterProjectAsset();
            }
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }

    private void RemoveResource_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.RemoveProjectAsset();
    }
}
