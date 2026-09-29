using System.IO;
using System.Text.Json;

namespace FlowLauncher.Plugin.SpeechToText;

/// <summary>
/// 转写历史持久化管理（JSON 文件）。
/// </summary>
public class TranscriptionHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly List<TranscriptionRecord> _records = [];
    private readonly object _lock = new();

    public TranscriptionHistory(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "stt-history.json");
        Load();
    }

    public IReadOnlyList<TranscriptionRecord> GetAll()
    {
        lock (_lock) return _records.AsReadOnly();
    }

    public IReadOnlyList<TranscriptionRecord> GetRecent(int count = 50)
    {
        lock (_lock) return _records.TakeLast(count).Reverse().ToList().AsReadOnly();
    }

    public void Add(TranscriptionRecord record)
    {
        lock (_lock)
        {
            _records.Add(record);
            Save();
        }
    }

    public void Remove(string id)
    {
        lock (_lock)
        {
            _records.RemoveAll(r => r.Id == id);
            Save();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _records.Clear();
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var records = JsonSerializer.Deserialize<List<TranscriptionRecord>>(json, JsonOptions);
            if (records is not null)
            {
                lock (_lock) _records.AddRange(records);
            }
        }
        catch
        {
            // 历史文件损坏时忽略
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_records, JsonOptions));
        }
        catch
        {
            // 写入失败时忽略
        }
    }
}
