using Microsoft.Web.WebView2.Core;

using System.IO;

using System.Text.Json;

using System.Windows;

namespace FlowLauncher.Plugin.Toolbox.Views;

public partial class JsonEditorWindow : Window
{
    private const string VirtualHost = "appassets.local";

    private readonly string _pluginDirectory;
    private readonly string _webRoot;

    private string _pendingJson;
    private bool _editorReady;
    private bool _closed;

    public JsonEditorWindow(string pluginDirectory, string json)
    {
        InitializeComponent();
        _pluginDirectory = pluginDirectory;
        _webRoot = Path.Combine(pluginDirectory, "wwwroot");
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
            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(_pluginDirectory, "WebView2Cache"));
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
