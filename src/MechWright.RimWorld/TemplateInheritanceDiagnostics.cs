using MechWright.Core;

namespace MechWright.RimWorld;

/// <summary>
/// Inspects only explicitly declared XML ParentName relationships. The local
/// project and a scanned installed-game template index are evidence sources;
/// no PatchOperation, MayRequire or load-order behavior is simulated.
/// </summary>
public static class TemplateInheritanceDiagnostics
{
    private sealed record TemplateSource(string FilePath, string DefType,
        string Name, string ParentName, string Path, bool Conditional);

    public static IReadOnlyList<WorkspaceDiagnostic> Inspect(
        IReadOnlyList<WorkspaceFile> files,
        IReadOnlyCollection<DefCatalog.ParentTemplate>? installed = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        var entries = files.SelectMany(file => file.Document.Definitions()
            .Where(def => def.Type != "Patch")
            .Select(def => new TemplateSource(
                file.RelativePath,
                def.Type,
                (def.Element.Attribute("Name")?.Value ?? "").Trim(),
                (def.Element.Attribute("ParentName")?.Value ?? "").Trim(),
                DefDocument.GetPath(def.Element),
                def.Element.Attribute("MayRequire") != null)))
            .ToArray();

        var local = entries.Where(x => x.Name.Length > 0)
            .GroupBy(x => (x.DefType, x.Name))
            .ToDictionary(g => g.Key, g => g.ToArray());
        var game = installed?.Select(x => (x.DefType, x.Name)).ToHashSet();
        var warnings = new List<WorkspaceDiagnostic>();

        foreach (var match in local.Where(x => x.Value.Length > 1))
            foreach (var source in match.Value)
                warnings.Add(new WorkspaceDiagnostic(source.FilePath,
                    new Diagnostic("MW031", Severity.Warning,
                        $"Duplicate parent template {match.Key.DefType}/{match.Key.Name} " +
                        "in this project; inheritance resolution depends on loading behavior.",
                        source.Path)));

        foreach (var entry in entries.Where(x => x.ParentName.Length > 0))
        {
            var key = (entry.DefType, entry.ParentName);
            if (!local.ContainsKey(key) && game != null && !game.Contains(key) &&
                !entry.Conditional)
                warnings.Add(new WorkspaceDiagnostic(entry.FilePath,
                    new Diagnostic("MW030", Severity.Warning,
                        $"ParentName '{entry.ParentName}' for {entry.DefType} was not " +
                        "found in this project or the scanned game XML. It may be " +
                        "provided by another Mod or conditional patch.",
                        entry.Path)));

            // No guess about a parent outside this project: a single local
            // chain is the only case where cycles can be demonstrated here.
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = entry;
            for (var depth = 0; depth < 64 && current.ParentName.Length > 0; depth++)
            {
                if (!visited.Add(current.ParentName))
                {
                    warnings.Add(new WorkspaceDiagnostic(entry.FilePath,
                        new Diagnostic("MW032", Severity.Warning,
                            $"Local ParentName cycle detected for {entry.DefType}: " +
                            $"{entry.ParentName}. Verify the inheritance chain before game load.",
                            entry.Path)));
                    break;
                }
                if (!local.TryGetValue((entry.DefType, current.ParentName), out var matches) ||
                    matches.Length != 1) break;
                current = matches[0];
            }
        }
        return warnings;
    }
}
