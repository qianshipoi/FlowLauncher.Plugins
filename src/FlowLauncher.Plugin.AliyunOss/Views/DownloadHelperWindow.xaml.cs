using Flow.Launcher.Plugin;

using DevToolkit.AliyunOss.Services;

using Microsoft.Win32;

using System.IO;
using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Views;

public partial class DownloadHelperWindow : Window
{
    private readonly IPublicAPI _publicApi;
    private readonly OssService _service;
    private readonly string _url;
    private readonly CancellationTokenSource _cts = new();
    private bool _cancelled;

    public DownloadHelperWindow(IPublicAPI publicApi, OssService service, string url)
    {
        InitializeComponent();
        _publicApi = publicApi;
        _service = service;
        _url = url;
        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CancelCore();
        _cts.Dispose();
    }

    private void CancelCore()
    {
        _cancelled = true;
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "请选择保存位置...";
            var dialog = new SaveFileDialog
            {
                FileName = Path.GetFileName(new Uri(_url).AbsolutePath),
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            StatusText.Text = "下载中...";
            await _publicApi.ShowProgressBoxAsync(
                "下载中...",
                async reportProgress =>
                {
                    if (File.Exists(dialog.FileName))
                        File.Delete(dialog.FileName);

                    await _publicApi.HttpDownloadAsync(
                        _url,
                        dialog.FileName,
                        reportProgress,
                        _cts.Token);
                },
                cancelProgress: () => CancelCore());

            if (_cancelled || _cts.IsCancellationRequested)
            {
                StatusText.Text = "已取消下载";
                return;
            }

            _publicApi.OpenDirectory(_url, dialog.FileName);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "已取消下载";
        }
        catch (Exception ex)
        {
            if (!_cancelled && !_cts.IsCancellationRequested)
            {
                _publicApi.ShowMsgError("下载失败", ex.Message);
            }
            else
            {
                StatusText.Text = "已取消下载";
            }
        }
        finally
        {
            Close();
        }
    }
}
