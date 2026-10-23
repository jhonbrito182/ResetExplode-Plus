using Microsoft.Win32;

namespace ResetExplode;

internal sealed record InstalledProgram(string Name, string? Publisher, string? Version, string? UninstallCommand);

internal static class InstalledProgramCatalog
{
    private const string UninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    public static List<InstalledProgram> Read()
    {
        var result = new List<InstalledProgram>();
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, result);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, result);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Default, result);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Registry32, result);

        return result
            .GroupBy(x => $"{x.Name.Trim()}|{x.Publisher?.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ReadHive(RegistryHive hive, RegistryView view, List<InstalledProgram> result)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(UninstallPath);
            if (uninstall == null) return;

            foreach (string subName in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var item = uninstall.OpenSubKey(subName);
                    if (item == null) continue;

                    string? name = item.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (Convert.ToInt32(item.GetValue("SystemComponent") ?? 0) == 1) continue;
                    if (item.GetValue("ParentKeyName") != null || item.GetValue("ReleaseType") != null) continue;

                    result.Add(new InstalledProgram(
                        name.Trim(),
                        item.GetValue("Publisher") as string,
                        item.GetValue("DisplayVersion") as string,
                        item.GetValue("UninstallString") as string));
                }
                catch { /* Ignora entradas de registro incompletas. */ }
            }
        }
        catch { /* Uma visão do registro pode não estar disponível neste Windows. */ }
    }
}