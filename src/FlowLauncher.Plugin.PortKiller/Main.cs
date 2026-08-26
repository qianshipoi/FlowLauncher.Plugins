using Flow.Launcher.Plugin;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace FlowLauncher.Plugin.PortKiller;

public class Main : IAsyncPlugin
{
    private PluginInitContext _context = default!;
    private IPublicAPI _publicAPI = default!;
    private PortHistoryManager _history = default!;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _publicAPI = _context.API;
        _history = new PortHistoryManager(context.CurrentPluginMetadata.PluginDirectory);
        return Task.CompletedTask;
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var rawInput = query.Search?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            var records = _history.GetRecent(10);
            if (records.Count == 0)
            {
                return
                [
                    new Result
                    {
                        Title = "输入端口号查询占用进程",
                        SubTitle = "例如: 80",
                        Action = _ =>
                        {
                            _context.API.ChangeQuery($"{query.ActionKeyword} 80", true);
                            return false;
                        }
                    }
                ];
            }

            return [.. records
                .Select(entry => new Result
                {
                    Title = $"历史查询端口 {entry.Query}",
                    SubTitle = entry.Timestamp.ToString("g"),
                    Action = _ =>
                    {
                        _context.API.ChangeQuery($"{query.ActionKeyword} {entry.Query}", true);
                        return false;
                    }
                })];
        }

        if (!int.TryParse(rawInput, out var port) || port < 1 || port > 65535)
        {
            return
            [
                new Result
                {
                    Title = "端口号无效",
                    SubTitle = "请输入 1 到 65535 的数字",
                    Action = _ => true
                }
            ];
        }

        List<ProcessHelpers.PortProcessInfo> infos;
        try
        {
            infos = await Task.Run(() => ProcessHelpers.QueryProcessesByPort(port, token), token);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            _context.API.ShowMsgError("查询失败", ex.Message);
            return [];
        }

        _history.Add(rawInput);

        if (infos.Count == 0)
        {
            return
            [
                new Result
                {
                    Title = $"未找到占用端口 {port} 的进程",
                    SubTitle = "可能没有进程在监听此端口",
                    Action = _ => true
                }
            ];
        }

        return [.. infos
            .Select(info => new Result
            {
                IcoPath = info.IconPath,
                Title = $"{info.ProcessName} ({info.ProcessId})",
                SubTitle = $"{info.Protocol} {info.LocalEndPoint} → {info.RemoteEndPoint}  状态: {info.State}",
                Action = _ => KillProcess(info)
            })];
    }

    private bool KillProcess(ProcessHelpers.PortProcessInfo info)
    {
        var confirm = _publicAPI.ShowMsgBox(
            $"确定结束 {info.ProcessName} (PID:{info.ProcessId}) 监听 {info.LocalEndPoint} 的进程吗？",
            "确认结束进程",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return false;

        try
        {
            var process = Process.GetProcessById(info.ProcessId);
            process.Kill(true);
            return true;
        }
        catch (Exception ex)
        {
            _context.API.ShowMsgError("无法终止进程", ex.Message);
            return false;
        }
    }
}
