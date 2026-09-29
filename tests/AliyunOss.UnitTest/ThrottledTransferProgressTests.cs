using DevToolkit.AliyunOss.Models;
using DevToolkit.AliyunOss.Utils;

namespace AliyunOss.UnitTest;

/// <summary>
/// 上传进度上报的节流：OSS SDK 按数据块回调（128MB 就有千余次），
/// 每次都 Post 到 UI 线程会挤满消息队列，大文件下气泡明显卡顿，所以在投递前滤掉。
/// </summary>
public class ThrottledTransferProgressTests
{
    private sealed class Collector : IProgress<TransferProgress>
    {
        public List<TransferProgress> Received { get; } = [];

        public void Report(TransferProgress value) => Received.Add(value);
    }

    [Fact]
    public void Report_FirstSampleAlwaysPasses()
    {
        var inner = new Collector();
        var throttled = new ThrottledTransferProgress(inner, TimeSpan.FromSeconds(10));

        throttled.Report(new TransferProgress(1, 1000));

        // 首个样本必须放行，否则起步阶段界面一直停在初始值
        Assert.Single(inner.Received);
    }

    [Fact]
    public void Report_DropsSamplesWithinTheWindow()
    {
        var inner = new Collector();
        var throttled = new ThrottledTransferProgress(inner, TimeSpan.FromMinutes(5));

        for (var i = 1; i <= 500; i++)
        {
            throttled.Report(new TransferProgress(i, 1000));
        }

        // 窗口极长 → 除首个外全部丢弃：这是"别把千余次回调全灌进 UI 线程"的核心保证
        Assert.Single(inner.Received);
    }

    [Fact]
    public void Report_FinalSampleAlwaysPasses()
    {
        var inner = new Collector();
        var throttled = new ThrottledTransferProgress(inner, TimeSpan.FromMinutes(5));

        throttled.Report(new TransferProgress(10, 1000));
        throttled.Report(new TransferProgress(1000, 1000));

        // 结束样本承载最终百分比，被压掉界面就停在最后一个中间值
        Assert.Equal(2, inner.Received.Count);
        Assert.Equal(1000, inner.Received[^1].Transferred);
    }

    [Fact]
    public void Report_UnknownTotal_KeepsFinalDetectionOff()
    {
        var inner = new Collector();
        var throttled = new ThrottledTransferProgress(inner, TimeSpan.FromMinutes(5));

        throttled.Report(new TransferProgress(100, 0));
        throttled.Report(new TransferProgress(200, 0));

        // Total 为 0 表示长度未知，不能把任何样本当作结束
        Assert.Single(inner.Received);
    }

    [Fact]
    public void Reset_LetsNextSampleThroughImmediately()
    {
        var inner = new Collector();
        var throttled = new ThrottledTransferProgress(inner, TimeSpan.FromMinutes(5));

        throttled.Report(new TransferProgress(10, 1000));
        throttled.Report(new TransferProgress(20, 1000));
        Assert.Single(inner.Received);

        // 切到下一个文件：首样本要立即投递，否则新文件起步进度被上一文件的时间戳压掉
        throttled.Reset();
        throttled.Report(new TransferProgress(30, 1000));

        Assert.Equal(2, inner.Received.Count);
    }
}
