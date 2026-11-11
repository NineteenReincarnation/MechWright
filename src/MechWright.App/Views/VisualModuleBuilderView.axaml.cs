using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualModuleBuilderView : UserControl
{
    public VisualModuleBuilderView()=>InitializeComponent();
    private void AddModule_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.AddFreeformModule(); }
    private void RemoveModule_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.RemoveFreeformModule(); }
    private void AddNode_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.AddFreeformField(); }
    private void SaveNode_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.SaveFreeformNode(); }
    private void RemoveNode_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.RemoveFreeformNode(); }
    private void SaveAttribute_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.SaveFreeformAttribute(); }
}
