using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

namespace FlowLauncher.Plugin.VarName;

/// <summary>
/// 变量名设置 VM（宿主无关）：API Key 不回显，留空保持不变；保存通过回调持久化，成功后触发 <see cref="ReQueryRequested"/>。
/// </summary>
public partial class SettingsViewModel(Settings settings, Action onChanged) : ObservableObject
{
    [ObservableProperty]
    private string _modelName = settings.ModelName;
    [ObservableProperty]
    private string _apiUrl = settings.ApiUrl;

    /// <summary>新 API Key 输入；不回显已存值，留空则保存时保持不变。</summary>
    [ObservableProperty]
    private string _apiKeyInput = "";

    [ObservableProperty]
    private string _apiKeyHint = string.IsNullOrEmpty(settings.GetPlainApiKey())
        ? "尚未配置"
        : "已配置，留空则不修改";

    [ObservableProperty]
    private string _validationMessage = string.Empty;
    [ObservableProperty]
    private bool _isBusy;
    [ObservableProperty]
    private List<string> _modelList = [];
    [ObservableProperty]
    private bool _isModelListOpen;

    /// <summary>本次操作使用的明文 Key：输入框为空则沿用已存值。</summary>
    private string ResolveApiKey() =>
        string.IsNullOrWhiteSpace(ApiKeyInput) ? settings.GetPlainApiKey() : ApiKeyInput;

    public bool IsFormValid =>
        !string.IsNullOrWhiteSpace(ModelName) &&
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(ResolveApiKey());

    public bool CanSubmit => IsFormValid && !IsBusy;

    /// <summary>保存后触发，宿主可据此刷新界面。</summary>
    public event Action? ReQueryRequested;

    partial void OnModelNameChanged(string value) => NotifyValidationState();
    partial void OnApiUrlChanged(string value) => NotifyValidationState();
    partial void OnApiKeyInputChanged(string value) => NotifyValidationState();
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));

    private void NotifyValidationState()
    {
        OnPropertyChanged(nameof(IsFormValid));
        OnPropertyChanged(nameof(CanSubmit));
        ValidationMessage = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        if (!IsFormValid)
        {
            ValidationMessage = "请填写所有必填字段后再保存。";
            return;
        }

        settings.ModelName = ModelName;
        settings.ApiUrl = ApiUrl;
        // 留空 = 保留已存 Key
        settings.SetApiKey(string.IsNullOrWhiteSpace(ApiKeyInput) ? null : ApiKeyInput);
        onChanged();

        ApiKeyInput = "";
        ApiKeyHint = string.IsNullOrEmpty(settings.GetPlainApiKey())
            ? "尚未配置"
            : "已配置，留空则不修改";
        ValidationMessage = "配置已保存。";
        ReQueryRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        if (!CanSubmit)
        {
            if (IsBusy) return;
            ValidationMessage = "请先填写所有必填字段再验证。";
            return;
        }

        IsBusy = true;
        ValidationMessage = "正在验证中...";

        try
        {
            var client = new OpenAI.Chat.ChatClient(
                ModelName,
                new System.ClientModel.ApiKeyCredential(ResolveApiKey()),
                new OpenAI.OpenAIClientOptions { Endpoint = new Uri(NormalizeUrl(ApiUrl)), NetworkTimeout = TimeSpan.FromSeconds(10) });

            await client.CompleteChatAsync(
                [new OpenAI.Chat.SystemChatMessage("You are checking API availability."), new OpenAI.Chat.UserChatMessage("Ping")],
                options: new OpenAI.Chat.ChatCompletionOptions { MaxOutputTokenCount = 1 });

            ValidationMessage = "验证成功，API 响应正常。";
        }
        catch (Exception ex)
        {
            ValidationMessage = $"验证异常：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task FetchModelsAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiUrl) || string.IsNullOrWhiteSpace(ResolveApiKey()))
        {
            ValidationMessage = "请先填写 API 地址和 API Key。";
            return;
        }

        IsBusy = true;
        ValidationMessage = "正在获取模型列表...";

        try
        {
            var probe = new Settings { ApiUrl = ApiUrl, ModelName = ModelName };
            probe.SetApiKey(ResolveApiKey());
            var service = new VarNameService(probe);
            var models = await service.GetModelsAsync(CancellationToken.None);
            ModelList = models;
            IsModelListOpen = true;
            ValidationMessage = $"获取到 {models.Count} 个模型。";
        }
        catch (Exception ex)
        {
            ValidationMessage = $"获取模型列表失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string NormalizeUrl(string url)
    {
        if (!url.EndsWith('/')) url += '/';
        return url;
    }
}
