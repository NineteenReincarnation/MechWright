using System.Globalization;
using System.Xml.Linq;
using MechWright.Core;

namespace MechWright.RimWorld;

public static class DefValidation
{
    public static IReadOnlyList<Diagnostic> Validate(DefDocument document)
    {
        var diagnostics = new List<Diagnostic>();
        var root = document.Xml.Root;
        if (root?.Name.LocalName == "LanguageData")
            return diagnostics; // Native RimWorld translation XML.
        if (root?.Name.LocalName == "Patch")
            return diagnostics; // Root-level RimWorld Patch XML is a valid project resource.
        if (root?.Name.LocalName != "Defs")
        {
            diagnostics.Add(new Diagnostic("MW001", Severity.Warning,
                "The document is not a standard <Defs> root. It can be viewed but not treated as a Def collection.",
                "/"));
            return diagnostics;
        }

        var entries = document.Definitions().Where(e => e.Type != "Patch").ToList();
        foreach (var group in entries.Where(x => x.DefName != "(unnamed)")
                     .GroupBy(x => (x.Type, x.DefName)).Where(x => x.Count() > 1))
            diagnostics.Add(new Diagnostic("MW002", Severity.Error,
                $"Duplicate {group.Key.Type}: {group.Key.DefName}.", "/Defs"));

        foreach (var entry in entries)
        {
            var location = $"/Defs/{entry.Type}[{entry.Index + 1}]";
            if (entry.DefName == "(unnamed)" && !entry.Abstract &&
                entry.Element.Attribute("Name") == null)
                diagnostics.Add(new Diagnostic("MW003", Severity.Warning,
                    $"Missing defName in {entry.Type}.", location));

            foreach (var field in document.Fields(entry))
            {
                var issue = StandardFieldRules.Validate(field.Name, field.Value);
                if (issue != null)
                    diagnostics.Add(new Diagnostic("MW010", Severity.Error, issue, field.Path));
            }

            if (entry.Type != "BodyDef") continue;
            var core = entry.Element.Element("corePart");
            if (core == null)
            {
                diagnostics.Add(new Diagnostic("MW004", Severity.Error,
                    "BodyDef requires a corePart.", location));
                continue;
            }
            InspectParts(core, location + "/corePart", diagnostics);
        }

        return diagnostics;
    }

    private static void InspectParts(XElement parent, string path, List<Diagnostic> errors)
    {
        var children = parent.Element("parts")?.Elements("li").ToList() ?? new List<XElement>();
        double sum = 0;
        foreach (var (child, i) in children.Select((e, i) => (e, i)))
        {
            var childPath = path + $"/parts/li[{i + 1}]";
            var coverage = child.Element("coverage");
            if (coverage != null)
            {
                if (!double.TryParse(coverage.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var value) ||
                    !double.IsFinite(value) || value < 0 || value > 1)
                    errors.Add(new Diagnostic("MW005", Severity.Error,
                        "coverage must be a finite number between 0 and 1.", childPath + "/coverage"));
                else sum += value;
            }
            else sum += 1; // RimWorld defaults to full relative coverage if the value is omitted.
            if (child.Element("def") == null)
                errors.Add(new Diagnostic("MW006", Severity.Error,
                    "Body part is missing its def reference.", childPath));
            InspectParts(child, childPath, errors);
        }
        if (sum > 1.000001)
            errors.Add(new Diagnostic("MW007", Severity.Warning,
                $"Child coverage weights total {sum:0.###}; check the intended hit distribution.",
                path + "/parts"));
    }
}
