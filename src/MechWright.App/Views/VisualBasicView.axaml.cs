using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualBasicView : UserControl
{
    public VisualBasicView() => InitializeComponent();

    private void ApplyVisualBasic_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ApplyVisualBasic();
    }
}
