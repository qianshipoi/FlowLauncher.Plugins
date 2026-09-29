namespace FlowLauncher.Plugin.AliyunOss.Models;

/// <summary>一次进度采样后的速率/剩余时间快照。</summary>
public readonly record struct TransferSnapshot(
    long Transferred,
    long Total,
    double Fraction,
    double BytesPerSecond,
    double? RemainingSeconds)
{
    public bool HasTotal => Total > 0;
}
