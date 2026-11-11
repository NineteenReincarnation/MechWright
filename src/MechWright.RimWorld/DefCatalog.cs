using System.Xml;
using System.Xml.Linq;
using MechWright.Core;

namespace MechWright.RimWorld;

/// <summary>Read-only local catalog. No game assets or XML are copied into the editor project.</summary>
public static class DefCatalog
{
    private static readonly string[] Packages =
        ["Core", "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey"];
    public static IReadOnlyList<string> SupportedPackages => Packages;

    public static async Task<IReadOnlyList<CatalogEntry>> ScanAsync(
        string selectedFolder, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(selectedFolder);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        return await Task.Run(() => Scan(root, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<CatalogEntry> Scan(string selectedFolder, CancellationToken token)
    {
        var data = Directory.Exists(Path.Combine(selectedFolder, "Data"))
            ? Path.Combine(selectedFolder, "Data") : selectedFolder;
        var targets = Packages
            .Select(name => (Name: name, Folder: Path.Combine(data, name, "Defs")))
            .Where(pair => Directory.Exists(pair.Folder)).ToList();

        // Also accept a single Defs directory or a selected Mod directory.
        if (targets.Count == 0)
        {
            var folder = Directory.Exists(Path.Combine(data, "Defs"))
                ? Path.Combine(data, "Defs") : data;
            targets.Add((Path.GetFileName(data), folder));
        }

        var results = new List<CatalogEntry>();
        foreach (var (package, folder) in targets)
        {
            token.ThrowIfCancellationRequested();
            foreach (var path in Directory.EnumerateFiles(folder, "*.xml", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (new FileInfo(path).Length > DefDocument.MaxXmlCharacters) continue;
                    var xml = File.ReadAllText(path);
                    var doc = DefDocument.Parse(xml, path);
                    foreach (var e in doc.Definitions())
                    {
                        if (e.DefName == "(unnamed)") continue;
                        results.Add(new CatalogEntry(
                            new DefinitionIdentity(e.Type, e.DefName, path),
                            e.Label, path, package));
                    }
                }
                catch (IOException) { /* Skip files inaccessible during a live game update. */ }
                catch (UnauthorizedAccessException) { }
                catch (XmlException) { /* Not a supported Defs XML file. */ }
                catch (InvalidDataException) { }
            }
        }
        return results;
    }
}
