using System.Windows;

namespace FlowLauncher.Plugin.PaperTodo.Views;

public partial class TextInputDialog : Window
{
    internal TextInputDialog(string title, string prompt, string value = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        ValueBox.Text = value;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public string Value => ValueBox.Text.Trim();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Value.Length == 0)
        {
            MessageBox.Show(this, "内容不能为空。", "PaperTodo");
            return;
        }
        DialogResult = true;
    }
}
