using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualBasicView : UserControl
{
    public VisualBasicView() => InitializeComponent();

    private async void AddContent_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm)
            await new MechWright.App.VisualContentWindow(vm,"全部内容")
                .ShowDialog(TopLevel.GetTopLevel(this) as Window ??
                    throw new InvalidOperationException("没有可以附加的主窗口。"));
    }

    private void ApplyVisualBasic_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ApplyVisualBasic();
    }
}
