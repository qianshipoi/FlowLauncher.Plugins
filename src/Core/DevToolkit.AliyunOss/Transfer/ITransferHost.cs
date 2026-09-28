namespace DevToolkit.AliyunOss.Transfer;

/// <summary>传输窗口所需的宿主能力，由各宿主（Flow Launcher 插件 / 独立 App）适配。</summary>
public interface ITransferHost
{
    /// <summary>复制文本到剪贴板。是否额外弹提示由宿主决定。</summary>
    void CopyText(string text);

    /// <summary>在资源管理器中定位并选中指定文件。</summary>
    void RevealInExplorer(string filePath);

    /// <summary>
    /// 读取气泡窗口的上次位置。从未记录或读取失败时返回 null（窗口会初始化到屏幕中央）。
    /// </summary>
    BubblePosition? LoadBubblePosition();

    /// <summary>持久化气泡窗口的位置（拖动结束时调用）。失败可静默忽略。</summary>
    void SaveBubblePosition(BubblePosition position);
}

/// <summary>气泡窗口在屏幕上的位置（窗口左上角，设备无关像素）。</summary>
public sealed record BubblePosition(double X, double Y);
