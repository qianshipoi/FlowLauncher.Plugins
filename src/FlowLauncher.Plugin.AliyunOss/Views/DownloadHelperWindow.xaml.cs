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

    public DownloadHelperWindow(IPublicAPI publicApi, OssService service, string url)
    {
        InitializeComponent();
        _publicApi = publicApi;
        _service = service;
        _url = url;
        Loaded += OnLoaded;
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
                Close();
                return;
            }

            StatusText.Text = "下载中...";
            await _publicApi.ShowProgressBoxAsync("下载中...", async (reportProgress) =>
            {
                if (File.Exists(dialog.FileName))
                    File.Delete(dialog.FileName);

                await _publicApi.HttpDownloadAsync(_url, dialog.FileName, reportProgress);
            });

            _publicApi.OpenDirectory(_url, dialog.FileName);
        }
        catch (Exception ex)
        {
            _publicApi.ShowMsgError("下载失败", ex.Message);
        }

        Close();
    }
}
