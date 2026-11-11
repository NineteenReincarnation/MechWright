using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<NativeListGroupRow> NativeListGroups { get; } = new();
    public ObservableCollection<NativeListItemRow> NativeListEntries { get; } = new();
    private NativeListGroupRow? _selectedListGroup;
    private NativeListItemRow? _selectedListEntry;
    private string _listXml = "<li>\n</li>";
    private string _listHelp = "选择列表。示例列表项不会自动写入 XML。";

    public NativeListGroupRow? SelectedListGroup
    {
        get => _selectedListGroup;
        set
        {
            if (ReferenceEquals(_selectedListGroup, value)) return;
            _selectedListGroup = value;
            Notify();
            RefreshNativeListItems();
        }
    }

    public NativeListItemRow? SelectedListEntry
    {
        get => _selectedListEntry;
        set
        {
            _selectedListEntry = value;
            Notify();
            Notify(nameof(HasSelectedListEntry));
            ListXml = value?.Item.Source ?? "<li>\n</li>";
        }
    }

    public bool HasSelectedListEntry => SelectedListEntry != null;
    public bool HasListGroup => SelectedListGroup != null;

    public string ListXml
    {
        get => _listXml;
        set { _listXml = value; Notify(); }
    }
    public string ListHelp
    {
        get => _listHelp;
        private set { _listHelp = value; Notify(); }
    }

    private void RefreshNativeListEditor()
    {
        NativeListGroups.Clear();
        NativeListEntries.Clear();
        _selectedListGroup = null;
        _selectedListEntry = null;
        Notify(nameof(SelectedListGroup));
        Notify(nameof(SelectedListEntry));
        Notify(nameof(HasListGroup));
        Notify(nameof(HasSelectedListEntry));
        ListXml = "<li>\n</li>";
        if (_document == null || SelectedDefinition == null)
        {
            ListHelp = "先选择 ThingDef / PawnKindDef 等需要编辑的定义。";
            return;
        }
        try
        {
            foreach (var group in NativeListEditor.Lists(_document, SelectedDefinition.Entry))
                NativeListGroups.Add(new NativeListGroupRow(group));
            SelectedListGroup = NativeListGroups.FirstOrDefault(x => x.Group.Exists &&
                x.Group.ItemCount > 0) ?? NativeListGroups.FirstOrDefault();
        }
        catch (Exception ex) { ListHelp = "无法读取此列表：" + ex.Message; }
    }

    private void RefreshNativeListItems(string? path = null)
    {
        NativeListEntries.Clear();
        if (_document == null || SelectedDefinition == null || SelectedListGroup == null)
        {
            SelectedListEntry = null;
            return;
        }
        try
        {
            foreach (var item in NativeListEditor.Items(_document,
                         SelectedDefinition.Entry, SelectedListGroup.Group.Container))
                NativeListEntries.Add(new NativeListItemRow(item));
            SelectedListEntry = NativeListEntries.FirstOrDefault(x => x.Item.XmlPath == path)
                ?? NativeListEntries.FirstOrDefault();
            ListHelp = $"{SelectedListGroup.Group.Container} · {NativeListEntries.Count} 项 " +
                "· 修改的是完整 <li> 节点，其他字段、注释和属性会保留；操作可撤销。";
        }
        catch (Exception ex)
        {
            SelectedListEntry = null;
            ListHelp = "复杂结构不能直接按 li 列表编辑：" + ex.Message;
        }
    }

    private void RefreshAfterListChange(int defIndex, string group, string? itemPath)
    {
        RefreshAll(defIndex);
        SelectedListGroup = NativeListGroups.FirstOrDefault(x => x.Group.Container == group);
        RefreshNativeListItems(itemPath);
    }

    public void AddNativeListEntry() => PerformListEdit(
        (doc, def, group, path) => NativeListEditor.Add(doc, def, group, ListXml));

    public void ReplaceNativeListEntry() => PerformListEdit(
        (doc, def, group, path) => NativeListEditor.Replace(doc, def, group,
            path ?? throw new InvalidOperationException("请选择要替换的列表项。"), ListXml));

    public void DuplicateNativeListEntry() => PerformListEdit(
        (doc, def, group, path) => NativeListEditor.Duplicate(doc, def, group,
            path ?? throw new InvalidOperationException("请选择要复制的列表项。")));

    public void MoveNativeListEntry(int direction) => PerformListEdit(
        (doc, def, group, path) => NativeListEditor.Move(doc, def, group,
            path ?? throw new InvalidOperationException("请选择要移动的列表项。"), direction));

    public void RemoveNativeListEntry() => PerformListEdit(
        (doc, def, group, path) =>
        {
            NativeListEditor.Remove(doc, def, group,
                path ?? throw new InvalidOperationException("请选择要删除的列表项。"));
            return null;
        });

    private void PerformListEdit(
        Func<DefDocument, DefEntry, string, string?, string?> action)
    {
        if (_document == null || SelectedDefinition == null || SelectedListGroup == null)
        {
            Status = "请选择一个 XML 定义和列表。";
            return;
        }
        var index = SelectedDefinition.Entry.Index;
        var group = SelectedListGroup.Group.Container;
        var path = SelectedListEntry?.Item.XmlPath;
        try
        {
            var updatedPath = action(_document, SelectedDefinition.Entry, group, path);
            RefreshAfterListChange(index, group, updatedPath);
            Status = $"已更新列表 {group} · 完整 XML 结构保留 · Ctrl+Z 可撤销";
        }
        catch (Exception ex) { ReportError(ex); }
    }
}

public sealed record NativeListGroupRow(NativeListEditor.ListInfo Group)
{
    public string Display => Group.Display;
}
public sealed record NativeListItemRow(NativeListEditor.ItemInfo Item)
{
    public string Display => Item.Display;
}
