using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class VisualReviewView : UserControl
{
    public VisualReviewView() => InitializeComponent();

    private void JumpDiagnostic_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.JumpToDiagnostic();
    }
}
