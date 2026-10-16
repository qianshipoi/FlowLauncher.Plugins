using System.Threading;

using OSS = AlibabaCloud.OSS.V2;

namespace FlowLauncher.Plugin.S3.Transfer;

/// <summary>
/// 区分"用户主动取消"与"传输真失败"。
/// <para>
/// 为什么不能只看异常类型：上传被取消时，OSS SDK 抛的是
/// <c>AlibabaCloud.OSS.V2.OperationException</c>，内层才包着 <c>TaskCanceledException</c>（实测 PutObject 如此）。
/// 只 catch <see cref="OperationCanceledException"/> 会完全漏掉它，取消就被当成失败，
/// 常驻拖拽气泡也不会回到等待拖入的状态。
/// </para>
/// </summary>
public static class TransferCancellation
{
    /// <summary>
    /// 该异常是否代表"用户按了取消"。
    /// </summary>
    /// <param name="exception">捕获到的异常（可能是 SDK 的包装异常）。</param>
    /// <param name="isCancellationRequested">
    /// 本次传输的取消令牌是否已置位。它是权威信号：令牌只在用户取消时才被取消。
    /// </param>
    public static bool IsUserCancellation(Exception exception, bool isCancellationRequested)
    {
        if (isCancellationRequested) return true;

        var sawCancellation = false;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            // 超时也抛 TaskCanceledException（实测 SDK 的 RequestTimeoutException 内层就是它），
            // 但那是真失败：若按取消静默吞掉，用户会看不到任何错误。
            // 注意 SDK 的超时类型直接继承 Exception，不是 System.TimeoutException，两种都要拦。
            if (current is TimeoutException or OSS.RequestTimeoutException) return false;
            if (current is OperationCanceledException) sawCancellation = true;
        }

        return sawCancellation;
    }
}
