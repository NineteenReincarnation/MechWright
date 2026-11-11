using System.Globalization;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>Graphic descriptors reflect explicit XML; no inheritance or Unity
/// graphic-class semantics are invented when metadata is absent.</summary>
public static class GraphicReferences
{
    public sealed record GraphicDescriptor(
        string XmlPath, string TexturePath, string GraphicClass,
        string DrawSize, bool ExplicitGraphicClass);

    public static IReadOnlyList<GraphicDescriptor> Inspect(DefDocument document, DefEntry definition)
    {
        if (definition.Element.Document != document.Xml)
            throw new InvalidOperationException("Definition is not from this document.");

        return definition.Element.DescendantsAndSelf()
            .Where(n => n.Name.LocalName is "graphicData" or "bodyGraphicData")
            .Select(n => new
            {
                Node = n,
                Texture = n.Element("texPath")?.Value?.Trim(),
                Class = n.Element("graphicClass")?.Value?.Trim(),
                Size = n.Element("drawSize")?.Value?.Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Texture))
            .Select(x => new GraphicDescriptor(
                DefDocument.GetPath(x.Node),
                x.Texture!,
                x.Class ?? "(未显式指定)",
                x.Size ?? "(未显式指定)",
                x.Class != null))
            .ToList();
    }
}
