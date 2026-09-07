using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Toolbox.Views.Tools;

public partial class CronView : UserControl
{
    public CronView()
    {
        InitializeComponent();
        Rows.StatusChanged += s => StatusText.Text = s;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Parse();
            e.Handled = true;
        }
    }

    private void Parse_Click(object sender, RoutedEventArgs e) => Parse();

    private void Parse()
    {
        var expression = InputBox.Text.Trim();
        if (expression.Length == 0)
        {
            StatusText.Text = "请输入 Cron 表达式";
            return;
        }

        try
        {
            Rows.SetResults(CronParserService.GetInfo(expression, DateTime.Now));
            StatusText.Text = "解析完成";
        }
        catch (FormatException ex)
        {
            Rows.Clear();
            StatusText.Text = ex.Message;
        }
    }
}
