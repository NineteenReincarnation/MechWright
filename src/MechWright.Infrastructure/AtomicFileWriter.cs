using System.Text;

namespace MechWright.Infrastructure;

/// <summary>Write through a temporary sibling, with backup and rollback on failure.</summary>
public static class AtomicFileWriter
{
    public static async Task WriteAsync(string target, string contents, CancellationToken token = default)
    {
        var absolute = Path.GetFullPath(target);
        var directory = Path.GetDirectoryName(absolute)
            ?? throw new ArgumentException("Invalid target path.", nameof(target));
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temp, contents, new UTF8Encoding(false), token);
            token.ThrowIfCancellationRequested();
            if (File.Exists(absolute))
            {
                var backup = absolute + ".bak";
                File.Replace(temp, absolute, backup, true);
            }
            else File.Move(temp, absolute);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
