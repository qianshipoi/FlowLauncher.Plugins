using System.IO;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DevToolkit.OpenDirectory.Views;

public partial class OpenDirectoryPage : UserControl
{
    public OpenDirectoryPage()
    {
        InitializeComponent();
    }

    private void Path_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Open();
            e.Handled = true;
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e) => Open();

    private void Open()
    {
        var raw = PathBox.Text.Trim().Trim('"');
        if (raw.Length == 0)
        {
            StatusText.Text = "请输入要打开的路径";
            return;
        }

        var expanded = Environment.ExpandEnvironmentVariables(raw);
        var path = PathService.Normalize(expanded);

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            StatusText.Text = $"路径不存在：{path}";
            return;
        }

        var dirToOpen = Directory.Exists(path)
            ? path
            : Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;

        try
        {
            PathService.OpenInExplorer(path, dirToOpen);
            StatusText.Text = $"已在资源管理器中打开：{path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"打开失败：{ex.Message}";
        }
    }
}
