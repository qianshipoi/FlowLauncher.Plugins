using DevToolkit.Toolbox.Services;

using System.IO;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Views.Tools;

public partial class HashView : UserControl
{
    public HashView()
    {
        InitializeComponent();
        Rows.StatusChanged += s => StatusText.Text = s;
    }

    private void Path_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Compute();
            e.Handled = true;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择要计算哈希的文件" };
        if (dialog.ShowDialog() == true)
        {
            PathBox.Text = dialog.FileName;
            Compute();
        }
    }

    private void Compute_Click(object sender, RoutedEventArgs e) => Compute();

    private async void Compute()
    {
        var path = PathBox.Text.Trim().Trim('"');
        if (path.Length == 0)
        {
            StatusText.Text = "请先选择或输入文件路径";
            return;
        }

        if (!File.Exists(path))
        {
            StatusText.Text = $"文件不存在：{path}";
            return;
        }

        StatusText.Text = "计算中…";
        try
        {
            var hashes = await Task.Run(() => HashCalculator.ComputeFileHashes(path));
            Rows.SetResults(hashes.Select(h => new ToolRow(h.Algorithm, h.HashHex)));
            StatusText.Text = $"{Path.GetFileName(path)} 计算完成";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"计算失败：{ex.Message}";
        }
    }
}
