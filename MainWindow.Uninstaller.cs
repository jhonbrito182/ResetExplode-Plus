using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;

namespace ResetExplode;

public partial class MainWindow
{
    private List<InstalledProgram> _installedPrograms = new();
    private ICollectionView? _installedProgramsView;

    private void InitUninstaller() => RefreshUninstaller();

    private void RefreshUninstaller()
    {
        _installedPrograms = InstalledProgramCatalog.Read();
        _installedProgramsView = CollectionViewSource.GetDefaultView(_installedPrograms);
        _installedProgramsView.Filter = item =>
        {
            if (item is not InstalledProgram program) return false;
            string query = UninstallerSearch.Text.Trim();
            return query.Length == 0 ||
                program.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                (program.Publisher?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false);
        };
        UninstallerList.ItemsSource = _installedProgramsView;
        UninstallerStatus.Text = $"{_installedPrograms.Count:N0} programa(s) encontrado(s).";
    }
    private void UninstallerSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _installedProgramsView?.Refresh();
    }

    private void BtnUninstallerRefresh_Click(object sender, RoutedEventArgs e) => RefreshUninstaller();

    private void UninstallProgram_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstalledProgram program) return;
        if (string.IsNullOrWhiteSpace(program.UninstallCommand))
        {
            UninstallerStatus.Text = $"“{program.Name}” não fornece um comando de desinstalação.";
            return;
        }

        if (MessageBox.Show($"Abrir o desinstalador de “{program.Name}”?{Environment.NewLine}{Environment.NewLine}" +
                            "O programa de desinstalação será iniciado e poderá pedir confirmação.",
                "Desinstalador", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var (fileName, arguments) = SplitCommand(program.UninstallCommand);
            if (Path.GetFileNameWithoutExtension(fileName).Equals("msiexec", StringComparison.OrdinalIgnoreCase))
                arguments = ConvertMsiInstallToUninstall(arguments);

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            });
            UninstallerStatus.Text = $"Desinstalador de “{program.Name}” iniciado. Atualize a lista quando terminar.";
        }
        catch (Exception ex)
        {
            UninstallerStatus.Text = $"Não foi possível iniciar o desinstalador: {ex.Message}";
        }
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        IntPtr argv = CommandLineToArgvW(command, out int count);
        if (argv == IntPtr.Zero || count == 0)
            throw new InvalidOperationException("O comando de desinstalação registrado é inválido.");

        try
        {
            string? fileName = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException("O comando de desinstalação não informa um executável.");

            var args = new List<string>();
            for (int i = 1; i < count; i++)
            {
                string? arg = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size));
                if (arg != null) args.Add(QuoteArgument(arg));
            }
            return (Environment.ExpandEnvironmentVariables(fileName), string.Join(" ", args));
        }
        finally { LocalFree(argv); }
    }

    private static string ConvertMsiInstallToUninstall(string arguments)
    {
        // Alguns registros usam /I para manutenção; /X inicia diretamente a remoção do pacote MSI.
        return new System.Text.RegularExpressions.Regex(@"(?i)(?<!\S)/I(?=\s|\{)").Replace(arguments, "/X", 1);
    }

    private static string QuoteArgument(string value)
    {
        if (value.Length > 0 && value.All(c => !char.IsWhiteSpace(c) && c != '"')) return value;
        var result = new System.Text.StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"')
            {
                result.Append('\\', slashes * 2 + 1).Append('"');
                slashes = 0;
                continue;
            }
            result.Append('\\', slashes).Append(c);
            slashes = 0;
        }
        result.Append('\\', slashes * 2).Append('"');
        return result.ToString();
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int argumentCount);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}