using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlowLauncher.Plugin.AliyunOss.Utils;

/// <summary>
/// 节流帮助类：限制委托在指定时间间隔内最多执行一次。
/// 支持 Leading（立即执行） 与 Trailing（在间隔结束时执行最后一次调用） 策略。
/// 可传入 SynchronizationContext 以在 UI 线程执行回调（WPF 可传 DispatcherSynchronizationContext.Current）。
/// </summary>
public sealed class Throttler : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly bool _leading;
    private readonly bool _trailing;
    private readonly SynchronizationContext? _syncContext;

    private readonly object _gate = new();
    private DateTime _lastExecutionUtc = DateTime.MinValue;
    private Func<Task>? _pendingWork;
    private CancellationTokenSource _pendingCts = new();
    private bool _disposed;

    public Throttler(TimeSpan interval, bool leading = false, bool trailing = true, SynchronizationContext? syncContext = null)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        if (!leading && !trailing) throw new ArgumentException("At least one of leading or trailing must be true.");

        _interval = interval;
        _leading = leading;
        _trailing = trailing;
        _syncContext = syncContext;
    }

    /// <summary>
    /// 节流同步操作（包装为异步执行）。
    /// </summary>
    public Task ThrottleAsync(Action action) => ThrottleAsync(() => { action(); return Task.CompletedTask; });

    /// <summary>
    /// 节流异步操作。
    /// </summary>
    public Task ThrottleAsync(Func<Task> work)
    {
        if (work is null) throw new ArgumentNullException(nameof(work));
        ThrowIfDisposed();

        TaskCompletionSource<object?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<Task> wrapped = async () =>
        {
            try
            {
                if (_syncContext != null)
                {
                    var doneTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _syncContext.Post(async _ =>
                    {
                        try
                        {
                            await work().ConfigureAwait(false);
                            doneTcs.SetResult(null);
                        }
                        catch (Exception ex)
                        {
                            doneTcs.SetException(ex);
                        }
                    }, null);
                    await doneTcs.Task.ConfigureAwait(false);
                }
                else
                {
                    await work().ConfigureAwait(false);
                }

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        };

        bool shouldExecuteImmediately = false;
        TimeSpan delay = TimeSpan.Zero;

        lock (_gate)
        {
            var now = DateTime.UtcNow;
            var sinceLast = now - _lastExecutionUtc;

            if (sinceLast >= _interval)
            {
                if (_leading)
                {
                    // 立即执行
                    _lastExecutionUtc = now;
                    shouldExecuteImmediately = true;
                }
                else if (_trailing)
                {
                    // 未到执行时间但没有最近执行记录，安排一次延迟执行（间隔后执行）
                    _pendingWork = wrapped;
                    _pendingCts.Cancel();
                    _pendingCts = new CancellationTokenSource();
                    delay = _interval;
                    _ = ScheduleTrailingAsync(delay, _pendingCts.Token);
                    return tcs.Task;
                }
            }
            else
            {
                // 在冷却期内
                if (_trailing)
                {
                    _pendingWork = wrapped;
                    _pendingCts.Cancel();
                    _pendingCts = new CancellationTokenSource();
                    delay = _interval - sinceLast;
                    _ = ScheduleTrailingAsync(delay, _pendingCts.Token);
                }
                else
                {
                    // 忽略调用（既非 leading 也不排队）
                    tcs.SetResult(null);
                }

                return tcs.Task;
            }
        }

        if (shouldExecuteImmediately)
        {
            // 立即执行（不阻塞调用线程）
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_syncContext != null)
                    {
                        var doneTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        _syncContext.Post(async _ =>
                        {
                            try
                            {
                                await work().ConfigureAwait(false);
                                doneTcs.SetResult(null);
                            }
                            catch (Exception ex)
                            {
                                doneTcs.SetException(ex);
                            }
                        }, null);
                        await doneTcs.Task.ConfigureAwait(false);
                    }
                    else
                    {
                        await work().ConfigureAwait(false);
                    }

                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        // fallback（不应该到达）
        return tcs.Task;
    }

    private async Task ScheduleTrailingAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Func<Task>? workToRun = null;
        lock (_gate)
        {
            if (_pendingWork == null)
                return;

            workToRun = _pendingWork;
            _pendingWork = null;
            _lastExecutionUtc = DateTime.UtcNow;
        }

        try
        {
            if (workToRun != null)
                await workToRun().ConfigureAwait(false);
        }
        catch
        {
            // 上层通过返回的 Task 捕获异常；这里不再抛出
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Throttler));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            _pendingCts.Cancel();
            _pendingCts.Dispose();
            _pendingWork = null;
        }
    }
}