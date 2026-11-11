using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualDefinitionBuilderView : UserControl
{
    public VisualDefinitionBuilderView()=>InitializeComponent();
    private void CreateDefinition_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.CreateFreeformDefinition(); }
    private void AddField_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.AddFreeformDefinitionField(); }
    private void SaveNode_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.SaveFreeformDefinitionValue(); }
    private void RemoveNode_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.RemoveFreeformDefinitionField(); }
    private void SaveAttribute_Click(object? sender,RoutedEventArgs e)
    { if(DataContext is MainWindowViewModel vm)vm.SaveFreeformDefinitionAttribute(); }
}
