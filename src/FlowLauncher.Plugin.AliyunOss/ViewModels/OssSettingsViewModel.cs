using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlowLauncher.Plugin.AliyunOss.Services;

namespace FlowLauncher.Plugin.AliyunOss.ViewModel;

/// <summary>
/// AccessKey 设置：ID 明文显示；Secret 用密码框且不回显已存值。
/// Secret 留空表示不修改，填入才更新；「测试」用当前填写值（或已存值）ListBuckets。
/// </summary>
public partial class OssSettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Action _onChanged;

    public OssSettingsViewModel(Settings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;
        _accessKeyId = settings.GetPlainAccessKeyId();
        _endpoint = settings.Endpoint;
        _computeFileMd5 = settings.ComputeFileMd5;
        RefreshSecretHint();
    }

    /// <summary>AccessKey ID，明文显示、可直接编辑。</summary>
    [ObservableProperty]
    private string _accessKeyId;

    /// <summary>新 Secret 输入；不回显已存值，留空则保存时保持不变。</summary>
    [ObservableProperty]
    private string _accessKeySecretInput = "";

    [ObservableProperty]
    private string _endpoint;

    /// <summary>
    /// 上传时计算文件 MD5 并写入对象元数据。默认关闭：算哈希要把整个文件多读一遍，
    /// 只有下游靠该指纹识别文件时才需要。
    /// </summary>
    [ObservableProperty]
    private bool _computeFileMd5;

    [ObservableProperty]
    private string _accessKeySecretHint = "";

    [ObservableProperty]
    private string _testStatus = "";

    [ObservableProperty]
    private bool _isTesting;

    private void RefreshSecretHint()
    {
        AccessKeySecretHint = string.IsNullOrEmpty(_settings.GetPlainAccessKeySecret())
            ? "尚未配置"
            : "已配置，留空则不修改";
    }

    /// <summary>解析本次操作使用的 Secret：输入框为空则沿用已存值。</summary>
    private string ResolveSecret()
    {
        return string.IsNullOrWhiteSpace(AccessKeySecretInput)
            ? _settings.GetPlainAccessKeySecret()
            : AccessKeySecretInput;
    }

    private static string NormalizeEndpoint(string? endpoint)
    {
        var value = (endpoint ?? "").Trim();
        if (value.Length == 0) return "";
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }
        return value;
    }

    [RelayCommand]
    private void Save()
    {
        var secret = string.IsNullOrWhiteSpace(AccessKeySecretInput) ? null : AccessKeySecretInput;
        _settings.SetSecrets(AccessKeyId ?? "", secret);
        _settings.Endpoint = NormalizeEndpoint(Endpoint);
        _settings.ComputeFileMd5 = ComputeFileMd5;
        _onChanged();

        AccessKeySecretInput = "";
        RefreshSecretHint();
        TestStatus = "已保存";
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        if (IsTesting) return;

        var endpoint = NormalizeEndpoint(Endpoint);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            TestStatus = "请先填写 Endpoint";
            return;
        }

        var id = (AccessKeyId ?? "").Trim();
        var secret = ResolveSecret();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
        {
            TestStatus = "请先填写 AccessKey ID 与 Secret（或此前已保存过 Secret）";
            return;
        }

        // 用临时配置做连通测试，不落盘
        var probe = new Settings();
        probe.SetSecrets(id, secret);
        probe.Endpoint = endpoint;

        IsTesting = true;
        TestStatus = "正在连接并列出存储空间…";
        try
        {
            var service = new OssService(probe);
            var buckets = await service.GetBucketsAsync();
            TestStatus = $"连接成功，共 {buckets.Count} 个存储空间";
        }
        catch (Exception ex)
        {
            TestStatus = $"连接失败：{ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
