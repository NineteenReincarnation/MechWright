using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualGestationView : UserControl
{
    public VisualGestationView()=>InitializeComponent();

    private void AddIngredient_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm)vm.AddGestationMaterial();
    }
    private void RemoveIngredient_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm &&
            sender is Button { DataContext: GestationIngredientRow row })
            vm.RemoveGestationMaterial(row);
    }
    private void SaveRecipe_Click(object? sender,RoutedEventArgs e)
    {
        if(DataContext is MainWindowViewModel vm)vm.SaveVisualGestation();
    }
}
