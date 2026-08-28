using System.IO;
using System.Text.Json;

namespace DevToolkit.Translator;

/// <summary>
/// 翻译记录
/// </summary>
public record TranslationRecord(
    string OriginalText,
    string TranslatedText,
    string FromLanguage,
    string ToLanguage,
    DateTime TranslatedAt
)
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// 翻译历史记录管理器
/// </summary>
public class HistoryManager
{
    private readonly string _historyFilePath;
    private readonly int _maxRecords;
    private readonly Lock _lock = new();
    private List<TranslationRecord> _records = [];

    public HistoryManager(string dataDirectory, int maxRecords = 100)
    {
        _historyFilePath = Path.Combine(dataDirectory, "translation_history.json");
        _maxRecords = maxRecords;
        Load();
    }

    /// <summary>
    /// 添加一条翻译记录
    /// </summary>
    public void Add(string originalText, string translatedText, string fromLanguage, string toLanguage)
    {
        var record = new TranslationRecord(originalText, translatedText, fromLanguage, toLanguage, DateTime.Now);

        lock (_lock)
        {
            // 避免重复添加相同原文
            _records.RemoveAll(r => r.OriginalText == originalText && r.FromLanguage == fromLanguage && r.ToLanguage == toLanguage);
            _records.Insert(0, record);

            // 限制最大记录数
            if (_records.Count > _maxRecords)
            {
                _records = _records.Take(_maxRecords).ToList();
            }
        }

        Save();
    }

    /// <summary>
    /// 获取所有历史记录
    /// </summary>
    public IReadOnlyList<TranslationRecord> GetAll()
    {
        lock (_lock)
        {
            return _records.AsReadOnly();
        }
    }

    /// <summary>
    /// 根据原文精确查找历史记录
    /// </summary>
    public TranslationRecord? FindByOriginalText(string originalText, string fromLanguage, string toLanguage)
    {
        lock (_lock)
        {
            return _records.FirstOrDefault(r =>
                r.OriginalText == originalText &&
                r.FromLanguage == fromLanguage &&
                r.ToLanguage == toLanguage);
        }
    }

    /// <summary>
    /// 按关键字搜索历史记录（匹配原文或译文）
    /// </summary>
    public IReadOnlyList<TranslationRecord> Search(string keyword, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [.. GetAll().Take(limit)];

        lock (_lock)
        {
            return [.. _records
                .Where(r => r.OriginalText.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                         || r.TranslatedText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Take(limit)];
        }
    }

    /// <summary>
    /// 删除指定记录
    /// </summary>
    public bool Remove(string id)
    {
        lock (_lock)
        {
            var removed = _records.RemoveAll(r => r.Id == id) > 0;
            if (removed) Save();
            return removed;
        }
    }

    /// <summary>
    /// 清空所有历史记录
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _records.Clear();
        }
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_historyFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_historyFilePath);
            _records = JsonSerializer.Deserialize<List<TranslationRecord>>(json) ?? [];
        }
        catch
        {
            _records = [];
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFilePath, json);
        }
        catch
        {
            // 静默处理保存失败
        }
    }
}
