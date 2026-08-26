using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FlowLauncher.Plugin.PortKiller;

internal sealed class PortHistoryEntry
{
    public string Query { get; init; }
    public DateTime Timestamp { get; init; }

    public PortHistoryEntry(string query)
    {
        Query = query;
        Timestamp = DateTime.Now;
    }
}

internal sealed class PortHistoryManager
{
    private readonly string _historyFilePath;
    private readonly int _maxEntries;
    private readonly object _lock = new();
    private List<PortHistoryEntry> _entries = [];

    public PortHistoryManager(string pluginDirectory, int maxEntries = 20)
    {
        _historyFilePath = Path.Combine(pluginDirectory, "port_history.json");
        _maxEntries = maxEntries;
        Load();
    }

    public IReadOnlyList<PortHistoryEntry> GetRecent(int limit)
    {
        lock (_lock)
        {
            return _entries.Take(limit).ToList();
        }
    }

    public void Add(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        lock (_lock)
        {
            _entries.RemoveAll(x => string.Equals(x.Query, query, StringComparison.OrdinalIgnoreCase));
            _entries.Insert(0, new PortHistoryEntry(query));
            if (_entries.Count > _maxEntries)
            {
                _entries = _entries.Take(_maxEntries).ToList();
            }
        }

        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_historyFilePath);
            var list = JsonSerializer.Deserialize<List<PortHistoryEntry>>(json);
            if (list != null)
            {
                _entries = list;
            }
        }
        catch
        {
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

            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFilePath, json);
        }
        catch
        {
        }
    }
}
