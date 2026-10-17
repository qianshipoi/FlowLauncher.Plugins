using FlowLauncher.Plugin.PaperTodo.Models;
using FlowLauncher.Plugin.PaperTodo.Services;
using FlowLauncher.Plugin.PaperTodo.ViewModels;

using Microsoft.Web.WebView2.Core;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace FlowLauncher.Plugin.PaperTodo.Views;

public partial class NotePaperWindow : Window
{
    private const string VirtualHost = "papertodo.local";

    private readonly PaperDatabase _database;
    private readonly NoteImageService _imageService;
    private readonly Settings _settings;
    private readonly PaperWindowManager _windowManager;
    private readonly string _webRoot;
    private readonly string _webViewDataDirectory;
    private readonly NotePaperViewModel _viewModel;
    private readonly Dictionary<string, ImageUploadSession> _imageUploads = [];
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _saveDebounce;
    private string _draftMarkdown;
    private bool _webReady;
    private bool _dirty;
    private bool _allowClose;

    internal NotePaperWindow(
        PaperDatabase database,
        Paper paper,
        Settings settings,
        NoteImageService imageService,
        PaperWindowManager windowManager,
        string webRoot,
        string webViewDataDirectory)
    {
        InitializeComponent();
        _database = database;
        _settings = settings;
        _imageService = imageService;
        _windowManager = windowManager;
        _webRoot = webRoot;
        _webViewDataDirectory = webViewDataDirectory;
        _viewModel = new NotePaperViewModel(database, paper);
        _draftMarkdown = MarkdownImageReferences.ToEditorMarkdown(paper.Content);
        DataContext = _viewModel;
        UpdatePinButton();
        Loaded += OnLoadedAsync;
        Closed += OnClosed;
    }

    public string PaperId => _viewModel.Paper.Id;

    public void CloseWithoutPrompt()
    {
        _allowClose = true;
        Close();
    }

