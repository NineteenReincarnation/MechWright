using Avalonia.Controls;
using Avalonia.Interactivity;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

public sealed partial class DocumentWorksheetView : UserControl
{
    public DocumentWorksheetView() => InitializeComponent();

    private void StartBlank_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.BeginDocumentConfiguration();
    }

    private void QuickThing_Click(object? sender, RoutedEventArgs e) =>
        QuickCreate("ThingDef");
    private void QuickKind_Click(object? sender, RoutedEventArgs e) =>
        QuickCreate("PawnKindDef");
    private void QuickBody_Click(object? sender, RoutedEventArgs e) =>
        QuickCreate("BodyDef");
    private void QuickPart_Click(object? sender, RoutedEventArgs e) =>
        QuickCreate("BodyPartDef");

    private void QuickCreate(string type)
    {
        if (DataContext is MainWindowViewModel vm) vm.CreateQuickDefinition(type);
    }

    private void ToggleCreatePanel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ToggleDocumentCreatePanel();
    }

    private void SaveLabel_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.HasUncommittedDocumentInputs)
            vm.CommitDocumentInputs();
    }

    private void CommitValue_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is Control { DataContext: DocumentNodeRow row } && row.IsEdited)
            vm.SaveDocumentNode(row);
    }

    private void CommitBoolean_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is CheckBox { DataContext: DocumentNodeRow row } && row.IsEdited)
            vm.SaveDocumentNode(row);
    }

    private void CommitChoice_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is ComboBox { DataContext: DocumentNodeRow row } && row.IsEdited)
            vm.SaveDocumentNode(row);
    }

    private void SaveLabel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.SaveDocumentDisplayName();
    }

    private void ToggleQuickPanel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ToggleDocumentQuickPanel();
    }

    private void ConfirmQuickField_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ApplyDocumentQuickOption();
    }

    private void ToggleStructurePanel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ToggleDocumentStructurePanel();
    }

    private void ToggleGroup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is Button { DataContext: DocumentNodeRow row })
            vm.ToggleDocumentGroup(row);
    }

    private void AddIntoRow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is Button { DataContext: DocumentNodeRow row })
        {
            vm.ToggleDocumentStructurePanel(row);
            MainScroll.Offset = new Avalonia.Vector(0, 0);
        }
    }

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
