using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualBasicView : UserControl
{
    public VisualBasicView() => InitializeComponent();

    private async void AddContent_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel vm) return;
        try
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
                await new MechWright.App.VisualContentWindow(vm,"全部内容").ShowDialog(owner);
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }

    private void ApplyVisualBasic_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ApplyVisualBasic();
    }
}
