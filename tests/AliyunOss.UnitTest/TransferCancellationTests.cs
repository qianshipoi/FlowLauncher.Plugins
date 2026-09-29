using FlowLauncher.Plugin.AliyunOss.Transfer;

namespace AliyunOss.UnitTest;

/// <summary>
/// 区分用户取消与传输失败。上传被取消时 OSS SDK 抛的是它自己的
/// <c>OperationException</c>，内层才包着 <c>TaskCanceledException</c>（实测 PutObject 如此），
/// 只 catch <see cref="OperationCanceledException"/> 会漏判、把取消显示成失败。
/// 这里用 SDK 的真实异常形态锁住判断逻辑。
/// </summary>
public class TransferCancellationTests
{
    /// <summary>复刻 SDK 的包装：OperationException("operation error PutObject: ...") 内层 TaskCanceledException。</summary>
    private static Exception SdkCancelWrapped() =>
        new InvalidOperationException(
            "operation error PutObject: System.Threading.Tasks.TaskCanceledException: The operation was canceled.",
            new TaskCanceledException("The operation was canceled."));

    [Fact]
    public void SdkWrappedCancellation_IsUserCancellation_EvenWithoutTokenFlag()
    {
        // SDK 把取消包在自己异常里，类型上不是 OperationCanceledException；
        // 靠异常链仍要认出这是取消，否则常驻气泡不会回到等待拖入
        Assert.True(TransferCancellation.IsUserCancellation(SdkCancelWrapped(), isCancellationRequested: false));
    }

    [Fact]
    public void TokenRequested_IsUserCancellation_RegardlessOfException()
    {
        // 令牌只在 Abort() 里被取消，是"用户按了取消"的权威信号
        Assert.True(TransferCancellation.IsUserCancellation(new Exception("whatever"), isCancellationRequested: true));
    }

    [Fact]
    public void PlainOperationCanceledException_IsUserCancellation()
    {
        Assert.True(TransferCancellation.IsUserCancellation(
            new OperationCanceledException(), isCancellationRequested: false));
    }

    [Fact]
    public void DirectTaskCanceledException_IsUserCancellation()
    {
        Assert.True(TransferCancellation.IsUserCancellation(
            new TaskCanceledException(), isCancellationRequested: false));
    }

    [Fact]
    public void ServiceError_IsNotUserCancellation()
    {
        // 服务端返回 500 这类真失败必须报失败，不能被当成取消静默吞掉
        var serviceError = new InvalidOperationException(
            "operation error PutObject: Error returned by Service.",
            new Exception("InternalError"));

        Assert.False(TransferCancellation.IsUserCancellation(serviceError, isCancellationRequested: false));
    }

    [Fact]
    public void HttpTimeout_IsNotUserCancellation()
    {
        // HTTP 读写超时同样抛 TaskCanceledException（内层 TimeoutException），那是真失败：
        // 若按异常类型一律当取消，超时会被静默吞掉、用户看不到任何错误
        var timeout = new InvalidOperationException(
            "operation error PutObject: timeout",
            new TimeoutException("The request timed out."));

        Assert.False(TransferCancellation.IsUserCancellation(timeout, isCancellationRequested: false));
    }

    [Fact]
    public void SdkRequestTimeout_IsNotUserCancellation()
    {
        // 实测 SDK 读超时的真实形态：OperationException 包 RequestTimeoutException 包 TaskCanceledException。
        // 关键：SDK 的 RequestTimeoutException 直接继承 Exception（不是 System.TimeoutException），
        // 只排除 TimeoutException 会漏掉它，把真超时当成用户取消静默吞掉。
        var sdkTimeout = new InvalidOperationException(
            "operation error PutObject: The operation was cancelled because it exceeded the configured timeout of 0:00:01.5.",
            new AlibabaCloud.OSS.V2.RequestTimeoutException(
                "The operation was cancelled because it exceeded the configured timeout of 0:00:01.5.",
                new TaskCanceledException("The operation was canceled.")));

        Assert.False(TransferCancellation.IsUserCancellation(sdkTimeout, isCancellationRequested: false));
    }

    [Fact]
    public void TimeoutNestedWithCancellation_StillNotUserCancellation()
    {
        // 超时里再套一层取消也还是超时，不能被内层的取消类型带偏
        var nested = new InvalidOperationException(
            "outer",
            new TimeoutException("timed out", new TaskCanceledException()));

        Assert.False(TransferCancellation.IsUserCancellation(nested, isCancellationRequested: false));
    }
}
