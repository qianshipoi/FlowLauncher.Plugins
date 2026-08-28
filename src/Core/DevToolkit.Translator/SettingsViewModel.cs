using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using OpenAI;

using OpenAI.Chat;

using System.ClientModel;

namespace DevToolkit.Translator;

/// <summary>
/// 翻译设置 VM（宿主无关）：保存通过 <paramref name="onChanged"/> 回调持久化，
/// 保存成功后触发 <see cref="ReQueryRequested"/> 由宿主自行处理（插件转发 FL ReQuery，App 刷新页面）。
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

    public bool IsFormValid =>
        !string.IsNullOrWhiteSpace(ModelName) &&
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public bool CanSubmit => IsFormValid && !IsBusy;

    /// <summary>保存后触发，宿主可据此刷新界面。</summary>
    public event Action? ReQueryRequested;

    partial void OnModelNameChanged(string value)
    {
        NotifyValidationState();
    }

    partial void OnApiUrlChanged(string value)
    {
        NotifyValidationState();
    }

    partial void OnApiKeyChanged(string value)
    {
        NotifyValidationState();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
    }

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
            if (IsBusy)
            {
                return;
            }

            ValidationMessage = "请先填写所有必填字段再验证。";
            return;
        }

        IsBusy = true;
        ValidationMessage = "正在验证中...";

        try
        {
            var client = new ChatClient(
            ModelName,
            new ApiKeyCredential(ApiKey),
            new OpenAIClientOptions()
            {
                Endpoint = new Uri(ApiUrl),
                NetworkTimeout = TimeSpan.FromSeconds(10),
            });

            var result = await client.CompleteChatAsync(
                [new SystemChatMessage("You are checking API availability."), new UserChatMessage("Ping")],
                options: new ChatCompletionOptions { MaxOutputTokenCount = 1 });

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
}
