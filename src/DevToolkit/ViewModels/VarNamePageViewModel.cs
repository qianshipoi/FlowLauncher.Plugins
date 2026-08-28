using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using DevToolkit.VarName;

using System.Collections.ObjectModel;

namespace DevToolkit.ViewModels;

/// <summary>变量名页 VM：生成候选 → 选择后展开命名规范变体 → 复制。</summary>
public partial class VarNamePageViewModel : ObservableObject
{
    private readonly VarName.Settings _settings;
    private readonly VarNameService _service;
    private readonly VarNameCache _cache;

    public ObservableCollection<string> Suggestions { get; } = [];

    public ObservableCollection<(string Label, string Value)> Variants { get; } = [];

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public VarNamePageViewModel(string dataDirectory, VarName.Settings settings)
    {
        _settings = settings;
        _service = new VarNameService(settings);
        _cache = new VarNameCache(dataDirectory);
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsBusy)
            return;

        var keyword = Input.Trim();
        if (keyword.Length == 0)
        {
            StatusMessage = "请输入中文描述，如：用户管理";
            return;
        }

        if (!_settings.IsValid())
        {
            StatusMessage = "请先在设置中填写 API（设置页 → 变量名）";
            return;
        }

        IsBusy = true;
        StatusMessage = "AI 生成中…";

        try
        {
            var suggestions = await _service.GenerateAsync(keyword, CancellationToken.None);
            if (suggestions.Count == 0)
            {
                StatusMessage = "未生成结果，请换一种描述";
                return;
            }

            _cache.Set(keyword, suggestions);
            ShowSuggestions(keyword, suggestions, fromCache: false);
            StatusMessage = "完成，选择一个候选词查看命名规范变体";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var keyword = Input.Trim();
        if (keyword.Length == 0)
            return;
        _cache.Remove(keyword);
        await GenerateAsync();
    }

    public void ShowCachedOrPrompt(string keyword)
    {
        var cached = _cache.Get(keyword);
        if (cached is not null)
        {
            ShowSuggestions(keyword, cached, fromCache: true);
            StatusMessage = "（缓存）选择一个候选词查看命名规范变体";
        }
    }

    private void ShowSuggestions(string keyword, List<string> suggestions, bool fromCache)
    {
        Suggestions.Clear();
        Variants.Clear();

        var sorted = fromCache
            ? [.. suggestions.OrderByDescending(s => s.Equals(_cache.GetLastSelectedValue(keyword), StringComparison.OrdinalIgnoreCase))]
            : suggestions;

        foreach (var suggestion in sorted)
            Suggestions.Add(suggestion);
    }

    /// <summary>选择候选词：展示命名规范变体（按上次选中的规范排序）。</summary>
    public void SelectSuggestion(string suggestion)
    {
        var keyword = Input.Trim();
        _cache.SetLastSelectedValue(keyword, suggestion);

        Variants.Clear();
        var variants = NamingStyleConverter.Variants(suggestion);
        var lastConvention = _cache.GetLastNamingConvention();
        foreach (var variant in string.IsNullOrEmpty(lastConvention)
            ? variants
            : [.. variants.OrderByDescending(v => v.Label.Equals(lastConvention, StringComparison.OrdinalIgnoreCase))])
        {
            Variants.Add(variant);
        }

        StatusMessage = $"\"{suggestion}\" 的命名规范变体，点击复制";
    }

    public void CopyVariant((string Label, string Value) variant)
    {
        System.Windows.Clipboard.SetText(variant.Value);
        _cache.SetLastNamingConvention(variant.Label);
        StatusMessage = $"已复制 {variant.Label}：{variant.Value}";
    }
}
