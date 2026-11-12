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

                // Main entry: blank XML opens a usable list without a mech wizard.
                vm.NewFile();
                Require(vm.IsDocumentMode && vm.HasDocument &&
                    !vm.HasWorkspace && vm.Definitions.Count == 0,
                    "New XML must open an empty file-first workspace.");
                Require(vm.DocumentIsEmpty && vm.DocumentVisibleNodes.Count == 0,
                    "A blank XML should show the first-content empty state.");
                vm.BeginDocumentConfiguration();
                Require(vm.DocumentHasDefinition && vm.DocumentQuickPanelOpen &&
                    vm.DocumentQuickOptions.Count > 0,
                    "New XML cannot enter a usable field list in one action.");
                vm.ToggleDocumentQuickPanel();
                Require(vm.DocumentHasNoFields,
                    "Starting the editor must not silently write values.");
                Require(vm.DocumentHasDefinition &&
                    vm.SelectedDefinition?.Entry.DefName.StartsWith("MP_", StringComparison.Ordinal) == true,
                    "One-click content should create only a minimal native definition.");
                vm.DocumentLabelDraft = "测试机体";
                vm.SaveDocumentDisplayName();
                Require(vm.SelectedDefinition?.Entry.Label == "测试机体",
                    "Friendly name was not written to the XML.");
                vm.ToggleDocumentQuickPanel();
                Require(vm.DocumentQuickOptions.Any(x => x.Path == "statBases/MoveSpeed"),
                    "The novice add menu did not expose a basic field.");
                vm.SelectedDocumentQuickOption = vm.DocumentQuickOptions.First(x =>
                    x.Path == "statBases/MoveSpeed");
                Require(!vm.DocumentCanAddQuickOption,
                    "Numeric values cannot be written before explicit user input.");
                vm.DocumentQuickNumber = 4.8m;
                Require(vm.DocumentCanAddQuickOption, "Explicit numeric input was not accepted.");
                vm.ApplyDocumentQuickOption();
                Require(vm.DocumentQuickPanelOpen,
                    "Add picker closed after first field.");
                Require(vm.XmlPreview.Contains("<MoveSpeed>4.8</MoveSpeed>") &&
                    vm.DocumentVisibleNodes.Any(x => x.Name == "MoveSpeed"),
                    "Inline novice add did not create a visible, real XML field.");
                vm.Undo();
                Require(!vm.XmlPreview.Contains("<MoveSpeed>4.8</MoveSpeed>"),
                    "Quick-add undo did not roll back the entire field.");
                vm.NewFile();
                Require(!vm.DocumentQuickPanelOpen,
                    "New XML must reset the previous file editing panel.");
                vm.DocumentDefName = "MP_UiFile";
                vm.DocumentDefLabel = "Simple part";
                vm.AppendDocumentDefinition();
                Require(vm.Definitions.Count == 1 &&
                    vm.SelectedDefinition?.Entry.DefName == "MP_UiFile",
                    "Inline definition row did not create the selected XML Def.");
                vm.DocumentSelectedNode = vm.DocumentNodes.First(x => x.IsRoot);
                vm.DocumentSelectedField = vm.DocumentFieldChoices.First(x => x.Name == "statBases");
                vm.AddDocumentField();
                Require(vm.DocumentNodes.Any(x => x.Name == "statBases"),
                    "Blank document could not create a statBases group.");
                vm.DocumentSelectedNode = vm.DocumentNodes.First(x => x.Name == "statBases");
                vm.DocumentSelectedField = vm.DocumentFieldChoices.First(x => x.Name == "MoveSpeed");
                vm.DocumentNewValue = "4.7";
                vm.AddDocumentField();
                var docMove = vm.DocumentNodes.First(x => x.Name == "MoveSpeed");
                docMove.Number = 5.2m;
                vm.SaveDocumentNode(docMove);
                Require(vm.XmlPreview.Contains("<MoveSpeed>5.2</MoveSpeed>"),
                    "Row changes were not written to the current XML.");
                vm.Undo();
                Require(vm.XmlPreview.Contains("<MoveSpeed>4.7</MoveSpeed>"),
                    "Undo could not restore a file-first row edit.");
                vm.Redo();
                Require(vm.XmlPreview.Contains("<MoveSpeed>5.2</MoveSpeed>"),
                    "Redo could not restore a file-first row edit.");
                var expandedGroup = vm.DocumentNodes.First(x => x.Name == "statBases");
                Require(vm.DocumentVisibleNodes.Any(x => x.Name == "MoveSpeed"),
                    "Expanded Inspector group must show its fields.");
                vm.ToggleDocumentGroup(expandedGroup);
                Require(vm.DocumentVisibleNodes.All(x => x.Name != "MoveSpeed"),
                    "Collapsing a group should hide its child without removing XML.");
                vm.DocumentFieldSearch = "MoveSpeed";
                Require(vm.DocumentVisibleNodes.Any(x => x.Name == "MoveSpeed"),
                    "Inspector search must find a field within collapsed groups.");
                vm.DocumentFieldSearch = "";
                Require(vm.XmlPreview.Contains("<MoveSpeed>5.2</MoveSpeed>"),
                    "Inspector group collapse must never mutate XML.");

                vm.CreateWorkspace();
                Require(vm.IsDocumentMode && vm.NeedsFirstMechanoid,
                    "Empty project must open its file editor without launching a wizard.");
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
                Require(vm.WorksheetFields.Count >= 10,
                    "The vertical field worksheet did not show standard rows.");
                var cp=vm.WorksheetFields.First(x=>x.Spec.Path=="combatPower");
                cp.Number=437;
                vm.SaveWorksheetField(cp);
                Require(VisualContentEditor.Read(project,"MP_UiSmoke",cp.Spec)=="437",
                    "Editing a worksheet field did not save into native PawnKindDef.");

                // Search must never discard unrelated unsaved row edits.
                var mass=vm.WorksheetFields.First(x=>x.Spec.Path=="statBases/Mass");
                var massBefore=VisualContentEditor.Read(project,"MP_UiSmoke",mass.Spec);
                mass.Number=123;
                vm.WorksheetSearch="combatPower";
                Require(vm.WorksheetFields.All(x=>x.Spec.Path!="statBases/Mass"),
                    "Worksheet search did not filter the visible field rows.");
                vm.WorksheetSearch="";
                Require(vm.WorksheetFields.First(x=>x.Spec.Path=="statBases/Mass").Number==123 &&
                    VisualContentEditor.Read(project,"MP_UiSmoke",mass.Spec)==massBefore,
                    "Filtering discarded an unsaved field edit or wrote it to XML.");

                // The primary page adds fields in place. A draft must not
                // modify XML before Save, and removing a draft must be safe.
                vm.ToggleWorksheetCatalog();
                Require(vm.WorksheetCatalogOpen, "Inline field catalog did not open.");
                var vacuumField=new VisualContentEditor.Option("ThingDef",
                    "race/canFlyInVacuum","真空飞行","其他原生属性",
                    "Boolean","false","原生可选属性","UI smoke",true);
                vm.AddWorksheetCatalogField(new WorksheetCatalogRow(vacuumField));
                var pending=vm.WorksheetFields.First(x=>
                    x.Spec.Path=="race/canFlyInVacuum");
                Require(pending.IsPinned &&
                    VisualContentEditor.Read(project,"MP_UiSmoke",vacuumField)==null,
                    "An unsaved worksheet draft modified native XML.");
                pending.Boolean=true;
                vm.SaveWorksheetField(pending);
                Require(VisualContentEditor.Read(project,"MP_UiSmoke",vacuumField)=="true" &&
                    vm.WorksheetFields.Any(x=>
                        x.Spec.Path=="race/canFlyInVacuum" && !x.IsPinned),
                    "Saving an inline field did not persist it as a regular worksheet row.");

                var abandoned=new VisualContentEditor.Option("ThingDef",
                    "statBases/Flammability","易燃性","基础性能",
                    "Number","0.5","原生可选属性","UI smoke",false);
                vm.AddWorksheetCatalogField(new WorksheetCatalogRow(abandoned));
                var draft=vm.WorksheetFields.First(x=>
                    x.Spec.Path=="statBases/Flammability");
                vm.DiscardWorksheetDraft(draft);
                Require(VisualContentEditor.Read(project,"MP_UiSmoke",abandoned)==null &&
                    vm.WorksheetFields.All(x=>x.Spec.Path!="statBases/Flammability"),
                    "Removing an unsaved draft should leave the native XML unchanged.");

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
                vm.SelectedFreeformTarget=vm.FreeformTargets[0];
                vm.FreeformContainer="tools";
                vm.AddFreeformModule();
                Require(vm.FreeformModules.Count>=1,
                    "Freeform list editor did not create a new item.");
                vm.SelectedFreeformNode=vm.FreeformNodes.First();
                vm.FreeformName="power";
                vm.FreeformValue="19";
                vm.AddFreeformField();
                Require(project.FindDefinitionFile("ThingDef","MP_UiSmoke")!
                    .Document.Export().Contains("<power>19</power>"),
                    "Freeform native field was not added.");
                vm.FreeformDefType="BodyPartDef";
                vm.FreeformNewDefName="MP_SmokeWing";
                vm.FreeformNewDefLabel="Smoke wing";
                vm.CreateFreeformDefinition();
                Require(project.FindDefinitionFile("BodyPartDef","MP_SmokeWing")!=null,
                    "Blank custom BodyPartDef was not created.");
                vm.WorkflowStep = 3;
                Require(vm.GestationIngredients.Count >= 1,
                    "Visual gestation material editor did not initialize.");
                vm.SaveVisualGestation();
                Require(project.FindDefinitionFile("RecipeDef","MP_UiSmoke_Gestate")!=null,
                    "Visual gestation form did not produce a linked RecipeDef.");
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
                Require(vm.VisualReviewChecks.Count == 6 &&
                    vm.VisualReviewChecks.Any(x=>x.Title=="机械培育" && x.Ready),
                    "Novice review did not display and recognize gestation.");
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
