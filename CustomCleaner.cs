using System.IO;
using System.Runtime.InteropServices;

namespace ResetExplode;

internal sealed class CleanupTarget
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public CleanupKind Kind { get; init; }
    public string? Path { get; init; }
    public string? Pattern { get; init; }
    public bool Selected { get; set; }
}

internal enum CleanupKind { Directory, Pattern, RecycleBin }

internal sealed record CustomCleanupResult(int FilesRemoved, long BytesFreed, bool RecycleBinEmptied, int RecycleBinErrors);

internal static class CustomCleaner
{
    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    public static CustomCleanupResult Clean(IEnumerable<CleanupTarget> selected)
    {
        int files = 0, recycleErrors = 0;
        bool recycleEmptied = false;
        long bytes = 0;

        foreach (var target in selected)
        {
            if (target.Kind == CleanupKind.RecycleBin)
            {
                int result = SHEmptyRecycleBin(IntPtr.Zero, null,
                    SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (result != 0) recycleErrors++;
                else recycleEmptied = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.Path) || !Directory.Exists(target.Path))
                continue;

            CleanDirectory(target.Path, target.Pattern, ref files, ref bytes);
        }

        return new CustomCleanupResult(files, bytes, recycleEmptied, recycleErrors);
    }

    private static void CleanDirectory(string root, string? pattern, ref int files, ref long bytes)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string current = pending.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(current).ToArray(); }
            catch { continue; }

            foreach (string entry in entries)
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(entry); }
                catch { continue; }

                // Nunca segue junctions ou links para fora da pasta de limpeza.
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }

                string name = System.IO.Path.GetFileName(entry);
                if (pattern != null && !name.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (pattern != null && !name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var info = new FileInfo(entry);
                    // Mantém arquivos recentes que podem estar sendo usados por uma instalação ou atualização.
                    if (info.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-24)) continue;
                    long length = info.Length;
                    info.Delete();
                    files++;
                    bytes += length;
                }
                catch { /* Arquivos em uso ou sem permissão são ignorados. */ }
            }
        }
    }
}