    public void RefreshTopmostButton() => UpdatePinButton();

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: _webViewDataDirectory);
            await WebView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"WebView2 初始化失败：{ex.Message}",
                "PaperTodo",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _viewModel.StatusText = "编辑器不可用";
            return;
        }

        if (_allowClose)
        {
            return;
        }

        var core = WebView.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(
            VirtualHost,
            _webRoot,
            CoreWebView2HostResourceAccessKind.Allow);
        core.AddWebResourceRequestedFilter(
            $"{MarkdownImageReferences.LocalImageBase}*",
            CoreWebView2WebResourceContext.Image);
        core.WebResourceRequested += Core_WebResourceRequested;
        core.WebMessageReceived += Core_WebMessageReceived;
        core.NavigationStarting += Core_NavigationStarting;
        core.NewWindowRequested += Core_NewWindowRequested;
        core.Navigate($"https://{VirtualHost}/note-editor.html");
    }

    private async void Core_WebResourceRequested(
        object? sender,
        CoreWebView2WebResourceRequestedEventArgs e)
    {
        using var deferral = e.GetDeferral();
        try
        {
            var uri = new Uri(e.Request.Uri);
            var imageId = uri.Segments.LastOrDefault()?.Trim('/');
            if (!MarkdownImageReferences.IsValidImageId(imageId))
            {
                e.Response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null,
                    404,
                    "Not Found",
                    "Content-Type: text/plain");
                return;
            }

            var image = await _database.GetNoteImageAsync(imageId!, _lifetime.Token);
            if (image is null || !string.Equals(image.PaperId, PaperId, StringComparison.Ordinal))
            {
                e.Response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null,
                    404,
                    "Not Found",
                    "Content-Type: text/plain");
                return;
            }

            e.Response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                new MemoryStream(image.Bytes, writable: false),
                200,
                "OK",
                $"Content-Type: {image.Mime}\r\nCache-Control: no-store");
        }
        catch
        {
            e.Response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                null,
                500,
                "Image unavailable",
                "Content-Type: text/plain");
        }
    }

    private async void Core_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            switch (type)
            {
                case "ready":
                    _webReady = true;
                    SendToEditor(new
                    {
                        type = "init",
                        markdown = MarkdownImageReferences.ToEditorMarkdown(_viewModel.Content),
                    });
                    break;

                case "change":
                    OnEditorChanged(root.GetProperty("markdown").GetString() ?? "");
                    break;

                case "save":
                    _draftMarkdown = root.GetProperty("markdown").GetString() ?? _draftMarkdown;
                    _dirty = true;
                    await SaveContentAsync();
                    break;

                case "imageBegin":
                    BeginImageUpload(root);
                    break;

                case "imageChunk":
                    await ReceiveImageChunkAsync(root);
                    break;

                case "openLink":
                    OpenExternalLink(root.GetProperty("href").GetString());
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"编辑器错误：{ex.Message}";
        }
    }

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith($"https://{VirtualHost}/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        e.Cancel = true;
        OpenExternalLink(e.Uri);
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternalLink(e.Uri);
    }

    private void OnEditorChanged(string markdown)
    {
        _draftMarkdown = markdown;
        _dirty = true;
        _viewModel.StatusText = "未保存";
        _saveDebounce?.Cancel();
        _saveDebounce?.Dispose();
        _saveDebounce = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _ = SaveAfterDelayAsync(_saveDebounce.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(500, token);
            await SaveContentAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<bool> SaveContentAsync(CancellationToken token = default)
    {
        if (!_dirty)
        {
            _viewModel.StatusText = "已保存";
            return true;
        }

        try
        {
            _viewModel.StatusText = "保存中...";
            await _viewModel.SaveContentAsync(_draftMarkdown, token);
            _dirty = false;
            _viewModel.StatusText = $"已保存 {DateTime.Now:HH:mm:ss}";
            return true;
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"保存失败：{ex.Message}";
            return false;
        }
    }

    private async Task CaptureCurrentMarkdownAsync()
    {
        if (!_webReady || WebView.CoreWebView2 is null)
        {
            return;
        }
        try
        {
            var json = await WebView.CoreWebView2.ExecuteScriptAsync(
                "window.paperTodoGetMarkdown ? window.paperTodoGetMarkdown() : null");
            var markdown = JsonSerializer.Deserialize<string>(json);
            if (markdown is not null && !string.Equals(markdown, _draftMarkdown, StringComparison.Ordinal))
            {
                _draftMarkdown = markdown;
                _dirty = true;
            }
        }
        catch
        {
        }
    }

    private void BeginImageUpload(JsonElement root)
    {
        var requestId = root.GetProperty("requestId").GetString() ?? "";
        var totalChunks = root.GetProperty("totalChunks").GetInt32();
        var fileName = root.TryGetProperty("fileName", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString() ?? "image"
            : "image";
        if (requestId.Length == 0 || totalChunks is <= 0 or > 200)
        {
            throw new InvalidDataException("图片上传参数无效。");
        }

        _imageUploads[requestId] = new ImageUploadSession(totalChunks, fileName);
    }

    private async Task ReceiveImageChunkAsync(JsonElement root)
    {
        var requestId = root.GetProperty("requestId").GetString() ?? "";
        var index = root.GetProperty("index").GetInt32();
        var data = root.GetProperty("data").GetString() ?? "";
        if (!_imageUploads.TryGetValue(requestId, out var session))
        {
            return;
        }

        var bytes = Convert.FromBase64String(data);
        session.Add(index, bytes);
        if (session.Received != session.TotalChunks)
        {
            return;
        }

        _imageUploads.Remove(requestId);
        try
        {
            var prepared = _imageService.Prepare(
                session.GetBytes(),
                autoCompress: _settings.AutoCompressImages);
            var image = await _database.AddNoteImageAsync(
                PaperId,
                prepared.Bytes,
                prepared.Mime,
                prepared.Width,
                prepared.Height,
                _lifetime.Token);
            SendToEditor(new
            {
                type = "imageSaved",
                requestId,
                url = MarkdownImageReferences.LocalImageBase + image.Id,
                altText = Path.GetFileNameWithoutExtension(session.FileName),
            });
        }
        catch (Exception ex)
        {
            SendToEditor(new
            {
                type = "imageError",
                requestId,
                message = ex.Message,
            });
        }
    }

    private void SendToEditor(object payload)
    {
        if (_webReady && WebView.CoreWebView2 is not null)
        {
            WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
    }

    private static void OpenExternalLink(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return;
        }
        if (uri.Scheme is not ("http" or "https" or "mailto"))
        {
            return;
        }
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private async void TitleBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        try
        {
            await _viewModel.SaveTitleAsync(_lifetime.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存标题失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinButton();
        _windowManager.SaveGeometrySoon(this, PaperId);
    }

    private void UpdatePinButton()
    {
        PinButton.Content = Topmost ? "已置顶" : "置顶";
        PinButton.Background = Topmost
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xDA, 0xE8, 0xF6))
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE9, 0xE5, 0xDC));
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        await CaptureCurrentMarkdownAsync();
        var saved = await SaveContentAsync(_lifetime.Token);
        if (!saved)
        {
            var choice = MessageBox.Show(
                this,
                "笔记尚未保存成功。是否继续尝试关闭？未保存内容可能丢失。",
                "PaperTodo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _allowClose = true;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _saveDebounce?.Cancel();
        _saveDebounce?.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
        WebView.Dispose();
    }

    private sealed class ImageUploadSession(int totalChunks, string fileName)
    {
        private readonly byte[]?[] _chunks = new byte[]?[totalChunks];

        public int TotalChunks { get; } = totalChunks;
        public string FileName { get; } = fileName;
        public int Received { get; private set; }

        public void Add(int index, byte[] bytes)
        {
            if (index < 0 || index >= _chunks.Length || _chunks[index] is not null)
            {
                throw new InvalidDataException("图片分块序号无效。");
            }
            _chunks[index] = bytes;
            Received++;
        }

        public byte[] GetBytes()
        {
            if (Received != _chunks.Length || _chunks.Any(chunk => chunk is null))
            {
                throw new InvalidDataException("图片分块不完整。");
            }
            using var stream = new MemoryStream();
            foreach (var chunk in _chunks)
            {
                stream.Write(chunk!);
            }
            return stream.ToArray();
        }
    }
}
