using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Toolbox.Views.Tools;

public partial class PwView : UserControl
{
    public PwView()
    {
        InitializeComponent();
        Rows.StatusChanged += s => StatusText.Text = s;
        Generate();
    }

    private void Length_KeyDown(object sender, KeyEventArgs e)
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
        var length = PasswordGeneratorService.DefaultLength;
        var text = LengthBox.Text.Trim();
        if (text.Length > 0
            && (!int.TryParse(text, out length) || length <= 0 || length > PasswordGeneratorService.MaxLength))
        {
            StatusText.Text = $"长度需为 1-{PasswordGeneratorService.MaxLength}";
            return;
        }

        Rows.SetResults(PasswordGeneratorService.Generate(length));
        StatusText.Text = "已生成，点击行尾复制";
    }
}
