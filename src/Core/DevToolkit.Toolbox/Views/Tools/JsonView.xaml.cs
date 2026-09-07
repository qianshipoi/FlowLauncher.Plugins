using System.Text.Json;

using System.Windows;

using System.Windows.Controls;

namespace DevToolkit.Toolbox.Views.Tools;

public partial class JsonView : UserControl
{
    private readonly Action<string> _openEditor;

    public JsonView(Action<string> openEditor)
    {
        InitializeComponent();
        _openEditor = openEditor;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text;
        var fromClipboard = false;
        if (string.IsNullOrWhiteSpace(text))
        {
            try
            {
                text = System.Windows.Clipboard.GetText();
                fromClipboard = !string.IsNullOrWhiteSpace(text);
            }
            catch
            {
                text = string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText.Text = "请先粘贴 JSON，或复制 JSON 后留空打开";
            return;
        }

        var valid = true;
        try
        {
            using var _ = JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            valid = false;
            StatusText.Text = $"JSON 无效：{ex.Message}（仍可打开编辑器修复）";
        }

        _openEditor(text);
        if (valid)
            StatusText.Text = $"已打开编辑器（{(fromClipboard ? "内容来自剪贴板，" : "")}{text.Length} 字符）";
    }
}
