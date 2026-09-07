using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

namespace DevToolkit.Toolbox.Views.Controls;

/// <summary>通用结果行列表（描述 + 值 + 复制按钮）。DataContext 设为 ToolRow 集合即可。</summary>
public partial class ResultRowsView : UserControl
{
    /// <summary>复制完成后的状态提示。</summary>
    public event Action<string>? StatusChanged;

    public ResultRowsView()
    {
        InitializeComponent();
    }

    public void SetResults(IEnumerable<ToolRow> rows)
    {
        DataContext = rows.ToList();
    }

    public void Clear()
    {
        DataContext = new List<ToolRow>();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ToolRow row)
            return;

        try
        {
            System.Windows.Clipboard.SetText(row.Value);
            StatusChanged?.Invoke($"已复制：{row.Description}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"复制失败：{ex.Message}");
        }
    }
}
