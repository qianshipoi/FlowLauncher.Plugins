using Microsoft.Win32;

using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.PasswordManager;

/// <summary>
/// SettingsView.xaml 的交互逻辑
/// </summary>
public partial class SettingsView : UserControl
{
    private ImportExportService? _importExport;

    public SettingsView()
    {
        InitializeComponent();
    }

    internal SettingsView(Settings settings, ImportExportService importExport) : this()
    {
        _importExport = importExport;
        DataContext = new { Settings = settings };
    }

    private void ExportEncryptedDb_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出加密数据库",
            Filter = "数据库文件|*.db",
            FileName = $"passwords_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _importExport!.ExportEncryptedDb(dialog.FileName);
                var keyPath = System.IO.Path.ChangeExtension(dialog.FileName, ".dbkey");
                StatusText.Text = $"导出成功！共复制数据库文件。\nDB: {dialog.FileName}\n密钥: {keyPath}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导出失败: {ex.Message}";
            }
        }
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出为 Excel",
            Filter = "Excel 文件|*.xlsx",
            FileName = $"passwords_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var count = _importExport!.ExportToExcel(dialog.FileName);
                StatusText.Text = $"导出成功！共导出 {count} 条记录。\n文件: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导出失败: {ex.Message}";
            }
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出为 CSV",
            Filter = "CSV 文件|*.csv",
            FileName = $"passwords_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var count = _importExport!.ExportToCsv(dialog.FileName);
                StatusText.Text = $"导出成功！共导出 {count} 条记录。\n文件: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导出失败: {ex.Message}";
            }
        }
    }

    private void ImportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "从 Excel 导入",
            Filter = "Excel 文件|*.xlsx",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var entries = _importExport!.ImportFromExcel(dialog.FileName);
                if (entries.Count == 0)
                {
                    StatusText.Text = "文件中没有找到有效数据。";
                    return;
                }

                var result = MessageBox.Show(
                    $"找到 {entries.Count} 条记录，确认导入？",
                    "确认导入",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var count = _importExport.ImportEntries(entries);
                    StatusText.Text = $"导入成功！共导入 {count} 条记录。";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导入失败: {ex.Message}";
            }
        }
    }

    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "从 CSV 导入",
            Filter = "CSV 文件|*.csv",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var entries = _importExport!.ImportFromCsv(dialog.FileName);
                if (entries.Count == 0)
                {
                    StatusText.Text = "文件中没有找到有效数据。";
                    return;
                }

                var result = MessageBox.Show(
                    $"找到 {entries.Count} 条记录，确认导入？",
                    "确认导入",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var count = _importExport.ImportEntries(entries);
                    StatusText.Text = $"导入成功！共导入 {count} 条记录。";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导入失败: {ex.Message}";
            }
        }
    }
}
