using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class DocumentWorksheetView : UserControl
{
    public DocumentWorksheetView() => InitializeComponent();

    private void AppendDefinition_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.AppendDocumentDefinition();
    }

    private void AddField_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.AddDocumentField();
    }

    private void SaveNode_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is Button { DataContext: DocumentNodeRow row })
            vm.SaveDocumentNode(row);
    }

    private void RemoveNode_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is Button { DataContext: DocumentNodeRow row })
            vm.RemoveDocumentNode(row);
    }
}
