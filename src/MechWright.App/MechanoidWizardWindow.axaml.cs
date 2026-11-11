using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App;

public sealed partial class MechanoidWizardWindow : Window
{
    private readonly MechanoidWizardViewModel _vm = new();

    public MechanoidWizardWindow(string? suggestedDefName = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(suggestedDefName))
            _vm.DefName = suggestedDefName;
        DataContext = _vm;
    }

    private void Preview_Click(object? sender, RoutedEventArgs e) => _vm.TryPreview();

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm.TryConfirm(out var blueprint))
            Close(blueprint);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
