using MechWright.RimWorld;

namespace MechWright.Infrastructure;

/// <summary>Read-only local asset locator. Missing assets are reported, not
/// substituted with a different direction or copied to output packages.</summary>
public static class TextureAssetLocator
{
    public sealed record Candidate(
        string Direction, string RelativePath, string? LocalPath,
        bool Exists)
    {
        public string Display => Exists ? $"✓ {Direction} · {RelativePath}"
                                      : $"– {Direction} · 未找到本地贴图";
    }

    public static IReadOnlyList<Candidate> Resolve(
        string? projectRoot, GraphicReferences.GraphicDescriptor descriptor)
    {
        if (!TryNormalizeTexturePath(descriptor.TexturePath, out var path))
            return [new Candidate("invalid", descriptor.TexturePath, null, false)];

        var multi = string.Equals(descriptor.GraphicClass, "Graphic_Multi",
            StringComparison.Ordinal);
        var parts = multi
            ? new[] { ("south", "_south"), ("north", "_north"),
                      ("east", "_east"), ("west", "_west") }
            : new[] { ("single", "") };
        var prefixes = projectRoot == null || !Directory.Exists(projectRoot)
            ? Array.Empty<string>()
            : new[] { Path.Combine(projectRoot, "Textures"),
                      Path.Combine(projectRoot, "1.7", "Textures") };

        var candidates = new List<Candidate>();
        foreach (var (direction, suffix) in parts)
        {
            var rel = "Textures/" + path + suffix + ".png";
            string? actual = null;
            foreach (var prefix in prefixes)
            {
                var candidate = Path.GetFullPath(Path.Combine(prefix,
                    (path + suffix + ".png").Replace('/', Path.DirectorySeparatorChar)));
                if (!candidate.StartsWith(Path.GetFullPath(prefix) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(candidate) &&
                    (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) == 0)
                {
                    actual = candidate;
                    break;
                }
            }
            candidates.Add(new Candidate(direction, rel, actual, actual != null));
        }
        return candidates;
    }

    /// <summary>Resolve registered user assets before probing an imported Mod's
    /// source directory, including newly attached images not exported yet.</summary>
    public static IReadOnlyList<Candidate> ResolveWorkspace(
        MechWorkspace? workspace, GraphicReferences.GraphicDescriptor descriptor)
    {
        var baseCandidates = Resolve(workspace?.SourceRoot, descriptor);
        if (workspace == null || workspace.Assets.Count == 0) return baseCandidates;
        return baseCandidates.Select(candidate =>
        {
            var match = workspace.Assets.FirstOrDefault(asset =>
                asset.RelativePath.Equals(candidate.RelativePath, StringComparison.OrdinalIgnoreCase) ||
                asset.RelativePath.Equals("1.7/" + candidate.RelativePath, StringComparison.OrdinalIgnoreCase) ||
                asset.RelativePath.Equals("Common/" + candidate.RelativePath, StringComparison.OrdinalIgnoreCase));
            if (match == null) return candidate;
            var exists = File.Exists(match.SourcePath);
            return new Candidate(candidate.Direction, candidate.RelativePath,
                exists ? match.SourcePath : null, exists);
        }).ToArray();
    }

    private static bool TryNormalizeTexturePath(string path, out string normalized)
    {
        normalized = path.Replace('\\', '/').Trim();
        if (normalized.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["Textures/".Length..];
        if (string.IsNullOrEmpty(normalized) ||
            normalized.StartsWith('/') || normalized.Contains(':') ||
            normalized.Contains('\\') ||
            normalized.Split('/').Any(x => x is ".." or "." or ""))
            return false;
        return true;
    }
}
