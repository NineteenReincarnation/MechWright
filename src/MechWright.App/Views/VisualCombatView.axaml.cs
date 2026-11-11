using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualCombatView : UserControl
{
    public VisualCombatView() => InitializeComponent();

    private void AddVisualMelee_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.AddVisualMelee();
    }
    private void RemoveVisualMelee_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.RemoveVisualMelee();
    }
    private void ApplyRanged_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ApplySelectedRangedPreset();
    }
}
