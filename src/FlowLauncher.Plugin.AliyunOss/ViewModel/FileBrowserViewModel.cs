using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevToolkit.AliyunOss;
using DevToolkit.AliyunOss.Models;
using DevToolkit.AliyunOss.Services;

using Flow.Launcher.Plugin;

using System.Collections.ObjectModel;
using System.IO;

namespace FlowLauncher.Plugin.AliyunOss.ViewModel;

public partial class FileBrowserViewModel(IPublicAPI publicApi, OssService service, Settings settings) : ObservableObject
{
    [ObservableProperty]
    private string _bucketName = "";

    [ObservableProperty]
    private string _prefix = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    [NotifyPropertyChangedFor(nameof(SelectedFileAccessUrl))]
    private OssObjectInfo? _selectedFile;

    [ObservableProperty]
    private string _statusText = "请设置桶名和前缀，然后点击加载";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isDragOver;

    public bool AutoCopyLink
    {
        get => settings.AutoCopyLink;
        set
        {
            if (settings.AutoCopyLink != value)
            {
                settings.AutoCopyLink = value;
                publicApi.SaveSettingJsonStorage<Settings>();
                OnPropertyChanged();
            }
        }
    }

    public bool HasSelectedFile => SelectedFile is not null && !SelectedFile.IsDirectory;

    public string SelectedFileAccessUrl
    {
        get
        {
            if (SelectedFile is not { IsDirectory: false } file)
                return "";
            try
            {
                // 访问链接用于下载/查看，必须是 GET 预签名，而非 PUT
                return service.GeneratePresignedDownloadUrl(BucketName, file.Key);
            }
            catch (Exception ex)
            {
                return $"生成链接失败: {ex.Message}";
            }
        }
    }

    public ObservableCollection<OssObjectInfo> Files { get; } = [];

    public void Init(string bucketName, string prefix)
    {
        BucketName = bucketName;
        Prefix = prefix;
        _ = LoadObjectsAsync();
    }

    [RelayCommand]
    private async Task LoadObjectsAsync()
    {
        if (string.IsNullOrWhiteSpace(BucketName))
        {
            StatusText = "请输入桶名";
            return;
        }

        IsLoading = true;
        StatusText = "加载中...";
        Files.Clear();

        try
        {
            var (objects, hasMore) = await service.GetObjectsDetailedAsync(BucketName, Prefix);
            foreach (var obj in objects)
            {
                Files.Add(obj);
            }
            StatusText = hasMore
                ? $"已加载 {Files.Count} 个对象（已达上限，更多数据未显示）"
                : $"已加载 {Files.Count} 个对象";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task HandleDropAsync(string[] filePaths)
    {
        if (string.IsNullOrWhiteSpace(BucketName) || filePaths.Length == 0) return;

        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileName(filePath);
            var objectName = string.IsNullOrWhiteSpace(Prefix)
                ? fileName
                : Prefix.TrimEnd('/') + '/' + fileName;

            using var cts = new CancellationTokenSource();
            var cancelled = false;

            try
            {
                await publicApi.ShowProgressBoxAsync(
                    $"上传 {fileName}...",
                    async reportProgress =>
                    {
                        // FlowLauncher 进度条要求 0-100；OssService 上报的是 0-1
                        await service.UploadFileAsync(
                            BucketName,
                            objectName,
                            filePath,
                            p => reportProgress(p * 100),
                            cts.Token);
                    },
                    cancelProgress: () =>
                    {
                        cancelled = true;
                        try
                        {
                            cts.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    });

                if (cancelled || cts.IsCancellationRequested)
                {
                    StatusText = $"已取消上传: {fileName}";
                    break;
                }

                if (AutoCopyLink)
                {
                    var url = await service.PresignDownloadUrlAsync(BucketName, objectName);
                    publicApi.CopyToClipboard(url);
                    StatusText = $"上传完成，链接已复制: {fileName}";
                }
                else
                {
                    StatusText = $"上传完成: {fileName}";
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = $"已取消上传: {fileName}";
                break;
            }
            catch (Exception ex)
            {
                if (cancelled || cts.IsCancellationRequested)
                {
                    StatusText = $"已取消上传: {fileName}";
                    break;
                }

                StatusText = $"上传失败 {fileName}: {ex.Message}";
            }
        }

        await LoadObjectsAsync();
    }

    [RelayCommand]
    private void CopyAccessUrl()
    {
        if (SelectedFile is { IsDirectory: false })
        {
            publicApi.CopyToClipboard(SelectedFileAccessUrl);
            StatusText = "链接已复制到剪贴板";
        }
    }

    public void NavigateToFolder(string folderPrefix)
    {
        Prefix = folderPrefix;
        _ = LoadObjectsAsync();
    }

    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(Prefix)) return;
        var trimmed = Prefix.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        Prefix = lastSlash >= 0 ? trimmed[..(lastSlash + 1)] : "";
        _ = LoadObjectsAsync();
    }
}