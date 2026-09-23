using Microsoft.Win32;

using Microsoft.Web.WebView2.Core;

using System.IO;
using System.Text.Json;

using System.Windows;

namespace DevToolkit.Core.Wpf;

/// <summary>
/// JSON 编辑器窗口（WebView2 + 本地 jsoneditor），宿主无关：
/// 调用方提供 wwwroot 静态资源目录与 WebView2 用户数据目录。
/// </summary>
public partial class JsonEditorWindow : Window
{
    private const string VirtualHost = "appassets.local";

    private readonly string _webRoot;
    private readonly string _userDataFolder;

    private string _pendingJson;
    private bool _editorReady;
    private bool _closed;

    public JsonEditorWindow(string webRoot, string userDataFolder, string json)
    {
        InitializeComponent();
        _webRoot = webRoot;
        _userDataFolder = userDataFolder;
        _pendingJson = json;
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _closed = true;
            WebView.Dispose();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _userDataFolder);
            await WebView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"WebView2 初始化失败：{ex.Message}", "JSON 编辑器");
            Close();
            return;
        }

        if (_closed)
            return;

        var core = WebView.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(VirtualHost, _webRoot, CoreWebView2HostResourceAccessKind.Allow);
        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate($"https://{VirtualHost}/index.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var message = JsonDocument.Parse(e.WebMessageAsJson);
        switch (message.RootElement.GetProperty("type").GetString())
        {
            case "ready":
                _editorReady = true;
                SendPendingJson();
                break;

            case "copy":
                var text = message.RootElement.GetProperty("text").GetString() ?? string.Empty;
                Clipboard.SetText(text);
                break;

            case "export":
                ExportJson(message.RootElement.GetProperty("text").GetString() ?? string.Empty);
                break;
        }
    }

    private void ExportJson(string text)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 JSON",
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = "data.json",
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出失败：{ex.Message}", "JSON 编辑器");
        }
    }

    private void SendPendingJson()
    {
        var payload = JsonSerializer.Serialize(new { type = "init", json = _pendingJson });
        WebView.CoreWebView2.PostWebMessageAsJson(payload);
    }

    /// <summary>窗口已打开时注入新的 JSON 内容；未就绪则缓存待编辑器加载后发送。</summary>
    public void UpdateJson(string json)
    {
        _pendingJson = json;
        if (_editorReady)
            SendPendingJson();
    }
}
