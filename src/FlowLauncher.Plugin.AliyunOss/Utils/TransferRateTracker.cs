using FlowLauncher.Plugin.AliyunOss.Models;

namespace FlowLauncher.Plugin.AliyunOss.Utils;

/// <summary>
/// 传输速率与剩余时间估算：滑动窗口采样，避免瞬时抖动导致数字乱跳。
/// 线程安全，可由 SDK 的上传回调线程直接调用。
/// </summary>
public sealed class TransferRateTracker
{
    private readonly TimeSpan _window;
    private readonly TimeSpan _minSampleInterval;
    private readonly Queue<(DateTimeOffset Time, long Bytes)> _samples = new(32);
    private readonly Lock _gate = new();

    private long _transferred;
    private long _total;

    public TransferRateTracker(TimeSpan? window = null, TimeSpan? minSampleInterval = null)
    {
        _window = window ?? TimeSpan.FromSeconds(3);
        _minSampleInterval = minSampleInterval ?? TimeSpan.FromMilliseconds(150);
        if (_window < _minSampleInterval)
            throw new ArgumentException("窗口不能小于采样间隔。", nameof(window));
    }

    public void Report(TransferProgress progress, DateTimeOffset now)
    {
        lock (_gate)
        {
            _transferred = progress.Transferred;
            if (progress.Total > 0) _total = progress.Total;

            // 采样过密则丢弃，样本量由窗口长度与采样间隔决定，不会无限增长
            if (_samples.Count > 0 && now - _samples.Last().Time < _minSampleInterval)
                return;

            _samples.Enqueue((now, progress.Transferred));
            while (_samples.Count > 2 && now - _samples.Peek().Time > _window)
                _samples.Dequeue();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _samples.Clear();
            _transferred = 0;
            _total = 0;
        }
    }

    public TransferSnapshot Snapshot()
    {
        lock (_gate)
        {
            var fraction = _total > 0 ? Math.Clamp((double)_transferred / _total, 0, 1) : 0;

            var speed = 0d;
            double? remaining = null;

            if (_samples.Count >= 2)
            {
                var oldest = _samples.Peek();
                var newest = _samples.Last();
                var seconds = (newest.Time - oldest.Time).TotalSeconds;
                var bytes = newest.Bytes - oldest.Bytes;
                if (seconds > 0 && bytes > 0)
                {
                    speed = bytes / seconds;
                    if (_total > _transferred) remaining = (_total - _transferred) / speed;
                }
            }

            return new TransferSnapshot(_transferred, _total, fraction, speed, remaining);
        }
    }
}
