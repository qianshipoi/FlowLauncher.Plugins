using DevToolkit.Toolbox.Services;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

namespace DevToolkit.Toolbox.Views.Tools;

public partial class JwtView : UserControl
{
    private readonly Action<string> _openEditor;
    private string? _payload;

    public JwtView(Action<string> openEditor)
    {
        InitializeComponent();
        _openEditor = openEditor;
        Rows.StatusChanged += s => StatusText.Text = s;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            Decode();
            e.Handled = true;
        }
    }

    private void Decode_Click(object sender, RoutedEventArgs e) => Decode();

    private void Decode()
    {
        var token = InputBox.Text.Trim();
        if (token.Length == 0)
        {
            StatusText.Text = "请先粘贴 JWT";
            return;
        }

        try
        {
            var rows = JwtDecoderService.Decode(token, DateTimeOffset.Now);
            Rows.SetResults(rows);
            _payload = rows.FirstOrDefault(r => r.Description == "Payload")?.Value;
            OpenPayloadButton.IsEnabled = _payload is not null;
            StatusText.Text = "解析完成";
        }
        catch (FormatException ex)
        {
            Rows.Clear();
            OpenPayloadButton.IsEnabled = false;
            _payload = null;
            StatusText.Text = ex.Message;
        }
    }

    private void OpenPayload_Click(object sender, RoutedEventArgs e)
    {
        if (_payload is not null)
            _openEditor(_payload);
    }
}
