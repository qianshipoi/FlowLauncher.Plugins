using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.S3;
using FlowLauncher.Plugin.S3.Models;
using FlowLauncher.Plugin.S3.Services;
using FlowLauncher.Plugin.S3.Transfer;
using FlowLauncher.Plugin.S3.ViewModels;
using FlowLauncher.Plugin.S3.Views;

using Flow.Launcher.Plugin;

using System.Collections.ObjectModel;
using System.IO;

namespace FlowLauncher.Plugin.S3.ViewModel;

public partial class FileBrowserViewModel(IPublicAPI publicApi, S3StorageService service, Settings settings) : ObservableObject
{
    /// <summary>本地化文案（Flow 宿主语言自动选择）。</summary>
    private ILocalization Loc { get; } = new FlowLocalization(publicApi);

    [ObservableProperty]
    private string _bucketName = "";

    [ObservableProperty]
    private string _prefix = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    [NotifyPropertyChangedFor(nameof(SelectedFileAccessUrl))]
    private ObjectInfo? _selectedFile;

    [ObservableProperty]
    private string _statusText = "";

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
                return $"{Loc.Get("browser_get_link_failed")}: {ex.Message}";
            }
        }
    }

    public ObservableCollection<ObjectInfo> Files { get; } = [];

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
            StatusText = Loc.Get("browser_enter_bucket");
            return;
        }

        IsLoading = true;
        StatusText = Loc.Get("browser_loading");
        Files.Clear();

        try
        {
            var (objects, hasMore) = await service.GetObjectsDetailedAsync(BucketName, Prefix);
            foreach (var obj in objects)
            {
                Files.Add(obj);
            }
            StatusText = hasMore
                ? Loc.Get("browser_loaded_capped", Files.Count)
                : Loc.Get("browser_loaded", Files.Count);
        }
        catch (Exception ex)
        {
            StatusText = Loc.Get("browser_load_failed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task HandleDropAsync(string[] filePaths)
    {
        if (string.IsNullOrWhiteSpace(BucketName) || filePaths.Length == 0) return;

        var requests = filePaths.Select(filePath =>
        {
            var fileName = Path.GetFileName(filePath);
            var objectName = string.IsNullOrWhiteSpace(Prefix)
                ? fileName
                : Prefix.TrimEnd('/') + '/' + fileName;
            return new UploadRequest(BucketName, objectName, filePath);
        }).ToList();

        // 进度、取消与链接复制都在传输窗口内完成，宿主隐藏与否都不影响
        var window = TransferWindow.ForUploadBatch(
            service, settings, new FlowTransferHost(publicApi), requests, new FlowLocalization(publicApi));

        // 上传窗口结束后再刷新列表，避免传输中读到旧结果
        window.Closed += (_, _) => _ = LoadObjectsAsync();
        window.Show();
    }

    [RelayCommand]
    private void CopyAccessUrl()
    {
        if (SelectedFile is { IsDirectory: false })
        {
            publicApi.CopyToClipboard(SelectedFileAccessUrl);
            StatusText = Loc.Get("browser_copy_done");
        }
    }

    [RelayCommand]
    private void EditMetadata()
    {
        if (SelectedFile is not { IsDirectory: false } file)
            return;

        var viewModel = new ObjectMetadataViewModel(
            service.CreateMetadataService(),
            BucketName,
            file.Key);
        new ObjectMetadataWindow(viewModel).ShowDialog();
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
