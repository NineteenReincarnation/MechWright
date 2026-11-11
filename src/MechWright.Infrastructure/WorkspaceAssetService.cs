using MechWright.RimWorld;

namespace MechWright.Infrastructure;

/// <summary>
/// Project-owned resource inventory and opt-in copy, never scans installed
/// RimWorld Core/DLC resources and never executes attached assemblies.
/// </summary>
public static class WorkspaceAssetService
{
    private const int MaxFiles = 8192;
    private const long MaxFileBytes = 256L * 1024 * 1024;
    private const long MaxTotalBytes = 2L * 1024 * 1024 * 1024;

    public sealed record Inventory(int Files, long TotalBytes)
    {
        public string Description => Files + " 项 / " +
            (TotalBytes / 1024d / 1024d).ToString("0.0") + " MiB";
    }

    public static WorkspaceAsset Register(
        MechWorkspace project, string sourcePath, string relativePath,
        bool fromDisk = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        var logical = WorkspaceAsset.ValidatePath(relativePath);
        var full = Path.GetFullPath(sourcePath);
        EnsureRegularFile(full);
        var size = new FileInfo(full).Length;
        if (size > MaxFileBytes)
            throw new InvalidDataException("One resource exceeds 256 MiB.");
        if (project.Assets.Count + 1 > MaxFiles ||
            project.Assets.Sum(x => x.Length) + size > MaxTotalBytes)
            throw new InvalidDataException("Total resource count / size exceeds the safety budget.");
        return project.AddAsset(logical, full, size, fromDisk);
    }

    public static Inventory InventoryOf(MechWorkspace project) =>
        new(project.Assets.Count, project.Assets.Sum(x => x.Length));

    /// <summary>Called when importing a Mod root: registers ONLY its own
    /// Textures/Sounds/Assemblies and preview images, without copying bytes.</summary>
    public static Inventory ImportAssets(MechWorkspace project, CancellationToken token = default)
    {
        var root = project.SourceRoot;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return InventoryOf(project);
        EnsureNoLinkInParents(root);
        foreach (var rel in EnumerateAssets(root, token))
        {
            token.ThrowIfCancellationRequested();
            var abs = Path.GetFullPath(Path.Combine(root,
                rel.Replace('/', Path.DirectorySeparatorChar)));
            Register(project, abs, rel, fromDisk: true);
        }
        return InventoryOf(project);
    }

    private static IEnumerable<string> EnumerateAssets(string root, CancellationToken token)
    {
        var directories = new List<string>();
        foreach (var prefix in new[] { "", "1.7", "Common" })
            foreach (var kind in new[] { "Textures", "Sounds", "Assemblies" })
            {
                var directory = Path.Combine(root, prefix, kind);
                if (Directory.Exists(directory)) directories.Add(directory);
            }
        var checkedFiles = 0;
        foreach (var directory in directories)
        {
            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                var current = pending.Pop();
                EnsureNoLinkInParents(current);
                foreach (var sub in Directory.EnumerateDirectories(current))
                    pending.Push(sub);
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    token.ThrowIfCancellationRequested();
                    if (++checkedFiles > MaxFiles)
                        throw new InvalidDataException("Too many resource files in the source Mod.");
                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    try { WorkspaceAsset.ValidatePath(relative); }
                    catch (ArgumentException) { continue; } // Unsupported types are not copied.
                    yield return relative;
                }
            }
        }
        var about = Path.Combine(root, "About", "Preview.png");
        if (File.Exists(about))
            yield return "About/Preview.png";
    }

    public static async Task CopyAsync(
        MechWorkspace project, string stagingRoot, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var root = Path.GetFullPath(stagingRoot);
        var total = 0L;
        var copied = 0;
        foreach (var asset in project.Assets)
        {
            token.ThrowIfCancellationRequested();
            var logical = WorkspaceAsset.ValidatePath(asset.RelativePath);
            var source = Path.GetFullPath(asset.SourcePath);
            EnsureRegularFile(source);
            var size = new FileInfo(source).Length;
            if (size > MaxFileBytes || ++copied > MaxFiles ||
                (total += size) > MaxTotalBytes)
                throw new InvalidDataException("Resource safety limits exceeded during export.");
            var destination = Path.GetFullPath(Path.Combine(root,
                logical.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Resource path escapes export directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = new FileStream(source, FileMode.Open, FileAccess.Read,
                FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(destination, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous);
            await input.CopyToAsync(output, 128 * 1024, token);
        }
    }

    private static void EnsureRegularFile(string full)
    {
        if (!File.Exists(full))
            throw new FileNotFoundException("Resource file no longer exists.", full);
        EnsureNoLinkInParents(full);
        if ((File.GetAttributes(full) & FileAttributes.Directory) != 0)
            throw new InvalidDataException("A resource path must be a file.");
    }

    private static void EnsureNoLinkInParents(string path)
    {
        var cursor = Path.GetFullPath(path);
        for (var i = 0; cursor != null && i < 256; i++)
        {
            if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links / junctions are forbidden for Mod resource copying.");
            var parent = Path.GetDirectoryName(cursor);
            if (parent == cursor) return;
            cursor = parent;
        }
    }
}
