using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

namespace DevToolkit.VarName;

/// <summary>
/// 变量名设置 VM（宿主无关）：保存通过回调持久化，成功后触发 <see cref="ReQueryRequested"/>。
/// </summary>
public partial class SettingsViewModel(Settings settings, Action onChanged) : ObservableObject
{
    [ObservableProperty]
    private string _modelName = settings.ModelName;
    [ObservableProperty]
    private string _apiUrl = settings.ApiUrl;
    [ObservableProperty]
    private string _apiKey = settings.ApiKey;
    [ObservableProperty]
    private string _validationMessage = string.Empty;
    [ObservableProperty]
    private bool _isBusy;
    [ObservableProperty]
    private List<string> _modelList = [];
    [ObservableProperty]
    private bool _isModelListOpen;

    public bool IsFormValid =>
        !string.IsNullOrWhiteSpace(ModelName) &&
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public bool CanSubmit => IsFormValid && !IsBusy;

    /// <summary>保存后触发，宿主可据此刷新界面。</summary>
    public event Action? ReQueryRequested;

    partial void OnModelNameChanged(string value) => NotifyValidationState();
    partial void OnApiUrlChanged(string value) => NotifyValidationState();
    partial void OnApiKeyChanged(string value) => NotifyValidationState();
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
        settings.ApiKey = ApiKey;
        onChanged();
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
                new System.ClientModel.ApiKeyCredential(ApiKey),
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
        if (string.IsNullOrWhiteSpace(ApiUrl) || string.IsNullOrWhiteSpace(ApiKey))
        {
            ValidationMessage = "请先填写 API 地址和 API Key。";
            return;
        }

        IsBusy = true;
        ValidationMessage = "正在获取模型列表...";

        try
        {
            var service = new VarNameService(new Settings { ApiUrl = ApiUrl, ApiKey = ApiKey, ModelName = ModelName });
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
