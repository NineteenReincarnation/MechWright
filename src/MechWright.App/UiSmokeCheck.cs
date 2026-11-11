using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MechWright.App.ViewModels;
using MechWright.RimWorld;

namespace MechWright.App;

/// <summary>
/// CI-only interactive Avalonia smoke check: exercise the actual compiled
/// window and project stages without needing an installed RimWorld copy.
/// </summary>
internal static class UiSmokeCheck
{
    public static void Attach(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!Environment.GetCommandLineArgs().Contains(
                "--ui-smoke-test", StringComparer.Ordinal)) return;

        var window = desktop.MainWindow ??
            throw new InvalidOperationException("MainWindow was not created.");
        window.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            var result = 0;
            try
            {
                var vm = window.DataContext as MainWindowViewModel ??
                    throw new InvalidOperationException("MainWindow has no editor ViewModel.");
                Require(vm.ShowStartPage, "New project page should be the initial view.");
                vm.CreateWorkspace();
                Require(vm.NeedsFirstMechanoid, "Empty workspace must guide the user.");
                vm.GenerateMechanoid(new MechanoidBlueprint(
                    "MP_UiSmoke", "UI smoke test mechanoid"));
                Require(vm.ShowWorkspacePage && !vm.NeedsFirstMechanoid,
                    "Generating a mechanoid should enter the editor.");

                vm.WorkflowStep = 0;
                Require(vm.SelectedDefinition?.Entry.Type == "ThingDef",
                    "Basic stage did not select ThingDef.");
                vm.SelectedChassisCard = vm.ChassisCards.Single(x => x.Preset.Key == "assault");
                vm.ApplyVisualBasic();
                var project = vm.Workspace ??
                    throw new InvalidOperationException("Workspace disappeared.");
                Require(VisualMechDesigner.CurrentString(project, "PawnKindDef",
                    "MP_UiSmoke", "combatPower") == "420",
                    "Visual chassis preset did not update the native PawnKindDef.");
                vm.WorkflowStep = 1;
                Require(vm.SelectedDefinition?.Entry.Type == "BodyDef" &&
                    vm.BodyParts.Count > 0, "Body stage did not select the linked BodyDef.");
                vm.WorkflowStep = 2;
                Require(vm.SelectedDefinition?.Entry.Type == "ThingDef",
                    "Weapon stage did not restore the current mechanoid ThingDef.");
                vm.SelectedMeleeCard = vm.MeleeCards.First();
                vm.AddVisualMelee();
                Require(vm.InstalledMelee.Count == 1,
                    "Clicking a melee preset did not create native tool configuration.");
                vm.WorkflowStep = 3;
                // Exercise user-facing generic content editor, not XML text entry.
                vm.OpenContentEditor("机械师与生成");
                var spawn=vm.ContentFields.First(x=>x.Spec.Path=="maxPerGroup");
                vm.SelectedContentField=spawn;
                vm.ContentNumber=4;
                vm.ApplySelectedContentField();
                Require(VisualContentEditor.Read(project,"MP_UiSmoke",spawn.Spec)=="4",
                    "Simple visual content editor did not add spawn configuration.");
                vm.CloseContentEditor();
                _ = new VisualContentWindow(vm);
                vm.CloseContentEditor();
                vm.WorkflowStep = 4;
                Require(vm.IsReviewStage, "Review stage is inaccessible.");
                Require(vm.VisualReviewChecks.Count == 5,
                    "Novice review did not display the five visual checks.");
                vm.WorkflowStep = 5;
                Require(vm.IsAdvancedStage, "Advanced editing remains inaccessible.");

                _ = new MechanoidWizardWindow();
                _ = new VanillaLibraryWindow { DataContext = vm };
            }
            catch (Exception ex)
            {
                result = 1;
                System.Diagnostics.Trace.WriteLine("MechWright UI smoke failure: " + ex);
            }
            finally { desktop.Shutdown(result); }
        }, DispatcherPriority.Background);
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
