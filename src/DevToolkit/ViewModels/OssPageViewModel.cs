using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using DevToolkit.AliyunOss;

using DevToolkit.AliyunOss.Services;

using DevToolkit.AliyunOss.ViewModel;

using System.Collections.ObjectModel;

using System.IO;

namespace DevToolkit.ViewModels;

/// <summary>OSS 页 VM：设置（复用 Core OssSettingsViewModel）+ 桶/对象浏览 + 复制链接/带进度下载。</summary>
public partial class OssPageViewModel : ObservableObject
{
    private readonly OssService _service;
    private readonly AliyunOss.Settings _settings;

    public OssSettingsViewModel Settings { get; }

    public ObservableCollection<string> Buckets { get; } = [];

    public ObservableCollection<string> Objects { get; } = [];

    [ObservableProperty]
    private string? _selectedBucket;

    [ObservableProperty]
    private string? _selectedObject;

    [ObservableProperty]
    private string _prefix = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    public OssPageViewModel(AliyunOss.Settings settings, Action save)
    {
        _settings = settings;
        Settings = new OssSettingsViewModel(settings, save);
        _service = new OssService(settings);
    }

    [RelayCommand]
    private async Task LoadBucketsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "加载 Buckets…";
        try
        {
            var buckets = await _service.GetBucketsAsync();
            Buckets.Clear();
            foreach (var bucket in buckets)
                Buckets.Add(bucket);
            SelectedBucket = buckets.FirstOrDefault();
            StatusMessage = $"共 {buckets.Count} 个 Bucket";
        }
        catch (SettingsNotConfiguredException)
        {
            StatusMessage = "请先填写并保存 AccessKey 与 Endpoint";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadObjectsAsync()
    {
        if (IsBusy) return;
        if (SelectedBucket is null)
        {
            StatusMessage = "请先加载并选择 Bucket";
            return;
        }

        IsBusy = true;
        StatusMessage = "列出对象…";
        try
        {
            var objects = await _service.GetObjectsAsync(SelectedBucket, Prefix);
            Objects.Clear();
            foreach (var obj in objects)
                Objects.Add(obj);
            StatusMessage = $"共 {objects.Count} 个对象";
        }
        catch (Exception ex)
        {
            StatusMessage = $"列出失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (SelectedBucket is null || SelectedObject is null)
        {
            StatusMessage = "请先选中对象";
            return;
        }

        try
        {
            var url = await _service.PresignDownloadUrlAsync(SelectedBucket, SelectedObject);
            System.Windows.Clipboard.SetText(url);
            StatusMessage = "已复制 6 小时有效的下载链接";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成链接失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (IsDownloading) return;
        if (SelectedBucket is null || SelectedObject is null)
        {
            StatusMessage = "请先选中对象";
            return;
        }

        try
        {
            var url = await _service.PresignDownloadUrlAsync(SelectedBucket, SelectedObject);
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileName(SelectedObject),
            };
            if (dialog.ShowDialog() != true)
                return;

            IsDownloading = true;
            DownloadProgress = 0;
            StatusMessage = $"下载中… {dialog.FileName}";

            await DownloadService.DownloadFileAsync(
                url, dialog.FileName, new Progress<double>(p => DownloadProgress = p));

            StatusMessage = $"下载完成：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"下载失败：{ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }
}
