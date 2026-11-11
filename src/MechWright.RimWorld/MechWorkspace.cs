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
    private readonly List<WorkspaceAsset> _assets = [];
    private readonly Dictionary<string, WorkspaceAsset> _assetByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _assetListModified;
    private readonly Dictionary<string, long> _indexedRevisions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (long Revision, IReadOnlyList<Diagnostic> Issues)> _validationCache =
        new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public string SourceRoot { get; }
    public IReadOnlyList<WorkspaceFile> Files => _files.AsReadOnly();
    public IReadOnlyList<WorkspaceAsset> Assets => _assets.AsReadOnly();
    public bool IsDirty => _files.Any(x => x.IsNew || x.Document.IsDirty) ||
        _assetListModified || _assets.Any(x => x.IsNew);
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
        return file; // Lazy: avoid O(N²) reindexing during large imports.
    }

    /// <summary>Pre-parse and verify all XML files before adding any of them.
    /// This prevents partially generated three-file mechanoids.</summary>
    public IReadOnlyList<WorkspaceFile> AddXmlBatch(
        IReadOnlyDictionary<string, string> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (_files.Count + documents.Count > 5000)
            throw new InvalidOperationException("Project XML file limit exceeded.");
        var parsed = documents.Select(x =>
        {
            var path = ValidateRelativePath(x.Key);
            return new WorkspaceFile(path, DefDocument.Parse(x.Value, path))
                { IsNew = true };
        }).ToList();
        if (parsed.Select(x => x.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != parsed.Count ||
            parsed.Any(x => _byPath.ContainsKey(x.RelativePath)))
            throw new InvalidOperationException("One or more XML paths already exist. Nothing was inserted.");

        var newDefs = parsed.SelectMany(x => x.Document.Definitions())
            .Where(x => x.DefName != "(unnamed)")
            .Select(x => (x.Type, x.DefName)).ToArray();
        if (newDefs.Distinct().Count() != newDefs.Length ||
            newDefs.Any(x => FindDefinitions(x.Type, x.DefName).Count != 0))
            throw new InvalidOperationException("Generated XML contains a duplicate DefName. Nothing was inserted.");

        foreach (var file in parsed)
        {
            _files.Add(file);
            _byPath.Add(file.RelativePath, file);
        }
        return parsed;
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
            if (!_validationCache.TryGetValue(file.RelativePath, out var cached) ||
                cached.Revision != file.Document.Revision)
            {
                cached = (file.Document.Revision, file.Document.Validate().ToList());
                _validationCache[file.RelativePath] = cached;
            }
            foreach (var issue in cached.Issues)
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

    public WorkspaceAsset AddAsset(string relativePath, string localSourcePath,
        long size, bool fromDisk = false)
    {
        relativePath = WorkspaceAsset.ValidatePath(relativePath);
        if (_assetByPath.ContainsKey(relativePath))
            throw new InvalidOperationException("An asset already exists at " + relativePath);
        if (_assets.Count >= 5000) throw new InvalidOperationException("Too many project assets.");
        if (string.IsNullOrWhiteSpace(localSourcePath) || size < 0)
            throw new ArgumentException("Invalid local resource file.");
        var asset = new WorkspaceAsset(relativePath, localSourcePath, size)
        {
            IsNew = !fromDisk
        };
        _assets.Add(asset);
        _assetByPath.Add(relativePath, asset);
        return asset;
    }

    public void RemoveAsset(string relativePath)
    {
        var path = WorkspaceAsset.ValidatePath(relativePath);
        if (!_assetByPath.Remove(path, out var asset))
            throw new InvalidOperationException("The project resource no longer exists.");
        _assets.Remove(asset);
        _assetListModified = true;
    }

    public void MarkAllSaved(bool includeAssets = true)
    {
        foreach (var file in _files)
        {
            file.Document.MarkSaved();
            file.IsNew = false;
        }
        if (includeAssets)
        {
            foreach (var asset in _assets) asset.IsNew = false;
            _assetListModified = false;
        }
    }

    public void Reindex()
    {
        if (_indexedRevisions.Count == _files.Count &&
            _files.All(f => _indexedRevisions.TryGetValue(f.RelativePath, out var revision)
                && revision == f.Document.Revision))
            return;
        _index.Clear();
        _indexedRevisions.Clear();
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
            _indexedRevisions[file.RelativePath] = file.Document.Revision;
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

public sealed record WorkspaceAsset(string RelativePath, string SourcePath, long Length)
{
    public bool IsNew { get; internal set; }

    private static readonly IReadOnlyDictionary<string, string[]> Extensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Textures"] = [".png", ".jpg", ".jpeg"],
            ["Sounds"] = [".wav", ".ogg"],
            ["Assemblies"] = [".dll"],
            ["About"] = [".png"]
        };

    public static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path.Contains(':') || path.Contains('\0'))
            throw new ArgumentException("Asset path must be relative to a Mod.");
        var segments = path.Replace('\\', '/').Split('/');
        if (segments.Length < 2 || segments.Any(x => x is "" or "." or ".." ||
            x.EndsWith(' ') || x.EndsWith('.') ||
            x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("Unsafe asset filename.");
        var offset = segments[0] is "1.7" or "Common" ? 1 : 0;
        if (segments.Length <= offset + 1 ||
            !Extensions.TryGetValue(segments[offset], out var allowed))
            throw new ArgumentException("Resources must be in Textures, Sounds, Assemblies or About.");
        var fileName = segments[^1];
        if (segments[offset].Equals("About", StringComparison.OrdinalIgnoreCase) &&
            (segments.Length != offset + 2 ||
            !fileName.Equals("Preview.png", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Only About/Preview.png can be included as an asset.");
        if (!allowed.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("File extension is not permitted in this Mod resource folder.");
        var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2",
            "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        if (segments.Any(x => reserved.Contains(x.Split('.')[0], StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Reserved Windows filename in resource path.");
        return string.Join('/', segments);
    }
}
