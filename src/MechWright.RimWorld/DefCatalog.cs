using System.Xml;
using System.Xml.Linq;
using MechWright.Core;

namespace MechWright.RimWorld;

/// <summary>
/// Reads XML from the user's installed game. It never writes into or bundles
/// RimWorld files. One parse pass populates both the generic Def index and
/// the read-only vanilla mechanoid reference library.
/// </summary>
public static class DefCatalog
{
    private static readonly string[] Packages =
        ["Core", "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey"];
    public static IReadOnlyList<string> SupportedPackages => Packages;

    public sealed record PackageScan(string Name, bool Present, int DefinitionCount);
    public sealed record CatalogScan(
        IReadOnlyList<CatalogEntry> Entries,
        IReadOnlyList<PackageScan> Packages,
        VanillaMechanoidLibrary Mechanoids,
        bool IsGameInstallation,
        IReadOnlyList<string> Warnings);

    public static async Task<IReadOnlyList<CatalogEntry>> ScanAsync(
        string selectedFolder, CancellationToken cancellationToken = default) =>
        (await ScanFullAsync(selectedFolder, cancellationToken)).Entries;

    public static Task<CatalogScan> ScanFullAsync(
        string selectedFolder, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(selectedFolder);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        return Task.Run(() => Scan(root, cancellationToken), cancellationToken);
    }

    private static CatalogScan Scan(string selectedFolder, CancellationToken token)
    {
        var data = Directory.Exists(Path.Combine(selectedFolder, "Data"))
            ? Path.Combine(selectedFolder, "Data") : selectedFolder;
        var isGameInstallation = Directory.Exists(Path.Combine(data, "Core", "Defs"));
        var targets = Packages.Select(name =>
            (Name: name, Folder: Path.Combine(data, name, "Defs")))
            .Where(p => Directory.Exists(p.Folder)).ToList();

        if (targets.Count == 0)
        {
            var folder = Directory.Exists(Path.Combine(data, "Defs"))
                ? Path.Combine(data, "Defs") : data;
            targets.Add((Path.GetFileName(data), folder));
        }

        var entries = new List<CatalogEntry>();
        var sources = new List<VanillaMechanoidLibrary.SourceDef>();
        var warnings = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenFiles = 0;
        foreach (var (package, folder) in targets)
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.xml", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                if (++seenFiles > 15_000)
                    throw new InvalidDataException("Definition catalog exceeds 15000 XML files.");
                try
                {
                    var info = new FileInfo(path);
                    if (info.Length > DefDocument.MaxXmlCharacters * 4L)
                    {
                        if (warnings.Count < 20) warnings.Add("Skipped oversized XML: " + path);
                        continue;
                    }
                    var doc = DefDocument.Parse(File.ReadAllText(path), path);
                    if (doc.Xml.Root?.Name.LocalName != "Defs") continue;
                    foreach (var e in doc.Xml.Root.Elements())
                    {
                        var type = e.Name.LocalName;
                        if (!type.EndsWith("Def", StringComparison.Ordinal)) continue;
                        var name = e.Element("defName")?.Value.Trim();
                        var template = e.Attribute("Name")?.Value.Trim();
                        var parent = e.Attribute("ParentName")?.Value.Trim();
                        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(template)) continue;
                        if (isGameInstallation)
                            sources.Add(new VanillaMechanoidLibrary.SourceDef(
                                type, name, template, parent, package, path, e));
                        if (!string.IsNullOrEmpty(name))
                        {
                            entries.Add(new CatalogEntry(
                                new DefinitionIdentity(type, name, path),
                                e.Element("label")?.Value.Trim() ?? name, path, package));
                            counts[package] = counts.GetValueOrDefault(package) + 1;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or XmlException or InvalidDataException)
                {
                    if (warnings.Count < 20)
                        warnings.Add("Unable to read " + path + ": " + ex.Message);
                }
            }
        }

        var packages = Packages.Select(x => new PackageScan(x,
            Directory.Exists(Path.Combine(data, x, "Defs")),
            counts.GetValueOrDefault(x))).ToList();
        var library = isGameInstallation
            ? VanillaMechanoidLibrary.Build(sources, token)
            : VanillaMechanoidLibrary.Empty;
        return new CatalogScan(entries, packages, library, isGameInstallation, warnings);
    }
}
