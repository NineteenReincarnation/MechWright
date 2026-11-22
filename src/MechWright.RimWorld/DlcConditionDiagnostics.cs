using System.Xml.Linq;
using MechWright.Core;

namespace MechWright.RimWorld;

/// <summary>
/// Observes optional DLC XML gates against packages present on disk in the
/// user's installation. Presence is not equivalent to enabling a DLC at runtime.
/// Unknown third-party package ids are never classified as missing.
/// </summary>
public static class DlcConditionDiagnostics
{
    private static readonly IReadOnlyDictionary<string, string> KnownDlc =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ludeon.RimWorld.Royalty"] = "Royalty",
            ["Ludeon.RimWorld.Ideology"] = "Ideology",
            ["Ludeon.RimWorld.Biotech"] = "Biotech",
            ["Ludeon.RimWorld.Anomaly"] = "Anomaly",
            ["Ludeon.RimWorld.Odyssey"] = "Odyssey"
        };

    public static IReadOnlyList<WorkspaceDiagnostic> Inspect(
        IReadOnlyList<WorkspaceFile> files,
        IReadOnlyCollection<DefCatalog.PackageScan>? packages)
    {
        if (packages == null) return [];
        var installed = packages.Where(x => x.Present)
            .Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<WorkspaceDiagnostic>();
        var emitted = new HashSet<(string File, string Package)>();

        foreach (var file in files)
        {
            var root = file.Document.Xml.Root;
            if (root == null) continue;
            foreach (var node in root.DescendantsAndSelf())
            {
                foreach (var attribute in node.Attributes().Where(x =>
                             x.Name.LocalName is "MayRequire" or "MayRequireAnyOf"))
                {
                    var packagesInGate = attribute.Value.Split(',',
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    var known = packagesInGate.Where(x => KnownDlc.ContainsKey(x))
                        .Select(x => KnownDlc[x]).Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (known.Length == 0) continue;

                    // "AnyOf" is satisfied by any possible installed DLC,
                    // but may also be satisfied by an unrelated third-party
                    // package. Do not assert absence if an unknown candidate
                    // could enable the content.
                    if (attribute.Name.LocalName == "MayRequireAnyOf" &&
                        (known.Any(installed.Contains) ||
                         packagesInGate.Length != known.Length))
                        continue;

                    foreach (var package in known.Where(x => !installed.Contains(x)))
                    {
                        if (!emitted.Add((file.RelativePath, package))) continue;
                        diagnostics.Add(new WorkspaceDiagnostic(file.RelativePath,
                            new Diagnostic("MW040", Severity.Information,
                                $"XML has a {attribute.Name.LocalName} gate for " +
                                $"{package}, but that DLC's definition folder was not " +
                                "found during the local scan. This content may be " +
                                "skipped; installed files do not prove DLC is enabled.",
                                DefDocument.GetPath(node))));
                    }
                }
            }
        }
        return diagnostics;
    }
}
