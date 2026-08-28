using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Views.Tools;

public partial class GuidView : UserControl
{
    public GuidView()
    {
        InitializeComponent();
        Rows.StatusChanged += s => StatusText.Text = s;
        Generate();
    }

    private void Name_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Generate();
            e.Handled = true;
        }
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Generate();

    private void Generate()
    {
        var name = NameBox.Text.Trim();
        var rows = new List<ToolRow>
        {
            new("GUID v4（随机）", GuidGenerator.NewV4().ToString()),
            new("GUID v7（时间有序）", GuidGenerator.NewV7().ToString()),
            new("GUID v1（时间戳）", GuidGenerator.NewV1().ToString()),
        };

        if (name.Length > 0)
        {
            rows.Add(new ToolRow($"GUID v3（MD5 · DNS: {name}）", GuidGenerator.NewV3(GuidGenerator.DnsNamespace, name).ToString()));
            rows.Add(new ToolRow($"GUID v5（SHA1 · DNS: {name}）", GuidGenerator.NewV5(GuidGenerator.DnsNamespace, name).ToString()));
        }

        Rows.SetResults(rows);
        StatusText.Text = name.Length > 0 ? "已生成（含命名变体）" : "已生成";
    }
}
