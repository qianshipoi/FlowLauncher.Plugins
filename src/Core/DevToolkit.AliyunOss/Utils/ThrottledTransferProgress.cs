using DevToolkit.AliyunOss.Models;

namespace DevToolkit.AliyunOss.Utils;

/// <summary>
/// 进度上报的时间窗节流：只让间隔够了样本投递到下游（通常是 UI 线程），其余丢弃。
/// <para>
/// OSS SDK 按数据块回调（实测 128MB 就有 1225 次），而 <c>Progress&lt;T&gt;</c> 每次
/// <c>Report</c> 都会 <c>Post</c> 一条消息到 UI 线程。大文件下这个量级会塞满消息队列，
/// 让气泡的重绘与鼠标响应排在后面，看起来就是"拖大文件卡住 UI"。
/// 在投递前过滤掉的成本只是一次加锁比较，代价远低于跨线程投递。
/// </para>
/// <para>
/// 结束样本（已传满总量）无条件放行：它承载最终百分比，丢了界面会停在最后一个中间值。
/// 可由 SDK 的回调线程直接调用。
/// </para>
/// </summary>
public sealed class ThrottledTransferProgress(IProgress<TransferProgress> inner, TimeSpan interval) : IProgress<TransferProgress>
{
    private readonly IProgress<TransferProgress> _inner = inner;
    private readonly long _intervalTicks = (long)(interval.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
    private readonly Lock _gate = new();

    private long _lastReportTicks;

    public void Report(TransferProgress progress)
    {
        if (!IsFinal(progress))
        {
            lock (_gate)
            {
                var now = System.Diagnostics.Stopwatch.GetTimestamp();

                // 首个样本一定放行：否则要等到窗口走完才有第一次进度，起步像卡住
                if (_lastReportTicks != 0 && now - _lastReportTicks < _intervalTicks) return;

                _lastReportTicks = now;
            }
        }

        _inner.Report(progress);
    }

    /// <summary>
    /// 清空节流窗口。切换到下一个文件时调用，让新文件的首个样本立即投递，
    /// 否则会沿用上一个文件的时间戳而把它的起步进度压掉。
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _lastReportTicks = 0;
        }
    }

    private static bool IsFinal(TransferProgress progress) =>
        progress.Total > 0 && progress.Transferred >= progress.Total;
}
