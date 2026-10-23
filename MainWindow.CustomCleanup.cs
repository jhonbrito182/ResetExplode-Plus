using System.Windows;
using System.Windows.Controls;

namespace ResetExplode;

public partial class MainWindow
{
    private List<CleanupTarget> _cleanupTargets = new();

    private void InitCustomCleanup()
    {
        if (_cleanupTargets.Count == 0)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cleanupTargets =
            [
                new CleanupTarget
                {
                    Name = "Temporários do usuário",
                    Description = "Arquivos antigos da pasta temporária da sua conta. Arquivos recentes ou em uso são ignorados.",
                    Kind = CleanupKind.Directory,
                    Path = System.IO.Path.GetTempPath(),
                    Selected = true
                },
                new CleanupTarget
                {
                    Name = "Cache de shaders DirectX",
                    Description = "Cache que o Windows recria quando necessário.",
                    Kind = CleanupKind.Directory,
                    Path = System.IO.Path.Combine(local, "D3DSCache"),
                    Selected = true
                },
                new CleanupTarget
                {
                    Name = "Miniaturas do Windows",
                    Description = "Miniaturas antigas do Explorador de Arquivos. O Windows as cria novamente.",
                    Kind = CleanupKind.Pattern,
                    Path = System.IO.Path.Combine(local, "Microsoft", "Windows", "Explorer"),
                    Pattern = "thumbcache_*.db",
                    Selected = true
                },
                new CleanupTarget
                {
                    Name = "Temporários do Windows",
                    Description = "Arquivos antigos em C:\\Windows\\Temp. Arquivos em uso ou sem acesso são ignorados.",
                    Kind = CleanupKind.Directory,
                    Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
                    Selected = false
                },
                new CleanupTarget
                {
                    Name = "Lixeira",
                    Description = "Esvazia os itens da Lixeira de todas as unidades.",
                    Kind = CleanupKind.RecycleBin,
                    Selected = false
                }
            ];
            CleanupList.ItemsSource = _cleanupTargets;
        }

        CleanupStatus.Text = "Selecione as áreas que deseja limpar. Downloads e documentos pessoais não são incluídos.";
    }

    private void BtnCleanupAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _cleanupTargets) target.Selected = true;
        CleanupList.Items.Refresh();
    }

    private void BtnCleanupNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _cleanupTargets) target.Selected = false;
        CleanupList.Items.Refresh();
    }

    private async void BtnCleanupStart_Click(object sender, RoutedEventArgs e)
    {
        var selected = _cleanupTargets.Where(x => x.Selected).ToList();
        if (selected.Count == 0)
        {
            CleanupStatus.Text = "Marque pelo menos uma área para limpar.";
            return;
        }

        string names = string.Join(Environment.NewLine, selected.Select(x => "• " + x.Name));
        if (MessageBox.Show($"Limpar as áreas selecionadas?{Environment.NewLine}{Environment.NewLine}{names}",
                "Limpeza personalizada", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        BtnCleanupStart.IsEnabled = false;
        CleanupStatus.Text = "Limpando arquivos antigos…";
        try
        {
            var result = await Task.Run(() => CustomCleaner.Clean(selected));
            string size = result.BytesFreed >= 1024L * 1024
                ? $"{result.BytesFreed / 1024d / 1024d:N1} MB"
                : $"{result.BytesFreed / 1024d:N0} KB";
            CleanupStatus.Text = $"{result.FilesRemoved:N0} arquivo(s) removido(s), aproximadamente {size} liberados." +
                (result.RecycleBinEmptied ? " A Lixeira foi esvaziada." : "") +
                (result.RecycleBinErrors > 0 ? " Não foi possível esvaziar a Lixeira em uma ou mais unidades." : "");
        }
        catch (Exception ex)
        {
            CleanupStatus.Text = $"A limpeza não foi concluída: {ex.Message}";
        }
        finally
        {
            BtnCleanupStart.IsEnabled = true;
        }
    }
}