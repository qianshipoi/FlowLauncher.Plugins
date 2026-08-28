using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Views.Tools;

public partial class BaseView : UserControl
{
    public BaseView()
    {
        InitializeComponent();
        Rows.StatusChanged += s => StatusText.Text = s;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConvertInput();
            e.Handled = true;
        }
    }

    private void Convert_Click(object sender, RoutedEventArgs e) => ConvertInput();

    private void ConvertInput()
    {
        var input = InputBox.Text.Trim();
        if (input.Length == 0)
        {
            StatusText.Text = "请输入要转换的值";
            return;
        }

        try
        {
            Rows.SetResults(BaseConverterService.Convert(input));
            StatusText.Text = "转换完成";
        }
        catch (FormatException ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
