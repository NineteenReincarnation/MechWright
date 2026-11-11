using System.Collections.ObjectModel;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<AttributeRow> XmlAttributes { get; } = new();
    private AttributeRow? _selectedXmlAttribute;
    private string _xmlAttributeValue = "";
    private string _newXmlAttributeName = "ParentName";
    private string _newXmlAttributeValue = "";

    public AttributeRow? SelectedXmlAttribute
    {
        get => _selectedXmlAttribute;
        set
        {
            _selectedXmlAttribute = value;
            Notify();
            XmlAttributeValue = value?.Attribute.Value ?? "";
        }
    }

    public string XmlAttributeValue
    {
        get => _xmlAttributeValue;
        set { _xmlAttributeValue = value; Notify(); }
    }
    public string NewXmlAttributeName
    {
        get => _newXmlAttributeName;
        set { _newXmlAttributeName = value; Notify(); }
    }
    public string NewXmlAttributeValue
    {
        get => _newXmlAttributeValue;
        set { _newXmlAttributeValue = value; Notify(); }
    }

    private void RefreshXmlAttributes()
    {
        XmlAttributes.Clear();
        if (_document != null && SelectedDefinition != null)
            foreach (var attribute in _document.Attributes(SelectedDefinition.Entry))
                XmlAttributes.Add(new AttributeRow(attribute));
        SelectedXmlAttribute = XmlAttributes.FirstOrDefault();
    }

    public void ApplyXmlAttribute()
    {
        if (_document == null || SelectedDefinition == null || SelectedXmlAttribute == null) return;
        var index = SelectedDefinition.Entry.Index;
        var path = SelectedXmlAttribute.Attribute.Path;
        try
        {
            _document.SetAttribute(SelectedXmlAttribute.Attribute, XmlAttributeValue);
            RefreshAll(index);
            SelectedXmlAttribute = XmlAttributes.FirstOrDefault(x => x.Attribute.Path == path);
            Status = $"已修改 XML 属性 {path}，可以撤销";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void RemoveXmlAttribute()
    {
        if (_document == null || SelectedDefinition == null || SelectedXmlAttribute == null) return;
        var index = SelectedDefinition.Entry.Index;
        try
        {
            _document.RemoveAttribute(SelectedXmlAttribute.Attribute);
            RefreshAll(index);
            Status = "XML 属性已移除（原节点内容及其他属性不变）";
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void AddXmlAttribute()
    {
        if (_document == null || SelectedDefinition == null) return;
        var index = SelectedDefinition.Entry.Index;
        try
        {
            _document.AddDefinitionAttribute(SelectedDefinition.Entry,
                NewXmlAttributeName.Trim(), NewXmlAttributeValue);
            RefreshAll(index);
            SelectedXmlAttribute = XmlAttributes.FirstOrDefault(x =>
                x.Attribute.Name == NewXmlAttributeName.Trim() &&
                x.Attribute.OwnerPath.EndsWith("/" + SelectedDefinition!.Entry.Type + "[" + (SelectedDefinition.Entry.Index + 1) + "]",
                    StringComparison.Ordinal));
            Status = "已为当前 Def 添加属性；可撤销";
        }
        catch (Exception ex) { ReportError(ex); }
    }
}

public sealed class AttributeRow(DefAttribute attribute)
{
    public DefAttribute Attribute { get; } = attribute;
    public string Display => Attribute.DisplayName;
}
