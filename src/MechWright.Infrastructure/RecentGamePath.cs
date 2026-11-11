namespace MechWright.Infrastructure;

/// <summary>
/// Last explicitly selected RimWorld installation; one short local file.
/// No game files are cached or copied. A missing installation is ignored.
/// </summary>
public static class RecentGamePath
{
    private static string StorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MechWright", "rimworld-directory.txt");

    public static string? Find()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                var saved = File.ReadAllText(StorePath).Trim();
                if (IsGame(saved)) return saved;
            }
            var roots = new[] { "C", "D", "E", "F" };
            foreach (var letter in roots)
            {
                foreach (var relative in new[]
                {
                    @"Steam\steamapps\common\RimWorld",
                    @"SteamLibrary\steamapps\common\RimWorld",
                    @"Program Files (x86)\Steam\steamapps\common\RimWorld"
                })
                {
                    var path = letter + @":\" + relative;
                    if (IsGame(path)) return path;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    public static void Remember(string path)
    {
        if (!IsGame(path)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, Path.GetFullPath(path));
        }
        catch (IOException) { } // Preferences must never block a successful scan.
        catch (UnauthorizedAccessException) { }
    }

    private static bool IsGame(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
        var data = Path.GetFileName(Path.TrimEndingDirectorySeparator(path))
            .Equals("Data", StringComparison.OrdinalIgnoreCase)
            ? path : Path.Combine(path, "Data");
        return Directory.Exists(Path.Combine(data, "Core", "Defs"));
    }
}
