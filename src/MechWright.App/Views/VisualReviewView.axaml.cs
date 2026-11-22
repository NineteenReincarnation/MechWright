using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MechWright.App.ViewModels;
using MechWright.Infrastructure;

namespace MechWright.App.Views;

public sealed partial class VisualReviewView : UserControl
{
    public VisualReviewView() => InitializeComponent();

    private void JumpDiagnostic_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.JumpToDiagnostic();
    }

    private async void ImportGameLog_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm ||
            TopLevel.GetTopLevel(this) is not { } window) return;
        try
        {
            var sources = await window.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "选择本机 RimWorld Player.log（只在内存中检查，不复制原文）",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("RimWorld log")
                        {
                            Patterns = ["*.log", "*.txt"]
                        }
                    ]
                });
            var path = sources.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) await vm.ImportGameLogAsync(path);
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }

    private async void ExportGameVerification_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm ||
            TopLevel.GetTopLevel(this) is not { } window) return;
        try
        {
            var text = vm.GenerateGameVerificationReport();
            var destination = await window.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "导出 RimWorld 实机验收记录",
                    SuggestedFileName = "MechWright-Game-Verification.md",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("Markdown") { Patterns = ["*.md"] }
                    ],
                    DefaultExtension = "md",
                    ShowOverwritePrompt = true
                });
            var path = destination?.TryGetLocalPath();
            if (path == null) return;
            await AtomicFileWriter.WriteAsync(path, text);
            vm.StatusMessage("实机验收记录已导出；结果需要人工在实际游戏中复核。");
        }
        catch (Exception ex) { vm.ReportError(ex); }
    }
}
