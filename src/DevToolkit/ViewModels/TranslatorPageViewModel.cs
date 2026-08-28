using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using DevToolkit.Translator;

using System.Collections.ObjectModel;

namespace DevToolkit.ViewModels;

/// <summary>翻译页 VM：设置（复用 Core SettingsViewModel）+ 翻译 + 历史记录。</summary>
public partial class TranslatorPageViewModel : ObservableObject
{
    private readonly Translator.Settings _settings;
    private readonly ITranslator _translator;
    private readonly HistoryManager _history;

    public SettingsViewModel Settings { get; }

    public ObservableCollection<TranslationRecord> History { get; } = [];

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public TranslatorPageViewModel(string dataDirectory, Translator.Settings settings, Action save, Action requery)
    {
        _settings = settings;
        Settings = new SettingsViewModel(settings, save);
        Settings.ReQueryRequested += requery;
        _translator = new AiTanslator(settings);
        _history = new HistoryManager(dataDirectory);
        RefreshHistory();
    }

    [RelayCommand]
    private async Task TranslateAsync()
    {
        if (IsBusy)
            return;

        var text = Input.Trim();
        if (text.Length == 0)
        {
            StatusMessage = "请输入要翻译的文本";
            return;
        }

        if (!_settings.IsValid())
        {
            StatusMessage = "请先在上方填写并保存 API 设置";
            return;
        }

        IsBusy = true;
        StatusMessage = "翻译中…";

        try
        {
            var cached = _history.FindByOriginalText(text, "auto", "en");
            Result = cached?.TranslatedText ?? await _translator.TranslateAsync(text, "auto", "en", CancellationToken.None);
            if (cached is null)
                _history.Add(text, Result, "auto", "en");
            RefreshHistory();
            StatusMessage = cached is null ? "完成" : "来自历史记录";
        }
        catch (Exception ex)
        {
            StatusMessage = $"翻译失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyResult()
    {
        if (Result.Length == 0)
            return;
        System.Windows.Clipboard.SetText(Result);
        StatusMessage = "已复制翻译结果";
    }

    [RelayCommand]
    private void CopyHistory(TranslationRecord record)
    {
        System.Windows.Clipboard.SetText(record.TranslatedText);
        StatusMessage = "已复制历史译文";
    }

    [RelayCommand]
    private void RemoveHistory(TranslationRecord record)
    {
        _history.Remove(record.Id);
        RefreshHistory();
        StatusMessage = "已删除历史记录";
    }

    public void RefreshHistory()
    {
        History.Clear();
        foreach (var record in _history.GetAll().Take(10))
            History.Add(record);
    }
}
