using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MechWright.App.ViewModels;

namespace MechWright.App.Views;

/// <summary>
/// The normal editor is one continuous list. Complex XML controls are tucked
/// away until explicitly requested at the end of that list.
/// </summary>
public sealed partial class DocumentWorksheetView : UserControl
{
    public DocumentWorksheetView()
    {
        InitializeComponent();
        KeyDown += OnEditorKeyDown;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.Key is Key.F or Key.K)
        {
            if (!vm.DocumentHasDefinition) vm.CreateQuickDefinition("ThingDef");
            Dispatcher.UIThread.Post(() => InlineSearchBox.Focus());
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Enter)
        {
            vm.CommitDocumentInputs();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ReferenceEquals(e.Source, IdentityEditor))
        {
            vm.CommitDocumentInputs();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ReferenceEquals(e.Source, LabelEditor))
        {
            vm.CommitDocumentInputs();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter &&
                 e.Source is TextBox { DataContext: InlineInspectorRow row })
        {
            vm.CommitInlineInspector(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (vm.InlineSearch.Length > 0)
            {
                vm.InlineSearch = "";
                e.Handled = true;
            }
            else if (vm.InlineAdvancedOpen)
            {
                vm.ToggleInlineAdvanced();
                e.Handled = true;
            }
        }
    }

    private void StartBlank_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CreateQuickDefinition("ThingDef");
    }

    private void SaveLabel_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.HasUncommittedDocumentInputs)
            Dispatcher.UIThread.Post(() => vm.CommitDocumentInputs(),
                DispatcherPriority.Background);
    }

    private void CommitIdentity_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.HasInlinePendingEdits)
            Dispatcher.UIThread.Post(() => vm.CommitDocumentInputs(),
                DispatcherPriority.Background);
    }

    private void CommitInline_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is Control { DataContext: InlineInspectorRow row } && row.IsEdited)
            Dispatcher.UIThread.Post(() => vm.CommitInlineInspector(row),
                DispatcherPriority.Background);
    }

    private void CommitInlineBoolean_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is CheckBox { DataContext: InlineInspectorRow row } && row.IsEdited)
            Dispatcher.UIThread.Post(() => vm.CommitInlineInspector(row),
                DispatcherPriority.Background);
    }

    private void CommitInlineChoice_SelectionChanged(object? sender,
        SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is ComboBox { DataContext: InlineInspectorRow row } && row.IsEdited)
            Dispatcher.UIThread.Post(() => vm.CommitInlineInspector(row),
                DispatcherPriority.Background);
    }

    private void ToggleAdvanced_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.ToggleInlineAdvanced();
    }

    private void AppendDefinition_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.AppendDocumentDefinition();
    }

    private void AddField_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.AddDocumentField();
    }

    private void RemoveSelectedNode_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            vm.DocumentSelectedNode is { CanDelete: true } row)
            vm.RemoveDocumentNode(row);
    }
}
