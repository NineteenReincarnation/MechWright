using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MechWright.Infrastructure;

/// <summary>
/// Reads user-supplied directional PNG packs. Only selected images are extracted;
/// no archive paths are ever used as filesystem destinations.
/// </summary>
public static class DirectionalTextureArchive
{
    private static readonly string[] Required = ["south", "north", "east"];
    private static readonly string[] Directions = ["south", "north", "east", "west"];
    private const long MaxArchiveBytes = 512L * 1024 * 1024;
    private const long MaxImageBytes = 12L * 1024 * 1024;

    public sealed record TextureGroup(string Name, IReadOnlyDictionary<string, string> Entries)
    {
        public string Display => Name + " · " + string.Join(" / ",
            Directions.Where(Entries.ContainsKey).Select(x => x.ToUpperInvariant()));
    }

    public static IReadOnlyList<TextureGroup> Inspect(string zipPath)
    {
        var source = new FileInfo(zipPath);
        if (!source.Exists || source.Length > MaxArchiveBytes)
            throw new InvalidDataException("贴图包不存在或超过 512 MiB 限制。");

        using var zip = ZipFile.OpenRead(source.FullName);
        if (zip.Entries.Count > 8192)
            throw new InvalidDataException("贴图包包含过多文件。");
        var grouped = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (entry.Length == 0 || entry.Length > MaxImageBytes ||
                !entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            var stem = Path.GetFileNameWithoutExtension(entry.Name);
            var direction = Directions.FirstOrDefault(x =>
                stem.EndsWith(" " + x, StringComparison.OrdinalIgnoreCase) ||
                stem.EndsWith("_" + x, StringComparison.OrdinalIgnoreCase) ||
                stem.EndsWith("-" + x, StringComparison.OrdinalIgnoreCase));
            if (direction == null) continue;
            var name = stem[..^(direction.Length + 1)].Trim();
            // Never turn an archive-supplied name into an arbitrary disk path.
            if (name.Length is < 1 or > 100 ||
                name.Any(x => !(char.IsLetterOrDigit(x) || x is '_' or '-'))) continue;
            if (!grouped.TryGetValue(name, out var entries))
                grouped[name] = entries = new(StringComparer.OrdinalIgnoreCase);
            // A duplicate direction is ambiguous; reject that group instead of
            // silently selecting whichever entry happens to come first.
            if (!entries.TryAdd(direction, entry.FullName))
                entries[direction] = "";
        }
        return grouped.Where(x => Required.All(d =>
                x.Value.TryGetValue(d, out var path) && path.Length > 0))
            .Select(x => new TextureGroup(x.Key,
                new Dictionary<string, string>(x.Value, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> Extract(string zipPath, TextureGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var verified = Inspect(zipPath).FirstOrDefault(x =>
            x.Name.Equals(group.Name, StringComparison.OrdinalIgnoreCase));
        if (verified == null)
            throw new InvalidDataException("所选机械体缺少完整的南、北、东方向贴图。");
        var file = new FileInfo(zipPath);
        var sourceId = file.FullName + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceId)))[..20];
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MechWright", "TextureImports", hash, verified.Name);
        Directory.CreateDirectory(root);

        using var zip = ZipFile.OpenRead(file.FullName);
        var output = new List<string>();
        foreach (var direction in Directions)
        {
            if (!verified.Entries.TryGetValue(direction, out var entryName)) continue;
            var entry = zip.GetEntry(entryName)
                ?? throw new InvalidDataException("ZIP 内贴图发生变化，请重新选择。");
            if (entry.Length > MaxImageBytes)
                throw new InvalidDataException("单张 PNG 超过 12 MiB 限制。");
            var target = Path.Combine(root, verified.Name + "_" + direction + ".png");
            var temp = target + ".tmp";
            try
            {
                using (var input = entry.Open())
                using (var dest = new FileStream(temp, FileMode.Create, FileAccess.Write))
                {
                    input.CopyTo(dest);
                    if (dest.Length > MaxImageBytes ||
                        dest.Length != entry.Length)
                        throw new InvalidDataException("PNG 数据大小不正确。");
                }
                File.Move(temp, target, true);
                output.Add(target);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        return output;
    }
}
