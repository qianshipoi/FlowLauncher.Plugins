using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevToolkit.AliyunOss.Models;
using DevToolkit.AliyunOss.Services;

using System.Collections.ObjectModel;
using System.IO;

namespace DevToolkit.AliyunOss.ViewModels;

/// <summary>OSS 管理页 VM：存储空间加载、对象浏览、上传与下载链接。</summary>
public partial class OssPageViewModel : ObservableObject
{
    private readonly OssService _service;

    public ObservableCollection<string> Buckets { get; } = [];

    public ObservableCollection<OssObjectInfo> Objects { get; } = [];

    [ObservableProperty]
    private string _selectedBucket = "";

    [ObservableProperty]
    private string _prefix = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _uploadProgress;

    [ObservableProperty]
    private OssObjectInfo? _selectedObject;

    public OssPageViewModel(OssService service)
    {
        _service = service;
    }

    partial void OnSelectedBucketChanged(string value) => _ = LoadObjectsAsync();

    [RelayCommand]
    private async Task LoadBucketsAsync()
    {
        IsBusy = true;
        StatusText = "加载存储空间…";
        try
        {
            Buckets.Clear();
            foreach (var bucket in await _service.GetBucketsAsync())
                Buckets.Add(bucket);
            StatusText = $"共 {Buckets.Count} 个存储空间";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadObjectsAsync()
    {
        if (string.IsNullOrEmpty(SelectedBucket))
            return;

        IsBusy = true;
        StatusText = "加载对象…";
        try
        {
            Objects.Clear();
            var (items, _) = await _service.GetObjectsDetailedAsync(SelectedBucket, Prefix);
            foreach (var item in items)
                Objects.Add(item);
            StatusText = $"共 {Objects.Count} 项";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>双击进入目录（以 / 结尾的对象视为目录）。</summary>
    [RelayCommand]
    private void EnterFolder()
    {
        if (SelectedObject is not { } obj || !obj.Key.EndsWith('/'))
            return;
        Prefix = obj.Key;
        _ = LoadObjectsAsync();
    }

    [RelayCommand]
    private void GoUp()
    {
        if (string.IsNullOrEmpty(Prefix))
            return;
        var trimmed = Prefix.TrimEnd('/');
        var idx = trimmed.LastIndexOf('/');
        Prefix = idx >= 0 ? Prefix[..(idx + 1)] : "";
        _ = LoadObjectsAsync();
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (string.IsNullOrEmpty(SelectedBucket))
        {
            StatusText = "请先选择存储空间";
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择要上传的文件" };
        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        UploadProgress = 0;
        StatusText = "上传中…";
        try
        {
            var objectName = Prefix + Path.GetFileName(dialog.FileName);
            var progress = new Progress<Models.TransferProgress>(p => UploadProgress = p.Fraction);
            await _service.UploadFileAsync(SelectedBucket, objectName, dialog.FileName, progress);
            StatusText = $"已上传：{objectName}";
            await LoadObjectsAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"上传失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (SelectedObject is not { } obj)
        {
            StatusText = "请先选择对象";
            return;
        }

        if (string.IsNullOrEmpty(SelectedBucket))
            return;

        try
        {
            var url = await _service.PresignDownloadUrlAsync(SelectedBucket, obj.Key);
            System.Windows.Clipboard.SetText(url);
            StatusText = "下载链接已复制到剪贴板";
        }
        catch (Exception ex)
        {
            StatusText = $"获取链接失败：{ex.Message}";
        }
    }
}
