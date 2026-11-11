using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

/// <summary>Shared live read-only vanilla encyclopedia surface.</summary>
public sealed partial class VanillaLibraryView : UserControl
{
    public VanillaLibraryView() => InitializeComponent();

    private void CompareVanilla_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.CompareVanillaWithCurrent();
    }
}
