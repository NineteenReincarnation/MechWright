using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Structural editor for RimWorld BodyDef. All changes are transactions in the
/// existing DefDocument, so the graphic tree, inspector, diagnostics and XML export
/// share one document with working undo/redo.
/// </summary>
public static class BodyStructureEditor
{
    public static readonly string[] Heights = ["Top", "Middle", "Bottom"];
    public static readonly string[] Depths = ["Outside", "Inside"];

    public static string AddChild(
        DefDocument document, BodyPartEntry parent, string partDef,
        string relativeCoverage = "0.10", string height = "Middle", string depth = "Outside")
    {
        EnsureAttached(document, parent);
        ValidateDef(partDef);
        var coverage = ParseCoverage(relativeCoverage);
        ValidateLayers(height, depth);

        string insertedPath = "";
        document.Transform(_ =>
        {
            var item = new XElement("li",
                new XElement("def", partDef.Trim()),
                new XElement("coverage", Format(coverage)),
                new XElement("height", height),
                new XElement("depth", depth));
            var children = parent.Xml.Element("parts");
            if (children == null)
            {
                children = new XElement("parts");
                parent.Xml.Add(children);
            }
            children.Add(item);
            insertedPath = DefDocument.GetPath(item);
        });
        return insertedPath;
    }

    public static void Update(
        DefDocument document, BodyPartEntry part,
        string partDef, string relativeCoverage, string height, string depth,
        string customLabel)
    {
        EnsureAttached(document, part);
        ValidateDef(partDef);
        ValidateLayers(height, depth);
        var coverage = part.Depth == 0 ? 1 : ParseCoverage(relativeCoverage);
        document.Transform(_ =>
        {
            part.Xml.SetElementValue("def", partDef.Trim());
            if (part.Depth > 0)
                part.Xml.SetElementValue("coverage", Format(coverage));
            part.Xml.SetElementValue("height", height);
            part.Xml.SetElementValue("depth", depth);
            // Do not add an empty optional label to the output.
            if (!string.IsNullOrWhiteSpace(customLabel))
                part.Xml.SetElementValue("customLabel", customLabel.Trim());
            else part.Xml.Element("customLabel")?.Remove();
        });
    }

    public static string Remove(DefDocument document, BodyPartEntry part)
    {
        EnsureAttached(document, part);
        if (part.Depth == 0)
            throw new InvalidOperationException("Cannot remove the BodyDef corePart. Change its def instead.");
        var list = part.Xml.Parent!;
        var parent = list.Parent ?? throw new InvalidOperationException("BodyDef parent part not found.");
        var parentPath = DefDocument.GetPath(parent);
        document.Transform(_ =>
        {
            part.Xml.Remove();
            // Preserve comments and nonempty containers from imported XML.
            if (!list.Elements().Any() && !list.Nodes().OfType<XComment>().Any())
                list.Remove();
        });
        return parentPath;
    }

    public static string Move(DefDocument document, BodyPartEntry part, int direction)
    {
        EnsureAttached(document, part);
        if (part.Depth == 0)
            throw new InvalidOperationException("The corePart cannot be reordered.");
        if (direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var sibling = direction < 0 ? part.Xml.ElementsBeforeSelf().LastOrDefault()
                                    : part.Xml.ElementsAfterSelf().FirstOrDefault();
        if (sibling == null)
            throw new InvalidOperationException("The selected part is already at that edge.");

        string path = "";
        document.Transform(_ =>
        {
            // LINQ to XML clones attached nodes added elsewhere. Detach first
            // so the original subtree is MOVED, never duplicated.
            part.Xml.Remove();
            if (direction < 0) sibling.AddBeforeSelf(part.Xml);
            else sibling.AddAfterSelf(part.Xml);
            path = DefDocument.GetPath(part.Xml);
        });
        return path;
    }

    private static void EnsureAttached(DefDocument document, BodyPartEntry part)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (part.Xml.Document != document.Xml ||
            part.Xml.Name.LocalName is not ("li" or "corePart") ||
            !part.Xml.Ancestors("BodyDef").Any())
            throw new InvalidOperationException("Select an existing part in a BodyDef before editing.");
    }

    private static void ValidateDef(string partDef)
    {
        if (string.IsNullOrWhiteSpace(partDef) ||
            !Regex.IsMatch(partDef.Trim(), @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
            throw new ArgumentException("BodyPartDef reference must be a valid DefName.", nameof(partDef));
    }

    private static void ValidateLayers(string height, string depth)
    {
        if (!Heights.Contains(height))
            throw new ArgumentException("Height must be Top, Middle or Bottom.", nameof(height));
        if (!Depths.Contains(depth))
            throw new ArgumentException("Depth must be Outside or Inside.", nameof(depth));
    }

    private static double ParseCoverage(string source)
    {
        if (!double.TryParse(source, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value) || value <= 0 || value > 1)
            throw new ArgumentException("Coverage must be a number greater than 0 and at most 1, using '.' as the decimal separator.");
        return value;
    }

    private static string Format(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
}
