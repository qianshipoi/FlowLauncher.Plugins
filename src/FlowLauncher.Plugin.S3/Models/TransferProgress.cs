namespace FlowLauncher.Plugin.S3.Models;

/// <summary>传输进度（字节）。<c>Total</c> 为 0 表示服务端未给出总长度，进度不可计算。</summary>
public readonly record struct TransferProgress(long Transferred, long Total)
{
    public double Fraction => Total > 0 ? Math.Clamp((double)Transferred / Total, 0, 1) : 0;

    public static TransferProgress Completed(long length) => new(length, length);
}
