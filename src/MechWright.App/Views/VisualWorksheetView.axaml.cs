using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private async void AddField_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel vm)return;
        try
        {
            if(TopLevel.GetTopLevel(this) is Window owner)
                await new MechWright.App.VisualContentWindow(vm,"全部内容")
                    .ShowDialog(owner);
        }
        catch(Exception ex){vm.ReportError(ex);}
    }
}
