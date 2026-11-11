using System.Collections.ObjectModel;
using System.Xml;
using MechWright.Core;

namespace MechWright.RimWorld;

/// <summary>
/// Editable multi-XML project. Paths are always relative to the selected project
/// root. Source XML is not rewritten until the user explicitly exports.
/// </summary>
public sealed class MechWorkspace
{
    private readonly List<WorkspaceFile> _files = [];
    private readonly Dictionary<string, WorkspaceFile> _byPath =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Kind, string Name), List<WorkspaceDefinition>> _index = [];

    public string Name { get; }
    public string SourceRoot { get; }
    public IReadOnlyList<WorkspaceFile> Files => _files.AsReadOnly();
    public bool IsDirty => _files.Any(x => x.IsNew || x.Document.IsDirty);
    public string? OriginalAboutXml { get; set; }
    public int DefinitionCount => _files.Sum(x => x.Document.Definitions().Count);

    public MechWorkspace(string name, string sourceRoot)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "New Mechanoid" : name.Trim();
        SourceRoot = sourceRoot;
    }

    public WorkspaceFile AddXml(string relativePath, string contents, bool fromDisk = false)
    {
        relativePath = ValidateRelativePath(relativePath);
        if (_byPath.ContainsKey(relativePath))
            throw new InvalidOperationException($"Duplicated project XML path: {relativePath}");
        if (_files.Count >= 5000)
            throw new InvalidOperationException("Maximum XML document count exceeded.");
        var file = new WorkspaceFile(relativePath,
            DefDocument.Parse(contents, relativePath)) { IsNew = !fromDisk };
        _files.Add(file);
        _byPath.Add(relativePath, file);
        Reindex();
        return file;
    }

    public WorkspaceFile? FindFile(string relativePath) =>
        _byPath.GetValueOrDefault(relativePath.Replace('\\', '/'));

    public WorkspaceFile? FindDefinitionFile(string type, string defName)
    {
        Reindex();
        return _index.TryGetValue((type, defName), out var definitions) && definitions.Count == 1
            ? definitions[0].File : null;
    }

    public IReadOnlyList<WorkspaceDefinition> FindDefinitions(string type, string defName)
    {
        Reindex();
        return _index.TryGetValue((type, defName), out var matches)
            ? matches.AsReadOnly() : Array.Empty<WorkspaceDefinition>();
    }

    public IReadOnlyList<WorkspaceDiagnostic> Validate()
    {
        Reindex();
        var result = new List<WorkspaceDiagnostic>();
        foreach (var file in _files)
        {
            foreach (var issue in file.Document.Validate())
                result.Add(new WorkspaceDiagnostic(file.RelativePath, issue));
        }
        foreach (var pair in _index.Where(x => x.Value.Count > 1))
        {
            foreach (var match in pair.Value)
                result.Add(new WorkspaceDiagnostic(match.File.RelativePath,
                    new Diagnostic("MW020", Severity.Error,
                        $"Duplicate definition across project: {pair.Key.Kind}/{pair.Key.Name}.",
                        match.Path)));
        }
        return result;
    }

    public IReadOnlyList<WorkspaceReference> GetReferences(
        WorkspaceFile file, IReadOnlyCollection<CatalogEntry>? catalog = null)
    {
        if (!_byPath.TryGetValue(file.RelativePath, out var tracked) || tracked != file)
            throw new InvalidOperationException("File does not belong to the selected project.");
        Reindex();
        var external = (catalog ?? [])
            .GroupBy(x => (x.Identity.Type, x.Identity.DefName))
            .ToDictionary(g => g.Key, g => g.First().SourcePackage);
        var own = file.Document.Definitions().Select(x => (x.Type, x.DefName)).ToHashSet();
        var refs = DefinitionReferences.Collect(file.Document, catalog);
        return refs.Select(r =>
        {
            var key = (r.TargetType, r.TargetDefName);
            var project = _index.GetValueOrDefault(key);
            if (project is { Count: 1 } && !own.Contains(key))
                return new WorkspaceReference(r with
                {
                    Location = ReferenceLocation.ProjectFile,
                    OriginPackage = project[0].File.RelativePath
                }, project[0].File.RelativePath);
            return new WorkspaceReference(r, r.Location == ReferenceLocation.CurrentDocument
                ? file.RelativePath : "");
        }).ToList();
    }

    public void MarkAllSaved()
    {
        foreach (var file in _files)
        {
            file.Document.MarkSaved();
            file.IsNew = false;
        }
    }

    public void Reindex()
    {
        _index.Clear();
        foreach (var file in _files)
        {
            foreach (var def in file.Document.Definitions())
            {
                if (def.DefName == "(unnamed)" || def.Type == "Patch") continue;
                var key = (def.Type, def.DefName);
                if (!_index.TryGetValue(key, out var matches))
                {
                    matches = [];
                    _index[key] = matches;
                }
                matches.Add(new WorkspaceDefinition(file, def.Type, def.DefName,
                    DefDocument.GetPath(def.Element)));
            }
        }
    }

    public static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path.Contains(':') || path.Contains('\0'))
            throw new ArgumentException("Project path must be relative and safe.");
        var segments = path.Replace('\\', '/').Split('/');
        if (segments.Any(x => x is "" or "." or "..") ||
            segments.Any(x => x.EndsWith(' ') || x.EndsWith('.') ||
                x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("Unsafe XML project path.");
        // Windows device names are invalid even when followed by a .xml extension.
        var devices = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2",
            "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        if (segments.Any(x => devices.Contains(x.Split('.')[0], StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Reserved Windows filename in project path.");
        if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only XML project files are supported.");
        return string.Join('/', segments);
    }
}

public sealed record WorkspaceFile(string RelativePath, DefDocument Document)
{
    public bool IsNew { get; internal set; }
    public string Display => RelativePath;
}

public sealed record WorkspaceDefinition(
    WorkspaceFile File, string Type, string DefName, string Path);

public sealed record WorkspaceDiagnostic(string FilePath, Diagnostic Issue);

public sealed record WorkspaceReference(DefinitionReference Reference, string TargetFile);
