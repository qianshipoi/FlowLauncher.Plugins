namespace FlowLauncher.Plugin.AliyunOss.Transfer;

/// <summary>
/// 传输流程向界面反馈的接口，由传输窗口的 ViewModel 实现。
/// 流程本身（Core）不直接接触 WPF 控件，便于独立测试与复用。
/// </summary>
public interface ITransferUi
{
    /// <summary>更新状态文案。</summary>
    void SetStatus(string text);

    /// <summary>询问同名对象已存在时的处理方式。取消传输时该任务应随之取消。</summary>
    Task<UploadConflictChoice> AskConflictAsync(string objectName, CancellationToken cancellationToken);
}
