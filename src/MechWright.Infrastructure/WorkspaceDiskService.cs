using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using MechWright.Core;
using MechWright.RimWorld;

namespace MechWright.Infrastructure;

/// <summary>
/// Imports a mod's editable XML without touching the original directory. Export
/// uses a new staging directory and publishes it only after every file was written.
/// Existing export directories are never merged or overwritten silently.
/// </summary>
public static class WorkspaceDiskService
{
    private const int MaxFiles = 5000;
    private const long MaxTotalBytes = 128 * 1024 * 1024;
    private static readonly string[] DefFolders = ["Defs", "Patches"];

    public static async Task<MechWorkspace> ImportAsync(
        string sourceFolder, CancellationToken token = default)
    {
        var root = Path.GetFullPath(sourceFolder);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);
        if (HasReparsePoint(root))
            throw new IOException("Import from symlinks and junctions is disabled for safety.");

        var files = EnumerateEditableXml(root).ToList();
        if (files.Count == 0)
            throw new InvalidDataException("No XML files were found under Defs or Patches.");
        if (files.Count > MaxFiles)
            throw new InvalidDataException("Project contains more than 5000 XML files.");

        var total = 0L;
        var workspace = new MechWorkspace(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)), root);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            var size = new FileInfo(file).Length;
            total += size;
            if (size > DefDocument.MaxXmlCharacters * 4L || total > MaxTotalBytes)
                throw new InvalidDataException("Project XML size exceeds the safety limit.");
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = await File.ReadAllTextAsync(file, token);
            try { workspace.AddXml(relative, text); }
            catch (XmlException ex)
            {
                throw new InvalidDataException($"Invalid XML in {relative}: {ex.Message}", ex);
            }
        }
        var about = Path.Combine(root, "About", "About.xml");
        if (File.Exists(about) && !HasReparsePoint(about) &&
            new FileInfo(about).Length <= 1024 * 1024)
        {
            var content = await File.ReadAllTextAsync(about, token);
            // Treat imported mod metadata as read-only project data.
            DefDocument.Parse(content, about);
            workspace.OriginalAboutXml = content;
        }
        return workspace;
    }

    private static IEnumerable<string> EnumerateEditableXml(string root)
    {
        var sources = new List<string>();
        foreach (var folder in DefFolders)
        {
            var candidate = Path.Combine(root, folder);
            if (Directory.Exists(candidate)) sources.Add(candidate);
        }
        var version = Path.Combine(root, "1.7");
        foreach (var folder in DefFolders)
        {
            var candidate = Path.Combine(version, folder);
            if (Directory.Exists(candidate)) sources.Add(candidate);
        }
        // A folder chosen directly from inside Defs or Patches is supported.
        if (sources.Count == 0 && DefFolders.Contains(Path.GetFileName(root),
                StringComparer.OrdinalIgnoreCase))
            sources.Add(root);

        foreach (var directory in sources)
        {
            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                if (HasReparsePoint(current)) continue;
                foreach (var sub in Directory.EnumerateDirectories(current))
                {
                    if (!HasReparsePoint(sub)) pending.Push(sub);
                }
                foreach (var file in Directory.EnumerateFiles(current, "*.xml"))
                {
                    if (!HasReparsePoint(file)) yield return file;
                }
            }
        }
    }

    public static async Task<string> ExportAsync(
        MechWorkspace project, string destination,
        string displayName, string packageId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 128)
            throw new ArgumentException("The Mod name must contain 1–128 characters.");
        if (project.OriginalAboutXml == null &&
            !Regex.IsMatch(packageId, @"^[A-Za-z0-9]+(?:[._-][A-Za-z0-9]+)+$"))
            throw new ArgumentException("Enter a valid packageId (for example local.author.modname).");

        var errors = project.Validate().Where(x => x.Issue.Severity == Severity.Error).ToList();
        if (errors.Count != 0)
            throw new InvalidDataException($"{errors.Count} blocking XML diagnostics. First: {errors[0].Issue.Message}");

        var final = Path.GetFullPath(destination);
        if (Directory.Exists(final) || File.Exists(final))
            throw new IOException("The target already exists; select a new export directory to prevent overwrites.");

        var parent = Path.GetDirectoryName(final) ??
            throw new ArgumentException("Cannot export to a drive root.");
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);

        var stage = Path.Combine(parent, ".mechwright-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var file in project.Files)
            {
                token.ThrowIfCancellationRequested();
                var relative = MechWorkspace.ValidateRelativePath(file.RelativePath);
                var target = Path.GetFullPath(Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unsafe project file path escapes the export root.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, file.Document.Export(),
                    new UTF8Encoding(false), token);
            }

            var aboutFolder = Path.Combine(stage, "About");
            Directory.CreateDirectory(aboutFolder);
            var aboutText = project.OriginalAboutXml ?? CreateAboutXml(displayName, packageId);
            await File.WriteAllTextAsync(Path.Combine(aboutFolder, "About.xml"),
                aboutText, new UTF8Encoding(false), token);

            token.ThrowIfCancellationRequested();
            Directory.Move(stage, final);
            return final;
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
        }
    }

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static string CreateAboutXml(string title, string id)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("ModMetaData",
                new XElement("name", title),
                new XElement("packageId", id),
                new XElement("supportedVersions", new XElement("li", "1.7"))));
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }
}
