using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Toolbox.Views.Tools;

public partial class EncView : UserControl
{
    public EncView()
    {
        InitializeComponent();
        Rows.StatusChanged += s => StatusText.Text = s;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            ConvertInput();
            e.Handled = true;
        }
    }

    private void Convert_Click(object sender, RoutedEventArgs e) => ConvertInput();

    private void ConvertInput()
    {
        var text = InputBox.Text;
        if (text.Length == 0)
        {
            StatusText.Text = "请输入要转换的文本";
            return;
        }

        Rows.SetResults(TextEncoderService.GetTransforms(text));
        StatusText.Text = $"完成（{text.Length} 字符）";
    }
}
