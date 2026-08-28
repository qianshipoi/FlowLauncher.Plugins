using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using DevToolkit.PortKiller;

using PortProcessInfo = DevToolkit.PortKiller.ProcessHelpers.PortProcessInfo;

using System.Collections.ObjectModel;

namespace DevToolkit.ViewModels;

/// <summary>端口占用页 VM：查询/历史/结束进程（确认对话框由 View 处理）。</summary>
public partial class PortKillerViewModel : ObservableObject
{
    private readonly PortHistoryManager _history;

    public ObservableCollection<PortProcessInfo> Processes { get; } = [];

    public ObservableCollection<string> RecentQueries { get; } = [];

    [ObservableProperty]
    private string _port = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private PortProcessInfo? _selectedProcess;

    [ObservableProperty]
    private bool _isBusy;

    public PortKillerViewModel(PortHistoryManager history)
    {
        _history = history;
        RefreshRecent();
    }

    private void RefreshRecent()
    {
        RecentQueries.Clear();
        foreach (var entry in _history.GetRecent(10))
            RecentQueries.Add(entry.Query);
    }

    [RelayCommand]
    private async Task QueryAsync()
    {
        if (IsBusy)
            return;

        if (!int.TryParse(Port.Trim(), out var port) || port < 1 || port > 65535)
        {
            StatusMessage = "请输入 1 到 65535 的端口号";
            return;
        }

        IsBusy = true;
        Processes.Clear();
        StatusMessage = "查询中…";

        try
        {
            var infos = await Task.Run(() => ProcessHelpers.QueryProcessesByPort(port, CancellationToken.None));
            _history.Add(Port.Trim());
            RefreshRecent();

            foreach (var info in infos)
                Processes.Add(info);

            StatusMessage = infos.Count == 0
                ? $"未找到占用端口 {port} 的进程"
                : $"端口 {port} 共 {infos.Count} 个占用进程，选中后可结束";
        }
        catch (Exception ex)
        {
            StatusMessage = $"查询失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>结束选中进程；返回 null 表示成功，否则为错误信息。</summary>
    public string? KillSelected()
    {
        if (SelectedProcess is not { } info)
        {
            StatusMessage = "请先选中要结束的进程";
            return null;
        }

        try
        {
            Process.GetProcessById(info.ProcessId).Kill(true);
            StatusMessage = $"已结束 {info.ProcessName} (PID:{info.ProcessId})";
            _ = QueryAsync();
            return null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"无法终止进程：{ex.Message}";
            return ex.Message;
        }
    }
}
