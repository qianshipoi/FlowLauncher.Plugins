using Flow.Launcher.Plugin;

using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.Services;

using Microsoft.Win32;

using System.IO;
using System.Windows;

namespace FlowLauncher.Plugin.AliyunOss.Views;

public partial class UploadHelperWindow : Window
{
    private readonly IPublicAPI _publicApi;
    private readonly OssService _service;
    private readonly string _bucket;
    private readonly string _key;
    private readonly bool _checkExists;
    private readonly bool _forceBackup;
    private readonly bool _tempUpload;
    private readonly Settings _settings;

    public UploadHelperWindow(IPublicAPI publicApi, OssService service, string bucket, string key,
        bool checkExists = false, bool forceBackup = false, bool tempUpload = false, Settings? settings = null)
    {
        InitializeComponent();
        _publicApi = publicApi;
        _service = service;
        _bucket = bucket;
        _key = key;
        _checkExists = checkExists;
        _forceBackup = forceBackup;
        _tempUpload = tempUpload;
        _settings = settings ?? new Settings();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_forceBackup)
            {
                StatusText.Text = "备份中...";
                var backupKey = $"{_key}.bak.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                await _service.CopyObjectAsync(_bucket, _key, _bucket, backupKey, CancellationToken.None);
            }
            else if (_checkExists)
            {
                var exists = await _service.ExistsAsync(_bucket, _key);
                if (exists)
                {
                    StatusText.Text = "文件已存在，请选择操作";

                    var existsDialog = new FileExistsDialog(_key);
                    if (existsDialog.ShowDialog() != true)
                    {
                        Close();
                        return;
                    }

                    if (existsDialog.Result == FileExistsDialog.Choice.BackupAndReplace)
                    {
                        var backupKey = $"{_key}.bak.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                        await _service.CopyObjectAsync(_bucket, _key, _bucket, backupKey, CancellationToken.None);
                    }
                    // Choice.Replace → 直接替换，继续上传
                }
            }

            StatusText.Text = "请选择文件...";
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                DefaultExt = _tempUpload ? null : Path.GetExtension(_key),
                Filter = _tempUpload ? "所有文件|*.*" : GetFilter(_key),
            };
            if (dialog.ShowDialog() != true)
            {
                Close();
                return;
            }

            var filePath = dialog.FileName;
            var uploadKey = _tempUpload
                ? $"tmp/{Guid.NewGuid():N}{Path.GetExtension(filePath)}"
                : _key;

            StatusText.Text = "上传中...";
            await _publicApi.ShowProgressBoxAsync("上传中...", async (reportProgress) =>
            {
                await _service.UploadFileAsync(_bucket, uploadKey, filePath, reportProgress);
            });

            var url = await _service.PresignDownloadUrlAsync(_bucket, uploadKey);

            if (_settings.AutoCopyLink)
            {
                _publicApi.CopyToClipboard(url);
                StatusText.Text = "上传完毕，链接已复制";
            }
            else
            {
                var result = _publicApi.ShowMsgBox("上传完毕，是否复制访问链接", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _publicApi.CopyToClipboard(url);
                }
            }
        }
        catch (Exception ex)
        {
            _publicApi.ShowMsgError("上传失败", ex.Message);
        }

        Close();
    }

    private static string GetFilter(string key)
    {
        var ext = Path.GetExtension(key);
        return string.IsNullOrWhiteSpace(ext) ? "所有文件|*.*" : $"{ext} 文件|*{ext}|所有文件|*.*";
    }
}
