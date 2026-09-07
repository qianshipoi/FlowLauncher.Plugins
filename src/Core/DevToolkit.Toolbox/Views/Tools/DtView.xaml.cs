using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

namespace DevToolkit.Toolbox.Views.Tools;

public partial class DtView : UserControl
{
    public DtView()
    {
        InitializeComponent();
        Rows.StatusChanged += s => StatusText.Text = s;
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        Rows.SetResults(TimestampProvider.GetEntries(DateTimeOffset.Now)
            .Select(entry => new ToolRow(entry.Description, entry.Value)));
        StatusText.Text = $"已刷新（{DateTime.Now:HH:mm:ss}）";
    }
}
