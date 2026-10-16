using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.S3.Services;

using System.Collections.ObjectModel;

namespace FlowLauncher.Plugin.S3.ViewModels;

public partial class MetadataEntryEditor : ObservableObject
{
    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _value = "";
}

public partial class ObjectMetadataViewModel(
    IObjectMetadataService metadataService,
    string bucketName,
    string key) : ObservableObject
{
    private string _expectedETag = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _eTag = "";

    [ObservableProperty]
    private string _contentLength = "";

    [ObservableProperty]
    private string _lastModified = "";

    [ObservableProperty]
    private string _contentType = "";

    [ObservableProperty]
    private string _contentEncoding = "";

    [ObservableProperty]
    private string _cacheControl = "";

    public string Header => $"{bucketName}/{key}";

    public ObservableCollection<MetadataEntryEditor> UserMetadata { get; } = [];

    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusText = "正在读取元数据…";

        try
        {
            var metadata = await metadataService.GetAsync(bucketName, key);
            _expectedETag = metadata.ConcurrencyToken;
            ETag = metadata.ETag;
            ContentLength = metadata.ContentLength.ToString("N0");
            LastModified = metadata.LastModified?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            ContentType = metadata.ContentType ?? "";
            ContentEncoding = metadata.ContentEncoding ?? "";
            CacheControl = metadata.CacheControl ?? "";

            UserMetadata.Clear();
            foreach (var pair in metadata.UserMetadata.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                UserMetadata.Add(new MetadataEntryEditor { Key = pair.Key, Value = pair.Value });

            StatusText = $"已加载 {UserMetadata.Count} 项用户元数据";
        }
        catch (Exception ex)
        {
            StatusText = $"读取失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddEntry() => UserMetadata.Add(new MetadataEntryEditor { Key = "new-key", Value = "" });

    [RelayCommand]
    private void RemoveEntry(MetadataEntryEditor? entry)
    {
        if (entry is not null)
            UserMetadata.Remove(entry);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving)
            return;

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in UserMetadata)
        {
            var key = (entry.Key ?? "").Trim();
            if (key.Length == 0)
            {
                StatusText = "元数据 Key 不能为空";
                return;
            }

            dictionary[key] = entry.Value ?? "";
        }

        IsSaving = true;
        StatusText = "正在保存元数据…";

        try
        {
            await metadataService.UpdateAsync(bucketName, key, dictionary, _expectedETag);
            await LoadAsync();
            StatusText = "元数据已保存";
        }
        catch (ConcurrentObjectChangedException)
        {
            StatusText = "对象已被其他程序修改，请重新加载后再保存";
        }
        catch (MetadataNotSupportedException ex)
        {
            StatusText = $"当前服务不支持元数据改写：{ex.Message}";
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败：{ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
