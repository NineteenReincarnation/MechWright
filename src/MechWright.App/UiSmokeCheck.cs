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

                // No creation wizard, field picker, or mandatory presets.
                // One XML immediately opens a continuous list of empty inputs.
                vm.NewFile();
                Require(vm.WorkbenchArea == 0 && vm.PrimarySaveLabel == "保存 XML",
                    "New XML must open the single editable workbench.");
                Require(vm.IsDocumentMode && vm.HasDocument && !vm.HasWorkspace &&
                    vm.Definitions.Count == 1 && vm.DocumentHasDefinition,
                    "New XML should open a minimal editable definition immediately.");
                Require(vm.SelectedDefinition?.Entry.DefName == "MP_Thing001" &&
                    vm.InlineInspectorRows.Count >= 12 && !vm.DocumentQuickPanelOpen,
                    "The first screen must be a full list, not another add-field panel.");
                Require(vm.InlineInspectorRows.Any(x => x.Path == "statBases/MoveSpeed") &&
                    !vm.XmlPreview.Contains("<MoveSpeed>"),
                    "Optional inputs must be visible without polluting the XML.");
                vm.DocumentLabelDraft = "测试机体";
                Require(vm.CommitDocumentInputs() &&
                    vm.SelectedDefinition?.Entry.Label == "测试机体",
                    "Editing the display name should commit without a separate wizard.");
                var firstMove = vm.InlineInspectorRows.Single(x =>
                    x.Path == "statBases/MoveSpeed");
                firstMove.Number = 4.8m;
                Require(vm.HasUncommittedDocumentInputs &&
                    !vm.XmlPreview.Contains("<MoveSpeed>"),
                    "Typing a number should not write XML before the cell is committed.");
                Require(vm.CommitInlineInspector(firstMove) &&
                    vm.XmlPreview.Contains("<MoveSpeed>4.8</MoveSpeed>"),
                    "A list cell must write the correct XML path directly.");
                Require(vm.InlineInspectorRows.Any(x =>
                    x.Path == "statBases/Mass" && !x.IsSet),
                    "Editing one cell must leave other optional inputs blank.");
                vm.InlineSearch = "ArmorRating_Sharp";
                Require(vm.InlineInspectorRows.Count == 1 &&
                    vm.InlineInspectorRows[0].Path == "statBases/ArmorRating_Sharp",
                    "Typing a path should filter the same inline list, not open a picker.");
                vm.InlineSearch = "";
                vm.Undo();
                Require(!vm.XmlPreview.Contains("<MoveSpeed>4.8</MoveSpeed>"),
                    "Inline field addition must be undoable.");
                vm.NewFile();
                Require(vm.InlineInspectorRows.Count >= 12 &&
                    !vm.DocumentQuickPanelOpen && vm.InlineSearch == "",
                    "A second XML must again open directly in its field list.");
                vm.DocumentDefName = "MP_UiFile";
                vm.DocumentDefLabel = "Simple part";
                vm.AppendDocumentDefinition();
                Require(vm.Definitions.Count == 2 &&
                    vm.SelectedDefinition?.Entry.DefName == "MP_UiFile",
                    "Inline definition row did not create the selected XML Def.");
                var selectedBeforeFilter = vm.SelectedDefinition;
                vm.SearchQuery = "not-an-existing-definition";
                Require(vm.VisibleDefinitions.Count == 0 &&
                    ReferenceEquals(vm.SelectedDefinition, selectedBeforeFilter),
                    "Search must not silently select or close a different definition.");
                vm.SearchQuery = "";
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

                vm.CreateQuickDefinition("BodyPartDef");
                var second = vm.SelectedDefinition!;
                vm.SelectedDefinition = vm.Definitions.Single(x =>
                    x.Entry.DefName == "MP_UiFile");
                var uncommittedMove = vm.DocumentNodes.Single(x => x.Name == "MoveSpeed");
                uncommittedMove.Number = 6.25m;
                Require(vm.HasUncommittedDocumentInputs,
                    "Changing a row must be recognized as pending input.");
                vm.SelectedDefinition = second;
                Require(vm.XmlPreview.Contains("<MoveSpeed>6.25</MoveSpeed>"),
                    "Changing definitions discarded unfinished input.");

                vm.CreateWorkspace();
                Require(vm.PrimarySaveLabel == "导出整个 Mod",
                    "Project toolbar must not imply that saving one XML saves a Mod.");
                Require(vm.IsDocumentMode && vm.NeedsFirstMechanoid &&
                    vm.WorkbenchArea == 0 && !vm.CanUseFullWorkbench,
                    "An empty Mod project should start in the XML workbench.");
                vm.OpenWorkbenchArea(4);
                Require(vm.WorkbenchArea == 0,
                    "A project without a mechanoid must not enter combat tools.");
                vm.GenerateMechanoid(new MechanoidBlueprint(
                    "MP_UiSmoke", "UI smoke test mechanoid"));
                Require(vm.ShowWorkspacePage && !vm.NeedsFirstMechanoid &&
                    vm.CanUseFullWorkbench,
                    "Generating a mechanoid should unlock the full workbench.");

                vm.OpenWorkbenchArea(3);
                Require(vm.WorkbenchArea == 3 && vm.WorkflowStep == 1 &&
                    vm.IsGuidedMode, "Graphics needs one-click global navigation.");
                vm.OpenWorkbenchArea(4);
                Require(vm.WorkbenchArea == 4 && vm.WorkflowStep == 2,
                    "Combat must be directly accessible from another tool.");
                vm.OpenWorkbenchArea(0);
                Require(vm.IsDocumentMode && vm.WorkbenchArea == 0,
                    "The global workbench could not return to XML configuration.");
                vm.OpenWorkbenchArea(1);
                Require(vm.WorkbenchArea == 1 && vm.IsGuidedMode,
                    "Returning to a visual tool required an extra wizard.");

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
                try
                {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory,
                        "ui-smoke-error.txt"), ex.ToString());
                }
                catch (IOException) { }
            }
            finally { desktop.Shutdown(result); }
        }, DispatcherPriority.Background);
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
