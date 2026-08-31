using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevToolkit.SpeechToText;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Action _onChanged;

    public SettingsViewModel(Settings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;
        _modelName = settings.ModelName;
        _apiUrl = settings.ApiUrl;
        _apiKey = settings.ApiKey;
        _language = settings.Language;
        _sampleRate = settings.SampleRate;
        _enableRealtimeTranscription = settings.EnableRealtimeTranscription;
        _realtimeIntervalMs = settings.RealtimeIntervalMs;
    }

    [ObservableProperty] private string _modelName;
    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private string _apiKey;
    [ObservableProperty] private string _language;
    [ObservableProperty] private int _sampleRate;
    [ObservableProperty] private bool _enableRealtimeTranscription;
    [ObservableProperty] private int _realtimeIntervalMs;
    [ObservableProperty] private string _connectionStatus = "";

    [RelayCommand]
    private void Save()
    {
        _settings.ModelName = ModelName;
        _settings.ApiUrl = ApiUrl;
        _settings.ApiKey = ApiKey;
        _settings.Language = Language;
        _settings.SampleRate = SampleRate;
        _settings.EnableRealtimeTranscription = EnableRealtimeTranscription;
        // 间隔过低会导致请求堆积，这里做下限保护
        _settings.RealtimeIntervalMs = RealtimeIntervalMs < 500 ? 500 : RealtimeIntervalMs;
        _onChanged();
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        Save();
        var service = new SpeechToTextService(_settings);
        service.ApplyAuthHeaders();
        ConnectionStatus = "测试中…";
        var ok = await service.TestConnectionAsync();
        ConnectionStatus = ok ? "连接成功 ✓" : "连接失败 ✗";
    }
}
